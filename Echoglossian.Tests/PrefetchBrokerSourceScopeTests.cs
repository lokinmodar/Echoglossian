// <copyright file="PrefetchBrokerSourceScopeTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Concurrent;

using Dalamud.Game.Gui.NamePlate;

using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.EFCoreSqlite.Models.Journal;
using Echoglossian.LanguagesHandling;
using Echoglossian.NativeUI.AddonHandlers.NamePlates;
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
    [InlineData(PrefetchFamily.NamePlate)]
    public async Task PrefetchOperation_QueuedNameAfterLiveScopeChanges_PersistsCanonicalAndNameRowsInCapturedScope(
        PrefetchFamily family)
    {
        var configuration = CreateConfiguration();
        var expectedPersistedRows = GetExpectedPersistedRowCount(family);
        var liveSource = new SourceClientLanguage("chs", "zh-CN");
        var resolverGate = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var chsPersistenceCompletion = new TaskCompletionSource<object[]>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var chtPersistenceCompletion = new TaskCompletionSource<object[]>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var chsPersistedRows = new ConcurrentQueue<object>();
        var chtPersistedRows = new ConcurrentQueue<object>();
        string? chsBrokerKey = null;
        string? chtBrokerKey = null;

        using var broker = CreateBroker();

        var chsResult = RunOperation(
            family,
            () => liveSource,
            configuration,
            broker.TryGetCached,
            QueueChs,
            PersistChs,
            out var chsCapturedSource,
            out var chsCapturedScope);

        Assert.Equal(PrefetchTranslationDispatchResult.Queued, chsResult);

        liveSource = new SourceClientLanguage("cht", "zh-CN");
        MutateConfiguration(configuration);

        var chtResult = RunOperation(
            family,
            () => liveSource,
            configuration,
            broker.TryGetCached,
            QueueCht,
            PersistCht,
            out var chtCapturedSource,
            out var chtCapturedScope);

        resolverGate.TrySetResult(true);

        var chsRows = await chsPersistenceCompletion.Task.WaitAsync(
            TimeSpan.FromSeconds(2));
        var chtRows = await chtPersistenceCompletion.Task.WaitAsync(
            TimeSpan.FromSeconds(2));

        Assert.Equal(PrefetchTranslationDispatchResult.Queued, chtResult);
        Assert.Equal(new SourceClientLanguage("chs", "zh-CN"), chsCapturedSource);
        Assert.Equal(
            new TranslationReuseScope("chs", "pt-BR", 4, true),
            chsCapturedScope);
        Assert.Equal(new SourceClientLanguage("cht", "zh-CN"), chtCapturedSource);
        Assert.Equal(
            new TranslationReuseScope("cht", "iw", 8, false),
            chtCapturedScope);
        Assert.EndsWith(ExpectedBrokerScope, chsBrokerKey);
        Assert.EndsWith("|Scope|cht|iw|8|False", chtBrokerKey);
        Assert.NotEqual(chsBrokerKey, chtBrokerKey);
        AssertPersistedRows(family, chsRows, chsCapturedScope);
        AssertPersistedRows(family, chtRows, chtCapturedScope);
        AssertLiveScopeMutated(configuration, liveSource);
        return;

        bool QueueChs(
            string key,
            Func<string> resolver,
            Action<string>? onResolved)
        {
            chsBrokerKey = key;
            return QueueBroker(key, resolver, onResolved);
        }

        bool QueueCht(
            string key,
            Func<string> resolver,
            Action<string>? onResolved)
        {
            chtBrokerKey = key;
            return QueueBroker(key, resolver, onResolved);
        }

        bool QueueBroker(
            string key,
            Func<string> resolver,
            Action<string>? onResolved)
        {
            return broker.Queue(
                key,
                async () =>
                {
                    await resolverGate.Task.ConfigureAwait(false);
                    return resolver();
                },
                onResolved);
        }

        void PersistChs(object row)
        {
            chsPersistedRows.Enqueue(row);
            if (chsPersistedRows.Count == expectedPersistedRows)
            {
                chsPersistenceCompletion.TrySetResult(
                    chsPersistedRows.ToArray());
            }
        }

        void PersistCht(object row)
        {
            chtPersistedRows.Enqueue(row);
            if (chtPersistedRows.Count == expectedPersistedRows)
            {
                chtPersistenceCompletion.TrySetResult(
                    chtPersistedRows.ToArray());
            }
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
    [InlineData(PrefetchFamily.NamePlate)]
    public void PrefetchOperation_CachedNameAfterLiveScopeChanges_PersistsCanonicalAndNameRowsInCapturedScope(
        PrefetchFamily family)
    {
        var configuration = CreateConfiguration();
        var liveSource = new SourceClientLanguage("chs", "zh-CN");
        string? capturedBrokerKey = null;
        var persistedRows = new List<object>();
        var brokerQueueCalls = 0;
        var translatorCalls = 0;

        var result = RunOperation(
            family,
            () => liveSource,
            configuration,
            TryGet,
            (_, _, _) =>
            {
                brokerQueueCalls++;
                return false;
            },
            Persist,
            out var capturedSource,
            out var capturedScope,
            () => translatorCalls++);

        Assert.Equal(PrefetchTranslationDispatchResult.Cached, result);
        Assert.Equal(new SourceClientLanguage("chs", "zh-CN"), capturedSource);
        Assert.Equal(
            new TranslationReuseScope("chs", "pt-BR", 4, true),
            capturedScope);
        Assert.EndsWith(ExpectedBrokerScope, capturedBrokerKey);
        AssertPersistedRows(family, persistedRows, capturedScope);
        AssertLiveScopeMutated(configuration, liveSource);
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

        void Persist(object row)
        {
            persistedRows.Add(row);
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
    [InlineData(PrefetchFamily.NamePlate)]
    public void PrefetchEntry_UnknownSource_DoesNotReachBroker(
        PrefetchFamily family)
    {
        var configuration = CreateConfiguration();
        var brokerLookupCalls = 0;
        var brokerQueueCalls = 0;
        var translatorCalls = 0;
        var persistenceCalls = 0;

        var result = RunOperation(
            family,
            () => new SourceClientLanguage("unknown", "unknown"),
            configuration,
            TryGet,
            (_, _, _) =>
            {
                brokerQueueCalls++;
                return false;
            },
            _ => persistenceCalls++,
            out _,
            out _,
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

    private static PrefetchTranslationDispatchResult RunOperation(
        PrefetchFamily family,
        Func<SourceClientLanguage?> sourceLanguageResolver,
        Config configuration,
        TryGetPrefetchTranslationDelegate tryGetTranslation,
        QueuePrefetchTranslationDelegate queueTranslation,
        Action<object> persistRow,
        out SourceClientLanguage capturedSourceLanguage,
        out TranslationReuseScope capturedScope,
        Action? onTranslate = null)
    {
        return family switch
        {
            PrefetchFamily.ActionDetail =>
                PluginEntry.RunActionDetailPrefetchOperationEntry(
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
                    row => persistRow(row),
                    out capturedSourceLanguage,
                    out capturedScope,
                    out _),
            PrefetchFamily.ReferenceText =>
                PluginEntry.RunReferenceTextPrefetchOperationEntry(
                    "MainCommandPrefetch",
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
                    row => persistRow(row),
                    out capturedSourceLanguage,
                    out capturedScope,
                    out _),
            PrefetchFamily.AcceptedQuest =>
                PluginEntry.RunAcceptedQuestPrefetchOperationEntry(
                    CreateQuestCanonicalData(),
                    sourceLanguageResolver,
                    configuration,
                    tryGetTranslation,
                    queueTranslation,
                    Translate,
                    _ => null,
                    _ => null,
                    row => persistRow(row),
                    (row, _) => persistRow(row),
                    out capturedSourceLanguage,
                    out capturedScope,
                    out _),
            PrefetchFamily.NamePlate =>
                PluginEntry.RunNamePlatePrefetchOperationEntry(
                    new NamePlatePrefetchCandidate(
                        NamePlateKind.EventObject,
                        "Original nameplate"),
                    sourceLanguageResolver,
                    configuration,
                    tryGetTranslation,
                    queueTranslation,
                    Translate,
                    row => persistRow(row),
                    out capturedSourceLanguage,
                    out capturedScope),
            _ => throw new ArgumentOutOfRangeException(
                nameof(family),
                family,
                null),
        };

        string Translate(
            string sourceText,
            SourceClientLanguage sourceLanguage,
            string targetLanguage,
            string originContext)
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

    /// <summary>
    ///     Asserts that all mutable live scope inputs changed after capture.
    /// </summary>
    /// <param name="configuration">The mutated live configuration.</param>
    /// <param name="liveSource">The mutated live source language.</param>
    private static void AssertLiveScopeMutated(
        Config configuration,
        SourceClientLanguage liveSource)
    {
        Assert.Equal("cht", liveSource.PersistenceCode);
        Assert.Equal(42, configuration.Lang);
        Assert.Equal(8, configuration.ChosenTransEngine);
        Assert.False(configuration.TranslateAlreadyTranslatedTexts);
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

    private static void AssertCapturedRowScope(
        object persistedRow,
        TranslationReuseScope expectedScope)
    {
        var (sourceLanguage, targetLanguage, translationEngine) =
            persistedRow switch
        {
            ActionTooltip row => (
                row.OriginalLang ?? string.Empty,
                row.TranslationLang ?? string.Empty,
                row.TranslationEngine),
            ReferenceTextRowBase row => (
                row.OriginalLang ?? string.Empty,
                row.TranslationLang ?? string.Empty,
                row.TranslationEngine),
            QuestPlate row => (
                row.OriginalLang ?? string.Empty,
                row.TranslationLang ?? string.Empty,
                row.TranslationEngine),
            NamePlateMessage row => (
                row.OriginalLang ?? string.Empty,
                row.TranslationLang ?? string.Empty,
                row.TranslationEngine),
            _ => throw new ArgumentOutOfRangeException(
                nameof(persistedRow),
                persistedRow,
                null),
        };

        Assert.Equal(expectedScope.SourceLanguageCode, sourceLanguage);
        Assert.Equal(expectedScope.TargetLanguageCode, targetLanguage);
        Assert.Equal(expectedScope.TranslationEngine, translationEngine);
    }

    /// <summary>
    ///     Asserts canonical and translated-name rows share the captured scope.
    /// </summary>
    /// <param name="persistedRows">The rows sent to production persistence.</param>
    private static void AssertCanonicalAndNameRows(
        IReadOnlyCollection<object> persistedRows,
        TranslationReuseScope expectedScope)
    {
        Assert.Equal(2, persistedRows.Count);
        Assert.All(
            persistedRows,
            row => AssertCapturedRowScope(row, expectedScope));
        Assert.Contains(persistedRows, row =>
            string.IsNullOrWhiteSpace(GetTranslatedName(row)));
        Assert.Contains(persistedRows, row =>
            !string.IsNullOrWhiteSpace(GetTranslatedName(row)));
    }

    /// <summary>
    ///     Asserts the persistence shape and captured scope for a prefetch
    ///     family.
    /// </summary>
    /// <param name="family">The prefetch family under test.</param>
    /// <param name="persistedRows">The rows sent to production persistence.</param>
    /// <param name="expectedScope">The expected captured scope.</param>
    private static void AssertPersistedRows(
        PrefetchFamily family,
        IReadOnlyCollection<object> persistedRows,
        TranslationReuseScope expectedScope)
    {
        if (family == PrefetchFamily.NamePlate)
        {
            var row = Assert.IsType<NamePlateMessage>(
                Assert.Single(persistedRows));

            AssertCapturedRowScope(row, expectedScope);
            Assert.False(string.IsNullOrWhiteSpace(row.TranslatedNamePlateText));
            return;
        }

        AssertCanonicalAndNameRows(persistedRows, expectedScope);
    }

    /// <summary>
    ///     Gets the expected persistence callback count for one prefetch family.
    /// </summary>
    /// <param name="family">The prefetch family under test.</param>
    /// <returns>The expected persistence callback count.</returns>
    private static int GetExpectedPersistedRowCount(PrefetchFamily family)
    {
        return family == PrefetchFamily.NamePlate ? 1 : 2;
    }

    /// <summary>
    ///     Gets the family-specific translated name from one persisted row.
    /// </summary>
    /// <param name="persistedRow">The persisted production row.</param>
    /// <returns>The translated name.</returns>
    private static string? GetTranslatedName(object persistedRow)
    {
        return persistedRow switch
        {
            ActionTooltip row => row.TranslatedActionName,
            ReferenceTextRowBase row => row.TranslatedName,
            QuestPlate row => row.TranslatedQuestName,
            NamePlateMessage row => row.TranslatedNamePlateText,
            _ => throw new ArgumentOutOfRangeException(
                nameof(persistedRow),
                persistedRow,
                null),
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

        /// <summary>
        ///     NamePlateGui world-object name prefetch.
        /// </summary>
        NamePlate,
    }
}
