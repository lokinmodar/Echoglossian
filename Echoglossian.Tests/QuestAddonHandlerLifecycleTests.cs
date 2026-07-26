// <copyright file="QuestAddonHandlerLifecycleTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;
using System.IO;

using Dalamud.Game.Addon.Lifecycle;

using Echoglossian.NativeUI.AddonHandlers.Quest;
using Echoglossian.NativeUI.Handlers;

using Xunit;

using PluginEntry = Echoglossian.Echoglossian;

namespace Echoglossian.Tests;

/// <summary>
/// Covers lifecycle expectations for standalone quest addon handlers.
/// </summary>
public class QuestAddonHandlerLifecycleTests
{
    /// <summary>
    /// Ensures Journal participates in plugin-unload resets so visible quest
    /// text can be restored when the translation runtime changes.
    /// </summary>
    [Fact]
    public void JournalHandler_ImplementsPluginUnloadAwareContract()
    {
        Assert.True(
            typeof(IPluginUnloadAwareAddonHandler).IsAssignableFrom(
                typeof(JournalHandler)));
        Assert.Equal(
            typeof(JournalHandler),
            typeof(JournalHandler).GetMethod(
                nameof(IPluginUnloadAwareAddonHandler.OnPluginUnload),
                BindingFlags.Instance | BindingFlags.Public)?.DeclaringType);
    }

    /// <summary>
    /// Ensures JournalDetail participates in plugin-unload resets so visible
    /// detail text can be restored when the translation runtime changes.
    /// </summary>
    [Fact]
    public void JournalDetailHandler_ImplementsPluginUnloadAwareContract()
    {
        Assert.True(
            typeof(IPluginUnloadAwareAddonHandler).IsAssignableFrom(
                typeof(JournalDetailHandler)));
        Assert.Equal(
            typeof(JournalDetailHandler),
            typeof(JournalDetailHandler).GetMethod(
                nameof(IPluginUnloadAwareAddonHandler.OnPluginUnload),
                BindingFlags.Instance | BindingFlags.Public)?.DeclaringType);
    }

    /// <summary>
    /// Ensures Journal registers a pre-draw retry path so delayed quest
    /// translations can still surface without a new addon update event.
    /// </summary>
    [Fact]
    public void JournalHandler_RegistersPreDrawRefresh()
    {
        var handler = new JournalHandler(CreateDependencies());

        Assert.Contains(AddonEvent.PreDraw, handler.GetEventHandlers().Keys);
    }

    /// <summary>
    /// Ensures JournalDetail registers a pre-draw retry path so delayed quest
    /// translations can still surface without a new addon update event.
    /// </summary>
    [Fact]
    public void JournalDetailHandler_RegistersPreDrawRefresh()
    {
        var handler = new JournalDetailHandler(CreateDependencies());

        Assert.Contains(AddonEvent.PreDraw, handler.GetEventHandlers().Keys);
    }

    /// <summary>
    /// Ensures JournalAccept refreshes tooltip targets after setup so queued
    /// translations can surface while the accept dialog remains open.
    /// </summary>
    [Fact]
    public void JournalAcceptHandler_RegistersPreDrawRefresh()
    {
        var handler = new JournalAcceptHandler(CreateDependencies());

        Assert.Contains(AddonEvent.PreDraw, handler.GetEventHandlers().Keys);
    }

    /// <summary>
    /// Ensures JournalResult refreshes tooltip targets after setup so queued
    /// translations can surface while the result dialog remains open.
    /// </summary>
    [Fact]
    public void JournalResultHandler_RegistersPreDrawRefresh()
    {
        var handler = new JournalResultHandler(CreateDependencies());

        Assert.Contains(AddonEvent.PreDraw, handler.GetEventHandlers().Keys);
    }

    /// <summary>
    /// Ensures RecommendList captures refresh payloads so all display modes can
    /// apply translated text after the addon repaints.
    /// </summary>
    [Fact]
    public void RecommendListHandler_RegistersPreRefreshCapture()
    {
        var handler = new RecommendListHandler(CreateDependencies());

        Assert.Contains(AddonEvent.PreRefresh, handler.GetEventHandlers().Keys);
    }

    /// <summary>
    ///     Ensures ToDoList requests the shared accepted-quest prefetch when
    ///     a visible quest has not yet been persisted with every required
    ///     translation.
    /// </summary>
    [Fact]
    public void ToDoListHandler_RequestsAcceptedQuestPrefetchForPendingTranslations()
    {
        var resolver = typeof(ToDoListHandler).GetMethod(
            "TryResolveVisibleQuestEntries",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var request = typeof(QuestAddonHandlerBase).GetMethod(
            "RequestAcceptedQuestPrefetch",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(resolver);
        Assert.NotNull(request);
        Assert.True(
            MethodReferences(resolver!, request!),
            "ToDoList must request the shared accepted-quest prefetch instead of owning a translation queue.");
    }

    /// <summary>
    ///     Ensures ToDoList can still recover an accepted quest id from the
    ///     visible quest title when the live todo-progress snapshot has not
    ///     loaded yet.
    /// </summary>
    [Fact]
    public void ToDoListHandler_RecoversAcceptedQuestIdWhenTodoProgressSnapshotIsUnavailable()
    {
        var resolver = typeof(ToDoListHandler).GetMethod(
            "TryResolveVisibleQuestEntries",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var questIdResolver = typeof(QuestLuminaResolver).GetMethod(
            "TryResolveQuestId",
            BindingFlags.Static | BindingFlags.Public,
            binder: null,
            [typeof(string), typeof(string).MakeByRefType()],
            modifiers: null);
        var acceptedGate = typeof(QuestProgressResolver).GetMethod(
            "TryResolveAcceptedQuestId",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            [typeof(string), typeof(uint).MakeByRefType()],
            modifiers: null);

        Assert.NotNull(resolver);
        Assert.NotNull(questIdResolver);
        Assert.NotNull(acceptedGate);
        Assert.True(
            MethodReferences(resolver!, questIdResolver!),
            "ToDoList must recover the quest id from the visible title when the live todo-progress snapshot is still unavailable.");
        Assert.True(
            MethodReferences(resolver!, acceptedGate!),
            "ToDoList must gate fallback prefetches through accepted-quest state.");
    }

    /// <summary>
    ///     Ensures JournalDetail sends unresolved active quest text to the
    ///     shared accepted-quest prefetch instead of waiting for an unrelated
    ///     refresh to populate the persisted canonical row.
    /// </summary>
    [Fact]
    public void JournalDetailHandler_RequestsAcceptedQuestPrefetchForPendingTranslations()
    {
        var resolver = typeof(JournalDetailHandler).GetMethod(
            "TranslateJournalBox",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var request = typeof(QuestAddonHandlerBase).GetMethod(
            "RequestAcceptedQuestPrefetch",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(resolver);
        Assert.NotNull(request);
        Assert.True(
            MethodReferences(resolver!, request!),
            "JournalDetail must request the shared accepted-quest prefetch instead of owning a translation queue.");
    }

    /// <summary>
    ///     Ensures JournalDetail can still recover an accepted quest id from
    ///     the visible title when progression data is not yet available.
    /// </summary>
    [Fact]
    public void JournalDetailHandler_RecoversAcceptedQuestIdWhenProgressSnapshotIsUnavailable()
    {
        var resolver = typeof(JournalDetailHandler).GetMethod(
            "TranslateJournalBox",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var questIdResolver = typeof(QuestLuminaResolver).GetMethod(
            "TryResolveQuestId",
            BindingFlags.Static | BindingFlags.Public,
            binder: null,
            [typeof(string), typeof(string).MakeByRefType()],
            modifiers: null);
        var acceptedGate = typeof(QuestProgressResolver).GetMethod(
            "TryResolveAcceptedQuestId",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            [typeof(string), typeof(uint).MakeByRefType()],
            modifiers: null);

        Assert.NotNull(resolver);
        Assert.NotNull(questIdResolver);
        Assert.NotNull(acceptedGate);
        Assert.True(
            MethodReferences(resolver!, questIdResolver!),
            "JournalDetail must recover the quest id from the visible title when the progress snapshot is still unavailable.");
        Assert.True(
            MethodReferences(resolver!, acceptedGate!),
            "JournalDetail must gate fallback prefetches through accepted-quest state.");
    }

    /// <summary>
    ///     Ensures JournalDetail refresh events continue to resolve the
    ///     current source language and translate immediately instead of
    ///     routing through a separate visible-signature invalidation layer.
    /// </summary>
    [Fact]
    public void JournalDetailHandler_TranslatesDirectlyFromEventRefresh()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "Quest",
            "JournalDetailHandler.cs"));
        var eventHandlerStart = source.IndexOf(
            "private unsafe void OnJournalDetailEvent(",
            StringComparison.Ordinal);
        var preDrawHandlerStart = source.IndexOf(
            "private unsafe void OnJournalDetailPreDrawEvent(",
            eventHandlerStart,
            StringComparison.Ordinal);

        Assert.True(
            eventHandlerStart >= 0,
            "JournalDetail should keep its direct event refresh path in OnJournalDetailEvent.");
        Assert.True(
            preDrawHandlerStart > eventHandlerStart,
            "JournalDetail pre-draw retry handling should remain separate from the direct event refresh path.");

        var methodBody = source.Substring(
            eventHandlerStart,
            preDrawHandlerStart - eventHandlerStart);

        Assert.Contains(
            "RuntimeLanguageHelper.TryResolveCurrentSourceLanguage",
            methodBody,
            StringComparison.Ordinal);
        Assert.Contains(
            "this.TranslateJournalDetail(sourceLanguage);",
            methodBody,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "TryComputeVisibleJournalDetailSignature",
            methodBody,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the current-quest JournalDetail pane only enters the active
    ///     quest translation path after the visible title is gated through the
    ///     accepted-quest runtime.
    /// </summary>
    [Fact]
    public void JournalDetailHandler_GatesCurrentQuestPaneToAcceptedQuestStateBeforeDbLookup()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "Quest",
            "JournalDetailHandler.cs"));
        var translateJournalBoxStart = source.IndexOf(
            "private unsafe bool TranslateJournalBox(",
            StringComparison.Ordinal);
        var acceptedGate = source.IndexOf(
            "QuestProgressResolver.TryResolveAcceptedQuestId",
            translateJournalBoxStart,
            StringComparison.Ordinal);
        var dbLookup = source.IndexOf(
            "var foundQuestPlate = this.FindQuestPlate(questPlate);",
            translateJournalBoxStart,
            StringComparison.Ordinal);

        Assert.True(
            translateJournalBoxStart >= 0,
            "JournalDetail current-quest translation path should remain in TranslateJournalBox.");
        Assert.True(
            dbLookup > translateJournalBoxStart,
            "JournalDetail current-quest translation path should keep its canonical DB lookup inside TranslateJournalBox.");
        Assert.True(
            acceptedGate > translateJournalBoxStart && acceptedGate < dbLookup,
            "JournalDetail current-quest translation must gate the visible title through accepted-quest state before reusing DB rows or scheduling prefetch.");
    }

    /// <summary>
    ///     Ensures Journal sends unresolved list titles to the shared
    ///     accepted-quest prefetch rather than waiting for an unrelated
    ///     refresh to populate the persisted canonical row.
    /// </summary>
    [Fact]
    public void JournalHandler_RequestsAcceptedQuestPrefetchForPendingTranslations()
    {
        var resolver = typeof(JournalHandler).GetMethod(
            "TranslateJournalQuests",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var request = typeof(QuestAddonHandlerBase).GetMethod(
            "RequestAcceptedQuestPrefetch",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(resolver);
        Assert.NotNull(request);
        Assert.True(
            MethodReferences(resolver!, request!),
            "Journal must request the shared accepted-quest prefetch instead of owning a translation queue.");
    }

    /// <summary>
    ///     Ensures Journal list titles are gated through the accepted-quest
    ///     runtime before the handler treats a visible row as retryable.
    /// </summary>
    [Fact]
    public void JournalHandler_GatesVisibleTitlesToAcceptedQuestState()
    {
        var resolver = typeof(JournalHandler).GetMethod(
            "TryResolveJournalQuestPlate",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var acceptedGate = typeof(QuestProgressResolver).GetMethod(
            "TryResolveAcceptedQuestId",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            [typeof(string), typeof(uint).MakeByRefType()],
            modifiers: null);

        Assert.NotNull(resolver);
        Assert.NotNull(acceptedGate);
        Assert.True(
            MethodReferences(resolver!, acceptedGate!),
            "Journal list rows must resolve through accepted-quest state before they are treated as pending.");
    }

    /// <summary>
    ///     Ensures ScenarioTree sends unresolved visible quest slots to the
    ///     shared accepted-quest prefetch instead of owning a separate
    ///     translation queue for quest-family data.
    /// </summary>
    [Fact]
    public void ScenarioTreeHandler_RequestsAcceptedQuestPrefetchForPendingTranslations()
    {
        var resolver = typeof(ScenarioTreeHandler).GetMethod(
            "TryResolveVisibleScenarioTreeEntry",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var request = typeof(QuestAddonHandlerBase).GetMethod(
            "RequestAcceptedQuestPrefetch",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(resolver);
        Assert.NotNull(request);
        Assert.True(
            MethodReferences(resolver!, request!),
            "ScenarioTree must request the shared accepted-quest prefetch instead of owning a translation queue.");
    }

    /// <summary>
    ///     Ensures recycled Journal list nodes do not retain the previous
    ///     row's source or translated title.
    /// </summary>
    [Fact]
    public void JournalListNodeSnapshot_RejectsARecycledNodeText()
    {
        var matcher = typeof(JournalHandler).GetMethod(
            "MatchesJournalListNodeSnapshot",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotNull(matcher);
        Assert.False((bool)matcher!.Invoke(
            null,
            ["Different quest", "Previous quest", "Quest anterior"])!);
        Assert.True((bool)matcher.Invoke(
            null,
            ["Previous quest", "Previous quest", "Quest anterior"])!);
        Assert.True((bool)matcher.Invoke(
            null,
            ["Quest anterior", "Previous quest", "Quest anterior"])!);
    }

    /// <summary>
    ///     Ensures JournalDetail native output is atomic: pending sections
    ///     restore the source snapshot rather than mixing source and translated
    ///     text in the same detail pane.
    /// </summary>
    /// <param name="writesNativeTranslation">
    ///     Whether the active display mode writes native translations.
    /// </param>
    /// <param name="nativeTranslationReady">
    ///     Whether every visible native JournalDetail section is translated.
    /// </param>
    /// <param name="ownsNativeMutation">
    ///     Whether this handler previously wrote the visible native state.
    /// </param>
    /// <param name="expectedAction">The required native mutation action.</param>
    [Theory]
    [InlineData(
        true,
        true,
        false,
        nameof(JournalDetailNativeMutationAction.ApplyTranslation))]
    [InlineData(
        true,
        false,
        true,
        nameof(JournalDetailNativeMutationAction.RestoreOriginal))]
    [InlineData(
        true,
        false,
        false,
        nameof(JournalDetailNativeMutationAction.None))]
    [InlineData(
        false,
        false,
        true,
        nameof(JournalDetailNativeMutationAction.RestoreOriginal))]
    [InlineData(
        false,
        false,
        false,
        nameof(JournalDetailNativeMutationAction.None))]
    public void ResolveNativeMutationAction_AvoidsMixedNativeQuestText(
        bool writesNativeTranslation,
        bool nativeTranslationReady,
        bool ownsNativeMutation,
        string expectedAction)
    {
        Assert.Equal(
            Enum.Parse<JournalDetailNativeMutationAction>(expectedAction),
            JournalDetailHandler.ResolveNativeMutationAction(
                writesNativeTranslation,
                nativeTranslationReady,
                ownsNativeMutation));
    }

    /// <summary>
    ///     Ensures a detail pane with visible supplemental summary text is not
    ///     considered native-ready when no canonical summary rows can translate
    ///     that text.
    /// </summary>
    /// <param name="primarySummaryReady">
    ///     Whether the primary summary is translated.
    /// </param>
    /// <param name="additionalCanonicalSummariesReady">
    ///     Whether every additional canonical summary is translated.
    /// </param>
    /// <param name="canonicalSummaryCount">
    ///     The number of canonical summary rows that back the visible pane.
    /// </param>
    /// <param name="hasVisibleAdditionalSummaryText">
    ///     Whether the pane has supplemental source summary text.
    /// </param>
    /// <param name="expected">The expected native-summary readiness.</param>
    [Theory]
    [InlineData(true, true, 0, true, false)]
    [InlineData(true, true, 0, false, true)]
    [InlineData(true, true, 2, true, true)]
    [InlineData(false, true, 2, false, false)]
    public void IsNativeSummaryTranslationReady_RequiresCoverageOfVisibleText(
        bool primarySummaryReady,
        bool additionalCanonicalSummariesReady,
        int canonicalSummaryCount,
        bool hasVisibleAdditionalSummaryText,
        bool expected)
    {
        Assert.Equal(
            expected,
            JournalDetailHandler.IsNativeSummaryTranslationReady(
                primarySummaryReady,
                additionalCanonicalSummariesReady,
                canonicalSummaryCount,
                hasVisibleAdditionalSummaryText));
    }

    /// <summary>
    ///     Ensures supplemental JournalDetail summary rows are identified by
    ///     their sibling component template rather than the text-node width.
    ///     The game leaves those supplemental text nodes at width zero.
    /// </summary>
    /// <param name="summaryContainerX">The primary summary container x coordinate.</param>
    /// <param name="summaryContainerWidth">The primary summary container width.</param>
    /// <param name="summaryTextX">The primary summary text x coordinate.</param>
    /// <param name="summaryTextY">The primary summary text y coordinate.</param>
    /// <param name="candidateContainerX">The supplemental container x coordinate.</param>
    /// <param name="candidateContainerWidth">The supplemental container width.</param>
    /// <param name="candidateTextX">The supplemental text x coordinate.</param>
    /// <param name="candidateTextY">The supplemental text y coordinate.</param>
    /// <param name="expected">The expected template match result.</param>
    [Theory]
    [InlineData(0f, 390f, 21f, 6f, 0f, 390f, 21f, 6f, true)]
    [InlineData(0f, 390f, 21f, 6f, 2f, 390f, 21f, 6f, false)]
    [InlineData(0f, 390f, 21f, 6f, 0f, 350f, 21f, 6f, false)]
    [InlineData(0f, 390f, 21f, 6f, 0f, 390f, 21f, 8f, false)]
    public void IsSupplementalSummaryNodeLayout_UsesSiblingTemplateInsteadOfTextWidth(
        float summaryContainerX,
        float summaryContainerWidth,
        float summaryTextX,
        float summaryTextY,
        float candidateContainerX,
        float candidateContainerWidth,
        float candidateTextX,
        float candidateTextY,
        bool expected)
    {
        Assert.Equal(
            expected,
            JournalDetailHandler.IsSupplementalSummaryNodeLayout(
                summaryContainerX,
                summaryContainerWidth,
                summaryTextX,
                summaryTextY,
                candidateContainerX,
                candidateContainerWidth,
                candidateTextX,
                candidateTextY));
    }

    /// <summary>
    /// Ensures JournalAccept tooltips wait until both title and body
    /// translations are available.
    /// </summary>
    /// <param name="translatedQuestName">The translated quest title.</param>
    /// <param name="translatedQuestMessage">The translated quest body.</param>
    /// <param name="expected">The expected readiness value.</param>
    [Theory]
    [InlineData("Titulo traduzido", "Corpo traduzido", true)]
    [InlineData("", "Corpo traduzido", false)]
    [InlineData("Titulo traduzido", "", false)]
    public void JournalAcceptTooltipReadiness_RequiresTranslatedNameAndMessage(
        string translatedQuestName,
        string translatedQuestMessage,
        bool expected)
    {
        Assert.Equal(
            expected,
            JournalAcceptHandler.IsTranslatedPayloadReady(
                translatedQuestName,
                translatedQuestMessage));
    }

    /// <summary>
    /// Ensures JournalResult tooltips wait until the translated title exists.
    /// </summary>
    /// <param name="translatedQuestName">The translated quest title.</param>
    /// <param name="expected">The expected readiness value.</param>
    [Theory]
    [InlineData("Titulo traduzido", true)]
    [InlineData("", false)]
    public void JournalResultTooltipReadiness_RequiresTranslatedName(
        string translatedQuestName,
        bool expected)
    {
        Assert.Equal(
            expected,
            JournalResultHandler.IsTranslatedPayloadReady(translatedQuestName));
    }

    /// <summary>
    /// Ensures RecommendList tooltips wait until translated title text exists.
    /// </summary>
    /// <param name="translatedQuestName">The translated quest title.</param>
    /// <param name="expected">The expected readiness value.</param>
    [Theory]
    [InlineData("Titulo traduzido", true)]
    [InlineData("", false)]
    public void RecommendListTooltipReadiness_RequiresTranslatedName(
        string translatedQuestName,
        bool expected)
    {
        Assert.Equal(
            expected,
            RecommendListHandler.IsTranslatedPayloadReady(translatedQuestName));
    }

    /// <summary>
    /// Ensures ScenarioTree tooltips wait until translated slot text exists.
    /// </summary>
    /// <param name="translatedQuestText">The translated ScenarioTree text.</param>
    /// <param name="expected">The expected readiness value.</param>
    [Theory]
    [InlineData("Texto traduzido", true)]
    [InlineData("", false)]
    public void ScenarioTreeTooltipReadiness_RequiresTranslatedText(
        string translatedQuestText,
        bool expected)
    {
        Assert.Equal(
            expected,
            ScenarioTreeHandler.IsTranslatedPayloadReady(translatedQuestText));
    }

    /// <summary>
    /// Ensures AreaMap tooltips wait until translated quest text exists.
    /// </summary>
    /// <param name="translatedQuestText">The translated AreaMap quest text.</param>
    /// <param name="expected">The expected readiness value.</param>
    [Theory]
    [InlineData("Texto traduzido", true)]
    [InlineData("", false)]
    public void AreaMapTooltipReadiness_RequiresTranslatedText(
        string translatedQuestText,
        bool expected)
    {
        Assert.Equal(
            expected,
            AreaMapHandler.IsTranslatedPayloadReady(translatedQuestText));
    }

    /// <summary>
    ///     Ensures every remaining quest-family handler is reachable through
    ///     runtime wiring, otherwise the addon-specific apply path never runs.
    /// </summary>
    /// <param name="handlerType">The quest handler type that must be wired.</param>
    [Theory]
    [InlineData(typeof(JournalAcceptHandler))]
    [InlineData(typeof(JournalResultHandler))]
    [InlineData(typeof(RecommendListHandler))]
    public void RuntimeWiring_RegistersRemainingQuestFamilyHandlers(
        Type handlerType)
    {
        var wiringMethod = typeof(PluginEntry).GetMethod(
            "EgloAddonHandler",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var handlerConstructor = handlerType.GetConstructor(
            [typeof(QuestAddonHandlerDependencies)]);

        Assert.NotNull(wiringMethod);
        Assert.NotNull(handlerConstructor);
        Assert.True(
            MethodReferences(wiringMethod!, handlerConstructor!),
            $"{handlerType.Name} must be constructed by EgloAddonHandler.");
    }

    /// <summary>
    ///     Ensures AreaMap uses the map-surface handler without also wiring
    ///     the legacy single-quest-row handler on the same addon.
    /// </summary>
    [Fact]
    public void RuntimeWiring_AreaMap_UsesOnlyMapSurfaceHandler()
    {
        var wiringMethod = typeof(PluginEntry).GetMethod(
            "EgloAddonHandler",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var mapSurfaceConstructor = typeof(MapSurfaceStringArrayHandler)
            .GetConstructor(
                [typeof(string), typeof(QuestAddonHandlerDependencies)]);
        var legacyConstructor = typeof(AreaMapHandler).GetConstructor(
            [typeof(QuestAddonHandlerDependencies)]);

        Assert.NotNull(wiringMethod);
        Assert.NotNull(mapSurfaceConstructor);
        Assert.NotNull(legacyConstructor);
        Assert.True(
            MethodReferences(wiringMethod!, mapSurfaceConstructor!),
            "AreaMap must be constructed through MapSurfaceStringArrayHandler.");
        Assert.False(
            MethodReferences(wiringMethod!, legacyConstructor!),
            "AreaMapHandler must not be constructed by EgloAddonHandler.");
    }

    /// <summary>
    /// Creates the minimal dependency bundle required to construct quest
    /// handlers for lifecycle tests.
    /// </summary>
    /// <returns>The quest-handler dependencies.</returns>
    private static QuestAddonHandlerDependencies CreateDependencies()
    {
        return new QuestAddonHandlerDependencies
        {
            Config = new Config(),
            TranslationService = null!,
            FindQuestPlate = _ => null,
            FindQuestPlateByName = _ => null,
            InsertQuestPlate = _ => string.Empty,
            UpdateQuestPlate = _ => string.Empty,
            UpdateQuestPlateGameVersion = (_, _) => { },
            NormalizeText = text => text,
            DisableTranslationAccordingToState = static () => false,
            TryGetQueuedTranslation = static (string _, out string translatedText) =>
            {
                translatedText = string.Empty;
                return false;
            },
            QueueTranslation = static (_, _, _) => false,
            QueueTranslationBatch = static (_, _, _, _) => false,
            RequestAcceptedQuestPrefetch = static (_, _) => { },
            RemoveHoverTooltipByPrefix = static _ => { },
            RegisterTranslatedHoverTooltipAddon = null!,
            RegisterTranslatedHoverTooltipTextNode = null!,
            RegisterTranslatedHoverTooltipResNode = null!,
            RegisterTranslatedHoverTooltipBounds = static (
                _,
                _,
                _,
                _,
                _,
                _,
                _,
                _) => { },
        };
    }

    /// <summary>
    /// Finds the repository root from the test output directory.
    /// </summary>
    /// <returns>The repository root directory.</returns>
    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Echoglossian.sln")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root.");
    }

    /// <summary>
    /// Determines whether one compiled method body references another member.
    /// </summary>
    /// <param name="method">The method body to inspect.</param>
    /// <param name="referencedMember">The expected referenced member.</param>
    /// <returns>True when the metadata token appears in the method body.</returns>
    private static bool MethodReferences(
        MethodInfo method,
        MemberInfo referencedMember)
    {
        var methodBody = method.GetMethodBody()?.GetILAsByteArray();
        if (methodBody == null)
        {
            return false;
        }

        var referencedToken = BitConverter.GetBytes(
            referencedMember.MetadataToken);
        return methodBody.AsSpan().IndexOf(referencedToken) >= 0;
    }
}
