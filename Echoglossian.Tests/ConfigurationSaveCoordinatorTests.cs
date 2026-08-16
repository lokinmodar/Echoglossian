// <copyright file="ConfigurationSaveCoordinatorTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers asynchronous configuration persistence coordination for the live
///     plugin save path.
/// </summary>
public sealed class ConfigurationSaveCoordinatorTests
{
    /// <summary>
    ///     Ensures queuing a save does not wait for a suspended persistence
    ///     delegate to finish.
    /// </summary>
    [Fact]
    public void QueueSave_ReturnsBeforeSuspendedPersistenceCompletes()
    {
        using var persistStarted = new ManualResetEventSlim();
        using var releasePersist = new ManualResetEventSlim();
        using var queueReturned = new ManualResetEventSlim();
        var coordinator = new ConfigurationSaveCoordinator(
            async (_, _) =>
            {
                persistStarted.Set();
                releasePersist.Wait();
                await Task.CompletedTask.ConfigureAwait(false);
            });
        var callerThread = new Thread(
            () =>
            {
                coordinator.QueueSave(new Config());
                queueReturned.Set();
            });

        try
        {
            callerThread.Start();

            Assert.True(persistStarted.Wait(TimeSpan.FromSeconds(1)));
            Assert.True(queueReturned.Wait(TimeSpan.FromSeconds(1)));
        }
        finally
        {
            releasePersist.Set();
            callerThread.Join(TimeSpan.FromSeconds(1));
        }
    }

    /// <summary>
    ///     Ensures multiple queued saves collapse to the latest pending
    ///     snapshot while the current persistence write is still in flight.
    /// </summary>
    /// <returns>A task that completes when the background save pump drains.</returns>
    [Fact]
    public async Task QueueSave_CoalescesPendingSnapshotsToLatestAcceptedValue()
    {
        var firstPersistStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstPersist = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var savedPrompts = new List<string?>();
        var invocationCount = 0;
        var coordinator = new ConfigurationSaveCoordinator(
            async (snapshot, _) =>
            {
                savedPrompts.Add(snapshot.ChatGptPrompt);
                if (Interlocked.Increment(ref invocationCount) == 1)
                {
                    firstPersistStarted.TrySetResult(true);
                    await releaseFirstPersist.Task.ConfigureAwait(false);
                }
            });

        coordinator.QueueSave(new Config { ChatGptPrompt = "prompt-a" });
        await firstPersistStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        coordinator.QueueSave(new Config { ChatGptPrompt = "prompt-b" });
        coordinator.QueueSave(new Config { ChatGptPrompt = "prompt-c" });

        releaseFirstPersist.SetResult(true);
        await coordinator.CompleteAsync().WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(new[] { "prompt-a", "prompt-c" }, savedPrompts);
    }

    /// <summary>
    ///     Ensures the final accepted snapshot is flushed when completion
    ///     begins during an in-flight write.
    /// </summary>
    /// <returns>A task that completes when the final snapshot is persisted.</returns>
    [Fact]
    public async Task CompleteAsync_FlushesLastAcceptedSnapshotBeforeReturning()
    {
        var firstPersistStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstPersist = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var savedPrompts = new List<string?>();
        var invocationCount = 0;
        var coordinator = new ConfigurationSaveCoordinator(
            async (snapshot, _) =>
            {
                savedPrompts.Add(snapshot.ChatGptPrompt);
                if (Interlocked.Increment(ref invocationCount) == 1)
                {
                    firstPersistStarted.TrySetResult(true);
                    await releaseFirstPersist.Task.ConfigureAwait(false);
                }
            });

        coordinator.QueueSave(new Config { ChatGptPrompt = "prompt-a" });
        await firstPersistStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        coordinator.QueueSave(new Config { ChatGptPrompt = "prompt-b" });

        var completionTask = coordinator.CompleteAsync();
        releaseFirstPersist.SetResult(true);

        await completionTask.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(new[] { "prompt-a", "prompt-b" }, savedPrompts);
    }

    /// <summary>
    ///     Ensures unexpected persistence failures are reported once and do not
    ///     prevent later saves from draining.
    /// </summary>
    /// <returns>A task that completes when the error and later save are both observed.</returns>
    [Fact]
    public async Task PersistAsync_UnexpectedException_IsObservedOnceAndLaterSavesContinue()
    {
        var observedExceptions = new List<Exception>();
        var observedException = new TaskCompletionSource<Exception>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var savedPrompts = new List<string?>();
        var invocationCount = 0;
        var coordinator = new ConfigurationSaveCoordinator(
            (snapshot, _) =>
            {
                if (Interlocked.Increment(ref invocationCount) == 1)
                {
                    throw new InvalidOperationException("boom");
                }

                savedPrompts.Add(snapshot.ChatGptPrompt);
                return Task.CompletedTask;
            },
            exception =>
            {
                observedExceptions.Add(exception);
                observedException.TrySetResult(exception);
            });

        coordinator.QueueSave(new Config { ChatGptPrompt = "prompt-a" });
        var exception = await observedException.Task.WaitAsync(TimeSpan.FromSeconds(1));
        coordinator.QueueSave(new Config { ChatGptPrompt = "prompt-b" });
        await coordinator.CompleteAsync().WaitAsync(TimeSpan.FromSeconds(1));

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal("boom", exception.Message);
        Assert.Single(observedExceptions);
        Assert.Equal(new[] { "prompt-b" }, savedPrompts);
    }

    /// <summary>
    ///     Ensures persistence snapshots retain the saved values even when the
    ///     live config instance mutates afterward.
    /// </summary>
    [Fact]
    public void CreatePersistenceSnapshot_ReturnsIndependentShallowCopy()
    {
        var config = new Config
        {
            ChatGptPrompt = "prompt-a",
            DefaultPluginCulture = "pt-BR",
            Lang = 34,
        };

        var snapshot = config.CreatePersistenceSnapshot();
        config.ChatGptPrompt = "prompt-b";
        config.DefaultPluginCulture = "en";
        config.Lang = 28;

        Assert.NotSame(config, snapshot);
        Assert.Equal("prompt-a", snapshot.ChatGptPrompt);
        Assert.Equal("pt-BR", snapshot.DefaultPluginCulture);
        Assert.Equal(34, snapshot.Lang);
    }

    /// <summary>
    ///     Ensures the shallow snapshot contract remains safe by rejecting any
    ///     new public reference-typed fields that are not strings.
    /// </summary>
    [Fact]
    public void Config_PublicReferenceFieldsRemainStringOnlyForSnapshotSafety()
    {
        var invalidFields = typeof(Config)
            .GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Where(field => !Attribute.IsDefined(field, typeof(NonSerializedAttribute)))
            .Where(field => !field.FieldType.IsValueType)
            .Where(field => field.FieldType != typeof(string))
            .Select(field => $"{field.Name}:{field.FieldType.FullName}")
            .ToArray();

        Assert.True(
            invalidFields.Length == 0,
            $"Expected only string public reference fields in Config, but found: {string.Join(", ", invalidFields)}");
    }
}
