// <copyright file="QuestAddonHandlerLifecycleTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;

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
    ///     Ensures tooltip-only JournalDetail rendering remains read-only unless
    ///     it must restore native text previously written by this handler.
    /// </summary>
    /// <param name="writesNativeTranslation">
    ///     Whether the active display mode writes native translations.
    /// </param>
    /// <param name="ownsNativeMutation">
    ///     Whether this handler previously wrote the visible native state.
    /// </param>
    /// <param name="expectedAction">The required native mutation action.</param>
    [Theory]
    [InlineData(
        true,
        false,
        nameof(JournalDetailNativeMutationAction.ApplyTranslation))]
    [InlineData(
        false,
        true,
        nameof(JournalDetailNativeMutationAction.RestoreOriginal))]
    [InlineData(false, false, nameof(JournalDetailNativeMutationAction.None))]
    public void ResolveNativeMutationAction_PreservesTooltipOnlyNativeState(
        bool writesNativeTranslation,
        bool ownsNativeMutation,
        string expectedAction)
    {
        Assert.Equal(
            Enum.Parse<JournalDetailNativeMutationAction>(expectedAction),
            JournalDetailHandler.ResolveNativeMutationAction(
                writesNativeTranslation,
                ownsNativeMutation));
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
    ///     Ensures every remaining quest-family handler is reachable through
    ///     runtime wiring, otherwise the addon-specific apply path never runs.
    /// </summary>
    /// <param name="handlerType">The quest handler type that must be wired.</param>
    [Theory]
    [InlineData(typeof(JournalAcceptHandler))]
    [InlineData(typeof(JournalResultHandler))]
    [InlineData(typeof(RecommendListHandler))]
    [InlineData(typeof(AreaMapHandler))]
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
