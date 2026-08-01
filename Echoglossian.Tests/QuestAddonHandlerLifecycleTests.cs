// <copyright file="QuestAddonHandlerLifecycleTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Plugin.Services;

using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite.Models.Journal;
using Echoglossian.LanguagesHandling;
using Echoglossian.NativeUI.AddonHandlers.Quest;
using Echoglossian.NativeUI.AddonHandlers.SelectionDialogs;
using Echoglossian.NativeUI.AddonHandlers.Toasts;
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
    ///     Ensures ToDo participates in plugin-unload resets and registers a
    ///     pre-draw refresh so persisted payloads can surface after an async
    ///     translation completes.
    /// </summary>
    [Fact]
    public void ToDoHandler_RegistersLifecycleRefreshAndUnloadCleanup()
    {
        var handlerType = typeof(JournalHandler).Assembly.GetType(
            "Echoglossian.NativeUI.AddonHandlers.Quest.ToDoHandler");

        Assert.NotNull(handlerType);
        Assert.True(
            typeof(IPluginUnloadAwareAddonHandler).IsAssignableFrom(
                handlerType!));
        Assert.Equal(
            handlerType,
            handlerType.GetMethod(
                nameof(IPluginUnloadAwareAddonHandler.OnPluginUnload),
                BindingFlags.Instance | BindingFlags.Public)?.DeclaringType);
    }

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
    /// Ensures Journal refresh events resolve the live source language so the
    /// visible list can be reapplied before draw after a list refresh.
    /// </summary>
    [Fact]
    public void JournalHandler_RefreshEvent_ReappliesVisibleListImmediately()
    {
        var refresh = typeof(JournalHandler).GetMethod(
            "OnJournalQuestEvent",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var sourceResolver = typeof(RuntimeLanguageHelper).GetMethod(
            nameof(RuntimeLanguageHelper.TryResolveCurrentSourceLanguage),
            BindingFlags.Static | BindingFlags.Public,
            binder: null,
            [typeof(SourceClientLanguage).MakeByRefType()],
            modifiers: null);

        Assert.NotNull(refresh);
        Assert.NotNull(sourceResolver);
        Assert.True(
            MethodReferences(refresh!, sourceResolver!),
            "Journal refresh events must resolve the live source language so list updates can reapply translations before draw.");
    }

    /// <summary>
    /// Ensures Journal hover registration uses explicit row bounds instead of
    /// the inflated text-node hitbox alone.
    /// </summary>
    [Fact]
    public void JournalHandler_UsesExplicitHoverBoundsForVisibleRows()
    {
        var translator = typeof(JournalHandler).GetMethod(
            "TranslateJournalQuests",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var hoverBoundsResolver = typeof(JournalHandler).GetMethod(
            "TryGetJournalHoverBounds",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(translator);
        Assert.NotNull(hoverBoundsResolver);
        Assert.True(
            MethodReferences(translator!, hoverBoundsResolver!),
            "Journal hover registration must resolve explicit row bounds before registering a tooltip target.");
    }

    /// <summary>
    /// Ensures Journal resolves visible rows through the accepted-quest
    /// snapshot before mutating the visible list.
    /// </summary>
    [Fact]
    public void JournalHandler_UsesAcceptedQuestSnapshotForVisibleRows()
    {
        var translator = typeof(JournalHandler).GetMethod(
            "TranslateJournalQuests",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var snapshotResolver = typeof(JournalHandler).GetMethod(
            "TryResolveAcceptedJournalQuestEntry",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(translator);
        Assert.NotNull(snapshotResolver);
        Assert.True(
            MethodReferences(translator!, snapshotResolver!),
            "Journal must reconcile visible rows through the accepted-quest snapshot instead of relying on recycled node history alone.");
    }

    /// <summary>
    /// Ensures Journal resolves the direct quest-title text node from the row
    /// shape instead of recursively searching for any reused node identifier.
    /// </summary>
    [Fact]
    public void JournalHandler_UsesDirectQuestTitleNodeResolver()
    {
        var translator = typeof(JournalHandler).GetMethod(
            "TranslateJournalQuests",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var titleResolver = typeof(JournalHandler).GetMethods(
                BindingFlags.Static | BindingFlags.NonPublic)
            .Single(method =>
            {
                if (method.Name != "TryGetJournalQuestTitleNode")
                {
                    return false;
                }

                var parameters = method.GetParameters();
                return parameters.Length == 2 &&
                       parameters[0].ParameterType.IsPointer &&
                       string.Equals(
                           parameters[0].ParameterType.GetElementType()?.Name,
                           "AtkResNode",
                           StringComparison.Ordinal);
            });

        Assert.NotNull(translator);
        Assert.NotNull(titleResolver);
        Assert.True(
            MethodReferences(translator!, titleResolver!),
            "Journal must resolve the direct quest title node from the row shape so hover and native translation do not bind to reused icon subnodes.");
    }

    /// <summary>
    /// Ensures one visible translated Journal title resolves back to the
    /// accepted quest entry that owns the original source title.
    /// </summary>
    [Fact]
    public void TryResolveAcceptedJournalQuestEntry_RecoversOriginalFromRenderedTranslatedTitle()
    {
        JournalHandler.AcceptedJournalQuestEntry[] entries =
        [
            new(
                70315,
                "Remembering the Past",
                "Recordando o passado",
                null),
            new(
                68799,
                "For Better or Worse",
                "Para o bem ou para o mal",
                null),
        ];

        Assert.True(
            JournalHandler.TryResolveAcceptedJournalQuestEntry(
                entries,
                "Para o bem ou para o mal",
                out var resolvedEntry));
        Assert.Equal((uint)68799, resolvedEntry.AcceptedQuestId);
        Assert.Equal(
            "For Better or Worse",
            resolvedEntry.OriginalQuestName);
    }

    /// <summary>
    /// Ensures one visible translated Journal title is rejected when multiple
    /// accepted quests would map to the same rendered text.
    /// </summary>
    [Fact]
    public void TryResolveAcceptedJournalQuestEntry_RejectsAmbiguousTranslatedTitle()
    {
        JournalHandler.AcceptedJournalQuestEntry[] entries =
        [
            new(
                1001,
                "Quest Alpha",
                "Titulo repetido",
                null),
            new(
                1002,
                "Quest Beta",
                "Titulo repetido",
                null),
        ];

        Assert.False(
            JournalHandler.TryResolveAcceptedJournalQuestEntry(
                entries,
                "Titulo repetido",
                out _));
    }

    /// <summary>
    /// Ensures Journal hover geometry stays aligned to the row bounds without
    /// vertically expanding into neighboring rows.
    /// </summary>
    [Fact]
    public void JournalHoverBounds_UseRowGeometryWithoutVerticalInflation()
    {
        var resolvedBounds = JournalHandler.ResolveJournalHoverBounds(
            100f,
            200f,
            680f,
            40f,
            120f,
            206f,
            220f,
            28f);

        Assert.Equal(new System.Numerics.Vector2(92f, 202f), resolvedBounds.TopLeft);
        Assert.Equal(new System.Numerics.Vector2(788f, 238f), resolvedBounds.BottomRight);
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
    ///     Ensures JournalAccept restores its owned native mutation when the
    ///     active display mode stops writing translated text.
    /// </summary>
    [Fact]
    public void JournalAcceptHandler_PreDrawRestoresOwnedNativeMutation()
    {
        var refresh = typeof(JournalAcceptHandler).GetMethod(
            "OnJournalAcceptPreDrawEvent",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var restore = typeof(JournalAcceptHandler).GetMethod(
            "RestoreJournalAcceptOriginals",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(refresh);
        Assert.NotNull(restore);
        Assert.True(
            MethodReferences(refresh!, restore!),
            "JournalAccept must restore its owned native text when the display mode switches away from native writes.");
    }

    /// <summary>
    ///     Ensures JournalAccept reads explicit quest identity only through the
    ///     dedicated popup identity helper.
    /// </summary>
    [Fact]
    public void JournalAcceptHandler_UsesPopupIdentityReader()
    {
        var handler = typeof(JournalAcceptHandler).GetMethod(
            "OnJournalAcceptEvent",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var identityReader = typeof(QuestPopupIdentity).GetMethod(
            "TryReadJournalAcceptQuestId",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(handler);
        Assert.NotNull(identityReader);
        Assert.True(
            MethodReferences(handler!, identityReader!),
            "JournalAccept must resolve quest identity through QuestPopupIdentity instead of inferring popup payload ownership inline.");
    }

    /// <summary>
    ///     Ensures JournalAccept can refresh pending translations from the
    ///     dedicated popup table when no safe canonical quest identity exists.
    /// </summary>
    [Fact]
    public void JournalAcceptHandler_UsesPopupPersistenceFallback()
    {
        var refresh = typeof(JournalAcceptHandler).GetMethod(
            "TryRefreshJournalAcceptPendingTranslation",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var popupLookup = typeof(QuestAddonHandlerBase).GetMethod(
            "FindQuestPopupText",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(refresh);
        Assert.NotNull(popupLookup);
        Assert.True(
            MethodReferences(refresh!, popupLookup!),
            "JournalAccept must check the dedicated popup table while pending translations settle.");
    }

    /// <summary>
    ///     Ensures JournalAccept cleanup restores any owned native mutation
    ///     before dropping runtime state.
    /// </summary>
    [Fact]
    public void JournalAcceptHandler_CleanupRestoresOwnedNativeMutation()
    {
        var cleanup = typeof(JournalAcceptHandler).GetMethod(
            "OnJournalAcceptCleanupEvent",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var restore = typeof(JournalAcceptHandler).GetMethod(
            "RestoreJournalAcceptOriginals",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(cleanup);
        Assert.NotNull(restore);
        Assert.True(
            MethodReferences(cleanup!, restore!),
            "JournalAccept cleanup must restore any handler-owned native mutation before state is cleared.");
    }

    /// <summary>
    ///     Ensures JournalAccept can still resolve the live body node through
    ///     the shared popup-section structural fallback when readable-node scans
    ///     miss the empty runtime body node.
    /// </summary>
    [Fact]
    public void JournalAcceptHandler_UsesSharedPopupSectionBodyFallback()
    {
        var resolver = typeof(JournalAcceptHandler).GetMethod(
            "TryFindJournalAcceptMessageNode",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var fallback = typeof(QuestAddonHandlerBase)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(method => string.Equals(
                                  method.Name,
                                  "TryFindPopupSectionBodyTextNodeByHeadingTextId",
                                  StringComparison.Ordinal) &&
                              method.GetParameters().Length == 3);
        var presentationResolver = typeof(JournalAcceptHandler).GetMethod(
            "TryResolveJournalAcceptPreferredHoverNode",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(resolver);
        Assert.NotNull(fallback);
        Assert.NotNull(presentationResolver);
        Assert.True(
            MethodReferences(resolver!, fallback!),
            "JournalAccept must fall back to the shared popup-section body resolver when the visible body node is structurally present but not readable.");
        Assert.False(
            MethodReferences(resolver!, presentationResolver!),
            "JournalAccept text-node resolution must not perform presentation-geometry traversal.");
    }

    /// <summary>
    ///     Ensures JournalAccept derives its body tooltip hitbox through the
    ///     shared popup-body geometry resolver.
    /// </summary>
    [Fact]
    public void JournalAcceptHandler_UsesSharedPopupBodyHoverBounds()
    {
        var register = typeof(JournalAcceptHandler).GetMethod(
            "RegisterJournalAcceptHoverTooltip",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var bounds = typeof(QuestAddonHandlerBase).GetMethod(
            "TryBuildPopupBodyHoverBounds",
            BindingFlags.Static | BindingFlags.NonPublic);
        var presentationResolver = typeof(JournalAcceptHandler).GetMethod(
            "TryResolveJournalAcceptPreferredHoverNode",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(register);
        Assert.NotNull(bounds);
        Assert.NotNull(presentationResolver);
        Assert.True(
            MethodReferences(register!, bounds!),
            "JournalAccept must register its body tooltip through shared popup-body hover bounds.");
        Assert.True(
            MethodReferences(register!, presentationResolver!),
            "JournalAccept presentation must resolve structural body geometry only while registering hover bounds.");
    }

    /// <summary>
    ///     Ensures popup-section text traversal uses the shared duplicate and
    ///     node-limit guard before following native child or sibling links.
    /// </summary>
    [Fact]
    public void QuestAddonHandlerBase_PopupSectionTextTraversalUsesBoundedNodeGuard()
    {
        var traversal = typeof(QuestAddonHandlerBase).GetMethod(
            "TryFindFirstVisibleTextNodeInSubtree",
            BindingFlags.Static | BindingFlags.NonPublic);
        var traversalGuard = typeof(AddonTextNodeResolvers)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(method => string.Equals(
                                  method.Name,
                                  "TryVisitNodeAddress",
                                  StringComparison.Ordinal) &&
                              method.GetParameters().Length == 3);

        Assert.NotNull(traversal);
        Assert.True(
            MethodReferences(traversal!, traversalGuard),
            "Popup-section text traversal must stop at duplicate addresses and its practical node limit.");
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
    ///     Ensures JournalResult restores its owned native mutation when the
    ///     active display mode stops writing translated text.
    /// </summary>
    [Fact]
    public void JournalResultHandler_PreDrawRestoresOwnedNativeMutation()
    {
        var refresh = typeof(JournalResultHandler).GetMethod(
            "OnJournalResultPreDrawEvent",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var restore = typeof(JournalResultHandler).GetMethod(
            "RestoreJournalResultOriginals",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(refresh);
        Assert.NotNull(restore);
        Assert.True(
            MethodReferences(refresh!, restore!),
            "JournalResult must restore its owned native text when the display mode switches away from native writes.");
    }

    /// <summary>
    ///     Ensures JournalResult reads explicit quest identity through the
    ///     dedicated popup identity helper.
    /// </summary>
    [Fact]
    public void JournalResultHandler_UsesQuestIdReader()
    {
        var handler = typeof(JournalResultHandler).GetMethod(
            "OnJournalResultEvent",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var reader = typeof(QuestPopupIdentity).GetMethod(
            "TryReadJournalResultQuestId",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(handler);
        Assert.NotNull(reader);
        Assert.True(
            MethodReferences(handler!, reader!),
            "JournalResult must resolve quest identity through QuestPopupIdentity before falling back to title-only lookup.");
    }

    /// <summary>
    ///     Ensures JournalResult can refresh pending translations from the
    ///     dedicated popup table after canonical lookup paths miss.
    /// </summary>
    [Fact]
    public void JournalResultHandler_UsesPopupPersistenceFallback()
    {
        var refresh = typeof(JournalResultHandler).GetMethod(
            "TryRefreshJournalResultPendingTranslation",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var popupLookup = typeof(QuestAddonHandlerBase).GetMethod(
            "FindQuestPopupText",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(refresh);
        Assert.NotNull(popupLookup);
        Assert.True(
            MethodReferences(refresh!, popupLookup!),
            "JournalResult must check the dedicated popup table when canonical lookup does not yield a translated title.");
    }

    /// <summary>
    ///     Ensures JournalResult falls back to the title lookup when its
    ///     canonical quest-id lookup does not find a row.
    /// </summary>
    [Fact]
    public void FindJournalResultQuestPlate_IdMiss_FallsBackToTitleLookup()
    {
        var lookup = typeof(JournalResultHandler).GetMethod(
            "FindJournalResultQuestPlate",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var calls = new List<string>();
        var knownRow = new QuestPlate(
            "Quest title",
            string.Empty,
            "en",
            "Canonical translation",
            string.Empty,
            "42",
            "pt-BR",
            0,
            DateTime.UtcNow,
            DateTime.UtcNow);
        var originalDataManager = PluginEntry.DManager;
        var originalLanguages = PluginEntry.LangDict;
        PluginEntry.DManager = CreateDataManager();
        PluginEntry.LangDict = new Dictionary<int, LanguageInfo>
        {
            [28] = new LanguageInfo(
                "pt-BR",
                "Brazilian Portuguese",
                string.Empty,
                string.Empty,
                []),
        };
        try
        {
            var handler = new JournalResultHandler(CreateDependencies(
                findQuestPlate: row =>
                {
                    calls.Add($"id:{row.QuestId}");
                    return null;
                },
                findQuestPlateByName: row =>
                {
                    calls.Add($"title:{row.QuestName}");
                    return knownRow;
                }));

            Assert.NotNull(lookup);
            var resolved = lookup!.Invoke(
                handler,
                [new SourceClientLanguage("en", "en"), "Quest title", "42"]);

            Assert.Same(knownRow, resolved);
            Assert.Equal(["id:42", "title:Quest title"], calls);
        }
        finally
        {
            PluginEntry.DManager = originalDataManager;
            PluginEntry.LangDict = originalLanguages;
        }
    }

    /// <summary>
    ///     Ensures JournalResult prefers a completed canonical title over a
    ///     stale title already applied to a UI node.
    /// </summary>
    [Fact]
    public void TryResolveJournalResultTranslation_CanonicalTitle_WinsOverAppliedCache()
    {
        var resolve = typeof(JournalResultHandler).GetMethod(
            "TryResolveJournalResultTranslation",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var canonicalRow = new QuestPlate(
            "Quest title",
            string.Empty,
            "en",
            "Canonical translation",
            string.Empty,
            "42",
            "pt-BR",
            0,
            DateTime.UtcNow,
            DateTime.UtcNow);
        var arguments = new object?[]
        {
            "journal-result-cache-key",
            "Quest title",
            canonicalRow,
            null,
            null,
        };

        QuestUiTranslationCache.Clear();
        try
        {
            QuestUiTranslationCache.Remember(
                "Quest title",
                "Previously applied translation");

            Assert.NotNull(resolve);
            var found = (bool)resolve!.Invoke(
                new JournalResultHandler(CreateDependencies()),
                arguments)!;

            Assert.True(found);
            Assert.Equal("Canonical translation", arguments[4]);
        }
        finally
        {
            QuestUiTranslationCache.Clear();
        }
    }

    /// <summary>
    ///     Ensures JournalResult cleanup restores any owned native mutation
    ///     before dropping runtime state.
    /// </summary>
    [Fact]
    public void JournalResultHandler_CleanupRestoresOwnedNativeMutation()
    {
        var cleanup = typeof(JournalResultHandler).GetMethod(
            "OnJournalResultCleanupEvent",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var restore = typeof(JournalResultHandler).GetMethod(
            "RestoreJournalResultOriginals",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(cleanup);
        Assert.NotNull(restore);
        Assert.True(
            MethodReferences(cleanup!, restore!),
            "JournalResult cleanup must restore any handler-owned native mutation before state is cleared.");
    }

    /// <summary>
    ///     Ensures JournalResult centralizes body-node lookup through a shared
    ///     structural fallback so hover, capture, native write, and restore all
    ///     target the same live body node.
    /// </summary>
    [Fact]
    public void JournalResultHandler_UsesSharedPopupSectionBodyFallback()
    {
        var resolver = typeof(JournalResultHandler).GetMethod(
            "TryFindJournalResultMessageNode",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var fallback = typeof(QuestAddonHandlerBase)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(method => string.Equals(
                                  method.Name,
                                  "TryFindPopupSectionBodyTextNodeByHeadingTextId",
                                  StringComparison.Ordinal) &&
                              method.GetParameters().Length == 3);
        var presentationResolver = typeof(JournalResultHandler).GetMethod(
            "TryResolveJournalResultPreferredHoverNode",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(resolver);
        Assert.NotNull(fallback);
        Assert.NotNull(presentationResolver);
        Assert.True(
            MethodReferences(resolver!, fallback!),
            "JournalResult must fall back to the shared popup-section body resolver when the visible body node is structurally present but not readable.");
        Assert.False(
            MethodReferences(resolver!, presentationResolver!),
            "JournalResult text-node resolution must not perform presentation-geometry traversal.");
    }

    /// <summary>
    ///     Ensures JournalResult derives its body tooltip hitbox through the
    ///     shared popup-body geometry resolver.
    /// </summary>
    [Fact]
    public void JournalResultHandler_UsesSharedPopupBodyHoverBounds()
    {
        var register = typeof(JournalResultHandler).GetMethod(
            "RegisterJournalResultHoverTooltip",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var bounds = typeof(QuestAddonHandlerBase).GetMethod(
            "TryBuildPopupBodyHoverBounds",
            BindingFlags.Static | BindingFlags.NonPublic);
        var presentationResolver = typeof(JournalResultHandler).GetMethod(
            "TryResolveJournalResultPreferredHoverNode",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(register);
        Assert.NotNull(bounds);
        Assert.NotNull(presentationResolver);
        Assert.True(
            MethodReferences(register!, bounds!),
            "JournalResult must register its body tooltip through shared popup-body hover bounds.");
        Assert.True(
            MethodReferences(register!, presentationResolver!),
            "JournalResult presentation must resolve structural body geometry only while registering hover bounds.");
    }

    /// <summary>
    ///     Ensures JournalResult keeps rich body capture while registering the
    ///     structural body hitbox through the explicit-bounds overload.
    /// </summary>
    [Fact]
    public void JournalResultHandler_UsesExplicitBoundsForStructuralBodyHover()
    {
        var register = typeof(JournalResultHandler).GetMethod(
            "RegisterJournalResultHoverTooltip",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var explicitBoundsRegistration = typeof(QuestAddonHandlerBase)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(method => string.Equals(
                                  method.Name,
                                  "RegisterTranslatedHoverTooltip",
                                  StringComparison.Ordinal) &&
                              method.GetParameters().Length == 9);

        Assert.NotNull(register);
        Assert.NotNull(explicitBoundsRegistration);
        Assert.True(
            MethodReferences(register!, explicitBoundsRegistration!),
            "JournalResult must use explicit bounds when a structural body candidate is available.");
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
    ///     Ensures ToDoList requested-update events can reuse the current
    ///     translated snapshot when only non-translatable rows such as timer
    ///     nodes repaint.
    /// </summary>
    [Fact]
    public void ToDoListHandler_RequestedUpdatesReuseCurrentPresentationForNonTranslatableRefreshes()
    {
        var requestedUpdateHandler = typeof(ToDoListHandler).GetMethod(
            "OnToDoListEvent",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var reuseMethod = typeof(ToDoListHandler).GetMethod(
            "TryReuseCurrentToDoPresentation",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(requestedUpdateHandler);
        Assert.NotNull(reuseMethod);
        Assert.True(
            MethodReferences(requestedUpdateHandler!, reuseMethod!),
            "ToDoList requested updates must reuse the current translated snapshot when only timer-driven repaint nodes changed.");
    }

    /// <summary>
    ///     Ensures ToDoList applies native text and hover tooltips only when a
    ///     runtime row actually has translated payload ready.
    /// </summary>
    [Fact]
    public void ToDoListHandler_UsesTranslatedPayloadReadyFlagForPresentation()
    {
        var applyPresentation = typeof(ToDoListHandler).GetMethod(
            "ApplyToDoListPresentation",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var registerTooltip = typeof(ToDoListHandler).GetMethod(
            "RegisterToDoTooltip",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var runtimeEntryType = typeof(ToDoListHandler).GetNestedType(
            "ToDoRuntimeEntry",
            BindingFlags.NonPublic);
        var translatedPayloadReadyGetter = runtimeEntryType?
            .GetProperty(
                "TranslatedPayloadReady",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
            .GetMethod;

        Assert.NotNull(applyPresentation);
        Assert.NotNull(registerTooltip);
        Assert.NotNull(runtimeEntryType);
        Assert.NotNull(translatedPayloadReadyGetter);
        Assert.True(
            MethodReferences(applyPresentation!, translatedPayloadReadyGetter!),
            "ToDoList native and swap presentation must skip rows whose translated payload is still pending.");
        Assert.True(
            MethodReferences(registerTooltip!, translatedPayloadReadyGetter!),
            "ToDoList hover tooltips must not treat pending placeholder rows as translated payload.");
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
    public void JournalListOriginalSnapshot_RejectsTranslatedTextFromARecycledNode()
    {
        var matcher = typeof(JournalHandler).GetMethod(
            "MatchesJournalListOriginalSnapshot",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotNull(matcher);
        Assert.False((bool)matcher!.Invoke(
            null,
            ["Different quest", "Previous quest", "Quest anterior"])!);
        Assert.True((bool)matcher.Invoke(
            null,
            ["Previous quest", "Previous quest", "Quest anterior"])!);
        Assert.False((bool)matcher.Invoke(
            null,
            ["Quest anterior", "Previous quest", "Quest anterior"])!);
    }

    /// <summary>
    ///     Ensures Journal can still recognize translated text that this
    ///     handler itself wrote when restoring a visible native mutation.
    /// </summary>
    [Fact]
    public void JournalListOwnedMutationSnapshot_AcceptsTheTranslatedTextWeWrote()
    {
        var matcher = typeof(JournalHandler).GetMethod(
            "MatchesJournalListOwnedMutationSnapshot",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotNull(matcher);
        Assert.True((bool)matcher!.Invoke(
            null,
            ["Previous quest", "Previous quest", "Quest anterior"])!);
        Assert.True((bool)matcher.Invoke(
            null,
            ["Quest anterior", "Previous quest", "Quest anterior"])!);
        Assert.False((bool)matcher.Invoke(
            null,
            ["Different quest", "Previous quest", "Quest anterior"])!);
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
    private static QuestAddonHandlerDependencies CreateDependencies(
        Func<QuestPlate, QuestPlate?>? findQuestPlate = null,
        Func<QuestPlate, QuestPlate?>? findQuestPlateByName = null)
    {
        return new QuestAddonHandlerDependencies
        {
            Config = new Config(),
            TranslationService = null!,
            FindQuestPlate = findQuestPlate ?? (_ => null),
            FindQuestPlateByName = findQuestPlateByName ?? (_ => null),
            FindQuestPopupText = _ => null,
            InsertQuestPlate = _ => string.Empty,
            InsertQuestPopupTextAsync = _ => Task.FromResult(string.Empty),
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
              RegisterTranslatedHoverTooltipTextNodeBounds = null!,
          };
    }

    /// <summary>
    /// Creates the minimal data-manager proxy required when the handler builds
    /// a quest plate with the current game version.
    /// </summary>
    /// <returns>The configured data-manager proxy.</returns>
    private static IDataManager CreateDataManager()
    {
        var gameDataProperty = typeof(IDataManager).GetProperty("GameData") ??
                               throw new MissingMemberException(
                                   typeof(IDataManager).FullName,
                                   "GameData");
        var gameData = RuntimeHelpers.GetUninitializedObject(
            gameDataProperty.PropertyType);
        var repositoriesProperty = gameDataProperty.PropertyType.GetProperty(
            "Repositories") ??
            throw new MissingMemberException(
                gameDataProperty.PropertyType.FullName,
                "Repositories");
        var repositories = (IDictionary)(Activator.CreateInstance(
            repositoriesProperty.PropertyType) ??
            throw new InvalidOperationException(
                "Could not create a Lumina repository dictionary."));
        var repositoryType = repositoriesProperty.PropertyType
            .GetGenericArguments()[1];
        var repository = RuntimeHelpers.GetUninitializedObject(repositoryType);
        SetMember(repository, "Version", "test-version");
        repositories.Add("ffxiv", repository);
        SetMember(gameData, "Repositories", repositories);

        var dataManager = DispatchProxy.Create<IDataManager, DataManagerProxy>();
        ((DataManagerProxy)(object)dataManager).GameData = gameData;
        return dataManager;
    }

    /// <summary>
    /// Sets a property or compiler-generated backing field on an uninitialized
    /// test object.
    /// </summary>
    /// <param name="instance">The object to update.</param>
    /// <param name="memberName">The member name.</param>
    /// <param name="value">The value to assign.</param>
    private static void SetMember(
        object instance,
        string memberName,
        object value)
    {
        var property = instance.GetType().GetProperty(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property?.SetMethod != null)
        {
            property.SetValue(instance, value);
            return;
        }

        var backingField = instance.GetType().GetField(
            $"<{memberName}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new MissingMemberException(instance.GetType().FullName, memberName);
        backingField.SetValue(instance, value);
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

    /// <summary>
    /// Supplies the game-data member needed by the game-version helper.
    /// </summary>
    private class DataManagerProxy : DispatchProxy
    {
        /// <summary>Gets or sets the fake Lumina game-data instance.</summary>
        public object? GameData { get; set; }

        /// <summary>
        /// Dispatches the game-data getter used by quest plate construction.
        /// </summary>
        /// <param name="targetMethod">The invoked interface method.</param>
        /// <param name="args">The invoked method arguments.</param>
        /// <returns>The configured game-data instance.</returns>
        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
        {
            if (targetMethod?.Name == "get_GameData")
            {
                return this.GameData;
            }

            throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
