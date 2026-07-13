// <copyright file="PrefetchBrokerSourceScopeTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.EFCoreSqlite.Models.Journal;
using Echoglossian.LanguagesHandling;
using Echoglossian.NativeUI.Helpers;

using Xunit;

using PluginEntry = Echoglossian.Echoglossian;

namespace Echoglossian.Tests;

/// <summary>
///     Covers real prefetch entry-to-row persistence orchestration.
/// </summary>
public class PrefetchBrokerSourceScopeTests : IDisposable
{
    private const string ExpectedBrokerScope = "|Scope|chs|pt-BR|4|True";

    private readonly Dictionary<int, LanguageInfo> originalLanguages;

    /// <summary>
    ///     Initializes the target-language table required by runtime scope
    ///     capture without constructing the Dalamud plugin.
    /// </summary>
    public PrefetchBrokerSourceScopeTests()
    {
        this.originalLanguages = PluginEntry.LangDict;
        PluginEntry.LangDict = new Dictionary<int, LanguageInfo>
        {
            [42] = new LanguageInfo(
                "he",
                "Hebrew",
                string.Empty,
                string.Empty,
                []),
            [81] = new LanguageInfo(
                "pt",
                "Brazilian Portuguese",
                string.Empty,
                string.Empty,
                []),
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        PluginEntry.LangDict = this.originalLanguages;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Ensures queued completion keeps the broker key and persisted row on
    ///     the scope captured by each production prefetch entry.
    /// </summary>
    /// <param name="family">The production prefetch family to exercise.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData(PrefetchFamily.ActionDetail)]
    [InlineData(PrefetchFamily.ReferenceText)]
    [InlineData(PrefetchFamily.AcceptedQuest)]
    public async Task PrefetchEntry_QueuedCompletionAfterLiveScopeChanges_PersistsCapturedRow(
        PrefetchFamily family)
    {
        var configuration = CreateConfiguration();
        SourceClientLanguage? liveSource =
            new SourceClientLanguage("chs", "zh-CN");
        var resolverGate = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var persistenceCompletion = new TaskCompletionSource<object>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        string? capturedBrokerKey = null;

        using var broker = CreateBroker();

        var result = RunEntry(
            family,
            () => liveSource,
            configuration,
            broker.TryGetCached,
            Queue,
            Persist);

        liveSource = new SourceClientLanguage("cht", "zh-CN");
        MutateConfiguration(configuration);
        resolverGate.TrySetResult(true);

        var persistedRow = await persistenceCompletion.Task.WaitAsync(
            TimeSpan.FromSeconds(2));

        Assert.Equal(PrefetchTranslationDispatchResult.Queued, result);
        Assert.EndsWith(ExpectedBrokerScope, capturedBrokerKey);
        AssertCapturedRowScope(persistedRow);
        return;

        bool Queue(
            string key,
            Func<string> resolver,
            Action<string>? onResolved)
        {
            capturedBrokerKey = key;
            return broker.Queue(
                key,
                async () =>
                {
                    await resolverGate.Task.ConfigureAwait(false);
                    return resolver();
                },
                onResolved);
        }

        void Persist(object row)
        {
            persistenceCompletion.TrySetResult(row);
        }
    }

    /// <summary>
    ///     Ensures cache-hit completion uses the scope captured before broker
    ///     lookup even when live state changes during that lookup.
    /// </summary>
    /// <param name="family">The production prefetch family to exercise.</param>
    [Theory]
    [InlineData(PrefetchFamily.ActionDetail)]
    [InlineData(PrefetchFamily.ReferenceText)]
    [InlineData(PrefetchFamily.AcceptedQuest)]
    public void PrefetchEntry_CacheHitWhileLiveScopeChanges_PersistsCapturedRow(
        PrefetchFamily family)
    {
        var configuration = CreateConfiguration();
        SourceClientLanguage? liveSource =
            new SourceClientLanguage("chs", "zh-CN");
        string? capturedBrokerKey = null;
        object? persistedRow = null;
        var brokerQueueCalls = 0;
        var translatorCalls = 0;

        var result = RunEntry(
            family,
            () => liveSource,
            configuration,
            TryGet,
            (_, _, _) =>
            {
                brokerQueueCalls++;
                return false;
            },
            row => persistedRow = row,
            () => translatorCalls++);

        Assert.Equal(PrefetchTranslationDispatchResult.Cached, result);
        Assert.EndsWith(ExpectedBrokerScope, capturedBrokerKey);
        Assert.NotNull(persistedRow);
        AssertCapturedRowScope(persistedRow);
        Assert.Equal(0, brokerQueueCalls);
        Assert.Equal(0, translatorCalls);
        return;

        bool TryGet(string key, out string translatedText)
        {
            capturedBrokerKey = key;
            liveSource = new SourceClientLanguage("cht", "zh-CN");
            MutateConfiguration(configuration);
            translatedText = "cached translation";
            return true;
        }
    }

    /// <summary>
    ///     Ensures unknown source entry fails before any production prefetch
    ///     family reaches broker lookup or queueing.
    /// </summary>
    /// <param name="family">The production prefetch family to exercise.</param>
    [Theory]
    [InlineData(PrefetchFamily.ActionDetail)]
    [InlineData(PrefetchFamily.ReferenceText)]
    [InlineData(PrefetchFamily.AcceptedQuest)]
    public void PrefetchEntry_UnknownSource_DoesNotReachBroker(
        PrefetchFamily family)
    {
        var brokerLookupCalls = 0;
        var brokerQueueCalls = 0;
        var translatorCalls = 0;
        var persistenceCalls = 0;

        var result = RunEntry(
            family,
            () => new SourceClientLanguage("unknown", "unknown"),
            CreateConfiguration(),
            TryGet,
            (_, _, _) =>
            {
                brokerQueueCalls++;
                return false;
            },
            _ => persistenceCalls++,
            () => translatorCalls++);

        Assert.Equal(PrefetchTranslationDispatchResult.Rejected, result);
        Assert.Equal(0, brokerLookupCalls);
        Assert.Equal(0, brokerQueueCalls);
        Assert.Equal(0, translatorCalls);
        Assert.Equal(0, persistenceCalls);
        return;

        bool TryGet(string key, out string translatedText)
        {
            brokerLookupCalls++;
            translatedText = string.Empty;
            return false;
        }
    }

    private static PrefetchTranslationDispatchResult RunEntry(
        PrefetchFamily family,
        Func<SourceClientLanguage?> sourceLanguageResolver,
        Config configuration,
        TryGetPrefetchTranslationDelegate tryGetTranslation,
        QueuePrefetchTranslationDelegate queueTranslation,
        Action<object> persistRow,
        Action? onTranslate = null)
    {
        return family switch
        {
            PrefetchFamily.ActionDetail =>
                PluginEntry.RunActionDetailNamePrefetchEntry(
                    "ActionDetailPrefetch|139|Name|Original action",
                    new ActionTooltipCanonicalPayload
                    {
                        ActionId = 139,
                        Name = "Original action",
                    },
                    "test-version",
                    sourceLanguageResolver,
                    configuration,
                    tryGetTranslation,
                    queueTranslation,
                    Translate,
                    _ => null,
                    row => persistRow(row)),
            PrefetchFamily.ReferenceText =>
                PluginEntry.RunReferenceTextNamePrefetchEntry(
                    "MainCommandPrefetch|139|Name|Original command",
                    new ReferenceTextCanonicalPayload
                    {
                        ReferenceId = 139,
                        Name = "Original command",
                    },
                    "test-version",
                    sourceLanguageResolver,
                    configuration,
                    tryGetTranslation,
                    queueTranslation,
                    Translate,
                    static (
                        originalLanguage,
                        targetLanguage,
                        translationEngine,
                        gameVersion,
                        originalPayload,
                        translatedPayload) =>
                        ReferenceTextPersistenceHelper
                            .CreateCanonicalRow<MainCommandText>(
                                originalLanguage,
                                targetLanguage,
                                translationEngine,
                                gameVersion,
                                originalPayload,
                                translatedPayload),
                    _ => null,
                    row => persistRow(row)),
            PrefetchFamily.AcceptedQuest =>
                PluginEntry.RunAcceptedQuestNamePrefetchEntry(
                    "AcceptedQuestPrefetch|139:0|Name|Original quest",
                    CreateQuestCanonicalData(),
                    sourceLanguageResolver,
                    configuration,
                    tryGetTranslation,
                    queueTranslation,
                    Translate,
                    (row, _) => persistRow(row)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(family),
                family,
                null),
        };

        string Translate(
            string sourceText,
            SourceClientLanguage sourceLanguage,
            string targetLanguage)
        {
            onTranslate?.Invoke();
            return "translated text";
        }
    }

    private static Config CreateConfiguration()
    {
        return new Config
        {
            Lang = 81,
            ChosenTransEngine = 4,
            TranslateAlreadyTranslatedTexts = true,
        };
    }

    private static void MutateConfiguration(Config configuration)
    {
        configuration.Lang = 42;
        configuration.ChosenTransEngine = 8;
        configuration.TranslateAlreadyTranslatedTexts = false;
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

    private static QuestCanonicalData CreateQuestCanonicalData()
    {
        var snapshot = new QuestProgressSnapshot(
            139,
            0,
            "Original quest",
            "quest/test/139",
            [],
            [],
            [],
            "test-hash");
        return QuestCanonicalData.Create(snapshot, "test-version");
    }

    private static void AssertCapturedRowScope(object persistedRow)
    {
        var actualScope = persistedRow switch
        {
            ActionTooltip row => new TranslationReuseScope(
                row.OriginalLang ?? string.Empty,
                row.TranslationLang ?? string.Empty,
                row.TranslationEngine,
                true),
            ReferenceTextRowBase row => new TranslationReuseScope(
                row.OriginalLang ?? string.Empty,
                row.TranslationLang ?? string.Empty,
                row.TranslationEngine,
                true),
            QuestPlate row => new TranslationReuseScope(
                row.OriginalLang ?? string.Empty,
                row.TranslationLang ?? string.Empty,
                row.TranslationEngine,
                true),
            _ => throw new ArgumentOutOfRangeException(
                nameof(persistedRow),
                persistedRow,
                null),
        };

        Assert.Equal(
            new TranslationReuseScope("chs", "pt-BR", 4, true),
            actualScope);
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
