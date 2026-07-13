// <copyright file="PrefetchBrokerSourceScopeTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;
using System.Runtime.CompilerServices;

using Echoglossian.EFCoreSqlite.Models.Journal;
using Echoglossian.NativeUI.Helpers;

using Xunit;

using PluginEntry = Echoglossian.Echoglossian;

namespace Echoglossian.Tests;

/// <summary>
///     Covers operation-scoped broker identity and callback persistence in
///     canonical prefetch runtimes.
/// </summary>
public class PrefetchBrokerSourceScopeTests
{
    /// <summary>
    ///     Ensures otherwise-identical action, reference, and accepted-quest
    ///     work cannot share broker results across source scopes.
    /// </summary>
    /// <param name="keyBuilderName">The scoped key-builder method to exercise.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("BuildActionDetailScopedTranslationKey")]
    [InlineData("BuildReferenceTextScopedTranslationKey")]
    [InlineData("BuildAcceptedQuestScopedTranslationKey")]
    public async Task ScopedKey_SourceChanges_QueuesDistinctBrokerWork(
        string keyBuilderName)
    {
        var englishScope = new TranslationReuseScope("en", "pt-BR", 4, true);
        var germanScope = englishScope with { SourceLanguageCode = "de" };
        var englishKey = BuildScopedKey(
            keyBuilderName,
            "identical-payload",
            englishScope);
        var germanKey = BuildScopedKey(
            keyBuilderName,
            "identical-payload",
            germanScope);
        var completions = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var completionCount = 0;

        using var broker = new QueuedTranslationBroker(
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(25),
            maxRateLimitRetries: 0);

        Assert.True(broker.Queue(
            englishKey,
            () => Task.FromResult("english-result"),
            _ => SignalCompletion()));
        Assert.True(broker.Queue(
            germanKey,
            () => Task.FromResult("german-result"),
            _ => SignalCompletion()));

        await completions.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.NotEqual(englishKey, germanKey);
        Assert.True(broker.TryGetCached(englishKey, out var englishResult));
        Assert.True(broker.TryGetCached(germanKey, out var germanResult));
        Assert.Equal("english-result", englishResult);
        Assert.Equal("german-result", germanResult);
        return;

        void SignalCompletion()
        {
            if (Interlocked.Increment(ref completionCount) == 2)
            {
                completions.TrySetResult(true);
            }
        }
    }

    /// <summary>
    ///     Ensures every field in the immutable reuse scope participates in
    ///     broker identity for each prefetch family.
    /// </summary>
    /// <param name="keyBuilderName">The scoped key-builder method to exercise.</param>
    [Theory]
    [InlineData("BuildActionDetailScopedTranslationKey")]
    [InlineData("BuildReferenceTextScopedTranslationKey")]
    [InlineData("BuildAcceptedQuestScopedTranslationKey")]
    public void ScopedKey_EachScopeFieldChanges_ProducesDistinctIdentity(
        string keyBuilderName)
    {
        var baseline = new TranslationReuseScope("en", "pt-BR", 4, true);
        var scopes = new[]
        {
            baseline,
            baseline with { SourceLanguageCode = "de" },
            baseline with { TargetLanguageCode = "de" },
            baseline with { TranslationEngine = 5 },
            baseline with { RequireMatchingEngine = false },
        };

        var keys = scopes
            .Select(scope => BuildScopedKey(
                keyBuilderName,
                "identical-payload",
                scope))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(scopes.Length, keys.Count);
    }

    /// <summary>
    ///     Ensures every delayed persistence callback receives the immutable
    ///     operation scope instead of rebuilding identity from live state.
    /// </summary>
    [Fact]
    public void CallbackPersistenceMethods_CarryCapturedReuseScope()
    {
        var callbackMethodNames = new[]
        {
            "ApplyActionDetailTranslation",
            "ApplyReferenceTextTranslation",
            "ApplyAcceptedQuestNameTranslation",
            "ApplyAcceptedQuestMessageTranslation",
            "ApplyAcceptedQuestSummaryTranslation",
            "ApplyAcceptedQuestObjectiveTranslation",
            "ApplyAcceptedQuestSystemTranslation",
        };
        var methods = typeof(PluginEntry).GetMethods(
            BindingFlags.Instance | BindingFlags.NonPublic);

        foreach (var callbackMethodName in callbackMethodNames)
        {
            var callback = Assert.Single(
                methods,
                method => method.Name == callbackMethodName);
            Assert.Contains(
                callback.GetParameters(),
                parameter => parameter.ParameterType ==
                             typeof(TranslationReuseScope));
        }
    }

    /// <summary>
    ///     Ensures accepted-quest row creation persists the operation-captured
    ///     source, target, and engine without consulting later live config.
    /// </summary>
    [Fact]
    public void CreateAcceptedQuestPrefetchPlate_UsesCapturedScope()
    {
        var method = typeof(PluginEntry).GetMethod(
            "CreateAcceptedQuestPrefetchPlate",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(QuestCanonicalData), typeof(TranslationReuseScope)],
            modifiers: null);
        Assert.NotNull(method);

        var snapshot = new QuestProgressSnapshot(
            1,
            0,
            "A Scoped Quest",
            "Quest/000/Test",
            [],
            [],
            [],
            "hash");
        var canonicalData = QuestCanonicalData.Create(snapshot, "test-version");
        var capturedScope = new TranslationReuseScope(
            "chs",
            "he",
            8,
            true);
        var runtime = (PluginEntry)RuntimeHelpers.GetUninitializedObject(
            typeof(PluginEntry));

        var result = method!.Invoke(runtime, [canonicalData, capturedScope]);
        var questPlate = Assert.IsType<QuestPlate>(result);

        Assert.Equal("chs", questPlate.OriginalLang);
        Assert.Equal("he", questPlate.TranslationLang);
        Assert.Equal(8, questPlate.TranslationEngine);
    }

    /// <summary>
    ///     Invokes one production scoped-key builder.
    /// </summary>
    /// <param name="methodName">The key-builder method name.</param>
    /// <param name="payloadIdentity">The existing payload identity.</param>
    /// <param name="scope">The immutable operation scope.</param>
    /// <returns>The scoped broker key.</returns>
    private static string BuildScopedKey(
        string methodName,
        string payloadIdentity,
        TranslationReuseScope scope)
    {
        var method = typeof(PluginEntry).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        return Assert.IsType<string>(
            method!.Invoke(null, [payloadIdentity, scope]));
    }
}
