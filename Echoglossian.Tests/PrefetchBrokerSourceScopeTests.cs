// <copyright file="PrefetchBrokerSourceScopeTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

using Xunit;

using PluginEntry = Echoglossian.Echoglossian;

namespace Echoglossian.Tests;

/// <summary>
///     Covers production prefetch dispatch through shared broker completion and
///     cache-hit paths.
/// </summary>
public class PrefetchBrokerSourceScopeTests
{
    /// <summary>
    ///     Ensures action, reference, and accepted-quest production dispatch
    ///     keeps broker and persistence callbacks bound to captured scopes.
    /// </summary>
    /// <param name="family">The production prefetch family to exercise.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData(PrefetchFamily.ActionDetail)]
    [InlineData(PrefetchFamily.ReferenceText)]
    [InlineData(PrefetchFamily.AcceptedQuest)]
    public async Task DispatchPrefetchTranslation_SourceChangesBeforeCompletion_UsesCapturedScope(
        PrefetchFamily family)
    {
        var configuration = new Config
        {
            ChosenTransEngine = 4,
            TranslateAlreadyTranslatedTexts = true,
        };
        var currentSource = new SourceClientLanguage("chs", "zh-CN");
        var capturedScope = CreateScope(currentSource, configuration, "pt-BR");
        var currentScope = capturedScope;
        var resolverGate = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var completionSource = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var persistedScopes = new List<TranslationReuseScope>();
        var completionCount = 0;
        var queuedResolverCount = 0;

        using var broker = CreateBroker();

        var firstResult = Dispatch(
            family,
            "identical-payload",
            currentSource,
            currentScope,
            broker.TryGetCached,
            Queue,
            () =>
            {
                return "chs-result";
            },
            Persist);

        currentSource = new SourceClientLanguage("cht", "zh-CN");
        var secondCapturedScope = CreateScope(
            currentSource,
            configuration,
            "pt-BR");

        var secondResult = Dispatch(
            family,
            "identical-payload",
            currentSource,
            secondCapturedScope,
            broker.TryGetCached,
            Queue,
            () => "cht-result",
            Persist);

        configuration.ChosenTransEngine = 8;
        configuration.TranslateAlreadyTranslatedTexts = false;
        currentScope = CreateScope(currentSource, configuration, "he");

        resolverGate.TrySetResult(true);
        await completionSource.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(PrefetchTranslationDispatchResult.Queued, firstResult);
        Assert.Equal(PrefetchTranslationDispatchResult.Queued, secondResult);
        Assert.Equal([capturedScope, secondCapturedScope], persistedScopes);
        Assert.DoesNotContain(currentScope, persistedScopes);

        var unexpectedResolverCalls = 0;
        TranslationReuseScope? cacheHitScope = null;
        bool? cacheHitFlag = null;
        var cacheResult = Dispatch(
            family,
            "identical-payload",
            new SourceClientLanguage("chs", "zh-CN"),
            capturedScope,
            broker.TryGetCached,
            Queue,
            () =>
            {
                unexpectedResolverCalls++;
                return "unexpected";
            },
            (translatedText, scope, fromCache) =>
            {
                Assert.Equal("chs-result", translatedText);
                cacheHitScope = scope;
                cacheHitFlag = fromCache;
            });

        Assert.Equal(PrefetchTranslationDispatchResult.Cached, cacheResult);
        Assert.Equal(capturedScope, cacheHitScope);
        Assert.True(cacheHitFlag);
        Assert.Equal(0, unexpectedResolverCalls);
        return;

        bool Queue(
            string key,
            Func<string> resolver,
            Action<string>? onResolved)
        {
            var queuePosition = Interlocked.Increment(ref queuedResolverCount);
            Func<Task<string>> queuedResolver = queuePosition == 1
                ? async () =>
                {
                    await resolverGate.Task.ConfigureAwait(false);
                    return resolver();
                }
                : () => Task.FromResult(resolver());
            return broker.Queue(key, queuedResolver, onResolved);
        }

        void Persist(
            string translatedText,
            TranslationReuseScope scope,
            bool fromCache)
        {
            Assert.False(fromCache);
            persistedScopes.Add(scope);
            if (Interlocked.Increment(ref completionCount) == 2)
            {
                completionSource.TrySetResult(true);
            }
        }
    }

    /// <summary>
    ///     Ensures unknown and internally mismatched source contracts are
    ///     rejected before any production prefetch family reaches the broker.
    /// </summary>
    [Fact]
    public void DispatchPrefetchTranslation_UnknownOrMismatchedSource_DoesNotReachBroker()
    {
        var families = Enum.GetValues<PrefetchFamily>();
        var invalidRequests = new[]
        {
            (Source: default(SourceClientLanguage), Scope: default(TranslationReuseScope)),
            (
                Source: new SourceClientLanguage("unknown", "unknown"),
                Scope: new TranslationReuseScope("unknown", "pt-BR", 4, true)),
            (
                Source: new SourceClientLanguage("chs", "zh-TW"),
                Scope: new TranslationReuseScope("chs", "pt-BR", 4, true)),
            (
                Source: new SourceClientLanguage("chs", "zh-CN"),
                Scope: new TranslationReuseScope("cht", "pt-BR", 4, true)),
        };
        var brokerLookupCalls = 0;
        var brokerQueueCalls = 0;
        var resolverCalls = 0;
        var callbackCalls = 0;

        foreach (var family in families)
        {
            foreach (var request in invalidRequests)
            {
                var result = Dispatch(
                    family,
                    "unknown-payload",
                    request.Source,
                    request.Scope,
                    TryGet,
                    Queue,
                    () =>
                    {
                        resolverCalls++;
                        return "unexpected";
                    },
                    (_, _, _) => callbackCalls++);

                Assert.Equal(PrefetchTranslationDispatchResult.Rejected, result);
            }
        }

        Assert.Equal(0, brokerLookupCalls);
        Assert.Equal(0, brokerQueueCalls);
        Assert.Equal(0, resolverCalls);
        Assert.Equal(0, callbackCalls);
        return;

        bool TryGet(string key, out string translatedText)
        {
            brokerLookupCalls++;
            translatedText = string.Empty;
            return false;
        }

        bool Queue(
            string key,
            Func<string> resolver,
            Action<string>? onResolved)
        {
            brokerQueueCalls++;
            return false;
        }
    }

    private static QueuedTranslationBroker CreateBroker()
    {
        return new QueuedTranslationBroker(
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromMilliseconds(25),
            maxRateLimitRetries: 0);
    }

    private static TranslationReuseScope CreateScope(
        SourceClientLanguage sourceLanguage,
        Config configuration,
        string targetLanguage)
    {
        return new TranslationReuseScope(
            sourceLanguage.PersistenceCode,
            targetLanguage,
            configuration.ChosenTransEngine,
            configuration.TranslateAlreadyTranslatedTexts);
    }

    private static PrefetchTranslationDispatchResult Dispatch(
        PrefetchFamily family,
        string payloadIdentity,
        SourceClientLanguage sourceLanguage,
        TranslationReuseScope scope,
        TryGetPrefetchTranslationDelegate tryGetTranslation,
        QueuePrefetchTranslationDelegate queueTranslation,
        Func<string> resolver,
        Action<string, TranslationReuseScope, bool> onCompleted)
    {
        return family switch
        {
            PrefetchFamily.ActionDetail =>
                PluginEntry.DispatchActionDetailPrefetchTranslation(
                    payloadIdentity,
                    sourceLanguage,
                    scope,
                    tryGetTranslation,
                    queueTranslation,
                    resolver,
                    onCompleted),
            PrefetchFamily.ReferenceText =>
                PluginEntry.DispatchReferenceTextPrefetchTranslation(
                    payloadIdentity,
                    sourceLanguage,
                    scope,
                    tryGetTranslation,
                    queueTranslation,
                    resolver,
                    onCompleted),
            PrefetchFamily.AcceptedQuest =>
                PluginEntry.DispatchAcceptedQuestPrefetchTranslation(
                    payloadIdentity,
                    sourceLanguage,
                    scope,
                    tryGetTranslation,
                    queueTranslation,
                    resolver,
                    onCompleted),
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, null),
        };
    }

    /// <summary>
    ///     Identifies one production prefetch family under test.
    /// </summary>
    public enum PrefetchFamily
    {
        /// <summary>
        ///     Action-detail prefetch.
        /// </summary>
        ActionDetail,

        /// <summary>
        ///     Sheet-backed reference-text prefetch.
        /// </summary>
        ReferenceText,

        /// <summary>
        ///     Accepted-quest prefetch.
        /// </summary>
        AcceptedQuest,
    }
}
