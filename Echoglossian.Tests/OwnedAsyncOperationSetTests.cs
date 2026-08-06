// <copyright file="OwnedAsyncOperationSetTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers ownership, cancellation, and error observation for background
///     native UI operations.
/// </summary>
public class OwnedAsyncOperationSetTests
{
    /// <summary>
    ///     Ensures starting a suspended operation does not wait for its
    ///     completion on the caller's thread.
    /// </summary>
    [Fact]
    public void Run_ReturnsBeforeSuspendedOperationCompletes()
    {
        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var operations = new OwnedAsyncOperationSet();

        var accepted = operations.Run(_ => completion.Task);

        Assert.True(accepted);
        Assert.False(completion.Task.IsCompleted);
    }

    /// <summary>
    ///     Ensures disposal cancels the token supplied to active operations.
    /// </summary>
    /// <returns>A task that completes when cancellation is observed.</returns>
    [Fact]
    public async Task Dispose_CancelsActiveOperationToken()
    {
        var cancellationObserved = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var operations = new OwnedAsyncOperationSet();

        Assert.True(
            operations.Run(
                async cancellationToken =>
                {
                    using var registration = cancellationToken.Register(
                        () => cancellationObserved.TrySetResult(true));
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }));

        operations.Dispose();

        await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    /// <summary>
    ///     Ensures a failing cancellation callback does not escape disposal
    ///     and is reported through the injected error observer.
    /// </summary>
    /// <returns>A task that completes when the callback failure is observed.</returns>
    [Fact]
    public async Task Dispose_CancellationCallbackFails_ReportsErrorWithoutThrowing()
    {
        var observedExceptions = new List<Exception>();
        var exceptionObserved = new TaskCompletionSource<Exception>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var operations = new OwnedAsyncOperationSet(
            exception =>
            {
                observedExceptions.Add(exception);
                exceptionObserved.TrySetResult(exception);
            });

        Assert.True(
            operations.Run(
                cancellationToken =>
                {
                    cancellationToken.Register(
                        static () => throw new InvalidOperationException("callback boom"));
                    return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }));

        var disposeException = Record.Exception(operations.Dispose);

        Assert.Null(disposeException);

        var observedException = await exceptionObserved.Task.WaitAsync(
            TimeSpan.FromSeconds(1));

        var cancellationException = Assert.IsType<AggregateException>(observedException);
        var callbackException = Assert.IsType<InvalidOperationException>(
            Assert.Single(cancellationException.Flatten().InnerExceptions));
        Assert.Equal("callback boom", callbackException.Message);
        Assert.Single(observedExceptions);
    }

    /// <summary>
    ///     Ensures a faulted operation is reported once rather than escaping as
    ///     an unobserved task exception.
    /// </summary>
    /// <returns>A task that completes when the unexpected exception is observed.</returns>
    [Fact]
    public async Task Run_UnexpectedException_InvokesErrorObserverOnce()
    {
        var observedExceptions = new List<Exception>();
        var exceptionObserved = new TaskCompletionSource<Exception>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var operations = new OwnedAsyncOperationSet(
            exception =>
            {
                observedExceptions.Add(exception);
                exceptionObserved.TrySetResult(exception);
            });

        Assert.True(
            operations.Run(
                _ => Task.FromException(new InvalidOperationException("boom"))));

        var observedException = await exceptionObserved.Task.WaitAsync(
            TimeSpan.FromSeconds(1));

        Assert.IsType<InvalidOperationException>(observedException);
        Assert.Equal("boom", observedException.Message);
        Assert.Single(observedExceptions);
    }

    /// <summary>
    ///     Ensures disposal prevents the set from accepting further work.
    /// </summary>
    [Fact]
    public void Run_AfterDispose_RejectsOperation()
    {
        using var operations = new OwnedAsyncOperationSet();
        operations.Dispose();

        var accepted = operations.Run(_ => Task.CompletedTask);

        Assert.False(accepted);
    }
}
