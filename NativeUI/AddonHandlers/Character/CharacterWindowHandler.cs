// <copyright file="CharacterWindowHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;
using Echoglossian.NativeUI.AddonHandlers.Common;

namespace Echoglossian.NativeUI.AddonHandlers.Character;

/// <summary>
///     Handles DB-first translation for the main character window.
/// </summary>
public unsafe class CharacterWindowHandler : CharacterTextNodeWindowHandlerBase
{
    private static readonly TimeSpan RootCharacterAppliedStateRefreshWindow =
        TimeSpan.FromSeconds(1);

    private static readonly HashSet<string> StableHeaderTexts =
    [
        "Character",
        "Attributes",
        "Profile",
        "Classes/Jobs",
        "Reputation",
    ];
    private static readonly IReadOnlyDictionary<string, string>
        StableHeaderFallbackTranslations = new Dictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["Character"] = "Personagem",
            ["Attributes"] = "Atributos",
            ["Profile"] = "Perfil",
            ["Classes/Jobs"] = "Classes/Profissões",
            ["Reputation"] = "Reputação",
        };

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="CharacterWindowHandler" /> class.
    /// </summary>
    /// <param name="config">The configuration settings for the plugin.</param>
    /// <param name="hoverTooltipManager">The shared hover-tooltip manager.</param>
    /// <param name="translationService">The service used for translating text.</param>
    public CharacterWindowHandler(
        Config config,
        HoverTooltipManager hoverTooltipManager,
        TranslationService translationService)
        : base(
            addonName: "Character",
            config: config,
            hoverTooltipManager: hoverTooltipManager,
            translationService: translationService,
            stringArrayType: StringArrayType.Character,
            useAtkValues: true)
    {
    }

    /// <inheritdoc />
    protected override bool ShouldCaptureTextNode(
        AtkTextNode* textNode,
        string visibleText)
    {
        return base.ShouldCaptureTextNode(textNode, visibleText) ||
               this.CanCaptureSupplementalCharacterText(visibleText) ||
               IsStableCharacterHeaderText(visibleText);
    }

    /// <inheritdoc />
    protected override bool ShouldCaptureStringArrayValues(
        byte subscribedAddonsCount)
    {
        return !this.AreDynamicCharacterSubwindowsVisible();
    }

    /// <inheritdoc />
    protected override bool ShouldWriteStringArrayValues(
        byte subscribedAddonsCount)
    {
        return !this.AreDynamicCharacterSubwindowsVisible();
    }

    /// <inheritdoc />
    protected override bool ShouldReuseCompatiblePayloads()
    {
        return false;
    }

    /// <inheritdoc />
    protected override bool ShouldRequestStringArrayUpdates()
    {
        return true;
    }

    /// <inheritdoc />
    protected override bool ShouldRefreshAppliedStateOnPreDraw()
    {
        return false;
    }

    /// <inheritdoc />
    protected override TimeSpan GetAppliedStatePreDrawRefreshWindow()
    {
        return GetRootCharacterAppliedStateRefreshWindow();
    }

    /// <inheritdoc />
    private protected override bool TryApplyCustomTextNodePayload(
        AtkUnitBase* addon,
        DbFirstGameWindowPayload sourcePayload,
        DbFirstGameWindowPayload targetPayload)
    {
        return this.ApplyVisibleTextNodesByValue(
            addon,
            sourcePayload,
            targetPayload);
    }

    /// <summary>
    ///     Determines whether one Character-family tab addon that owns its own
    ///     visible text-node surface is currently visible.
    /// </summary>
    /// <returns>
    ///     <see langword="true" /> when a dynamic Character-family subwindow is
    ///     visible; otherwise <see langword="false" />.
    /// </returns>
    private bool AreDynamicCharacterSubwindowsVisible()
    {
        return this.IsAddonVisible("CharacterClass") ||
               this.IsAddonVisible("CharacterStatus") ||
               this.IsAddonVisible("CharacterProfile") ||
               this.IsAddonVisible("CharacterRepute");
    }

    /// <summary>
    ///     Gets the short post-lifecycle refresh window for the root
    ///     Character window so late-populating chrome can settle without
    ///     requiring permanent pre-draw polling.
    /// </summary>
    /// <returns>
    ///     The time span during which the root Character handler may keep
    ///     refreshing after lifecycle events to translate the title, tabs,
    ///     gear-set label, and current job name.
    /// </returns>
    internal static TimeSpan GetRootCharacterAppliedStateRefreshWindow()
    {
        return RootCharacterAppliedStateRefreshWindow;
    }

    /// <summary>
    ///     Determines whether one visible text belongs to the stable root
    ///     header of the Character window and should therefore be captured
    ///     even when it is not yet part of the canonical lookup.
    /// </summary>
    /// <param name="visibleText">The currently visible text.</param>
    /// <returns>
    ///     <see langword="true" /> when the text is one stable root-header
    ///     label; otherwise <see langword="false" />.
    /// </returns>
    internal static bool IsStableCharacterHeaderText(string visibleText)
    {
        return StableHeaderTexts.Contains(visibleText);
    }

    /// <summary>
    ///     Appends one local fallback translation set for the stable root
    ///     Character header labels so the window title and tab labels can
    ///     still translate even when the current DB-first canonical payloads
    ///     do not include them.
    /// </summary>
    /// <param name="originalLookup">The translated-to-original lookup.</param>
    /// <param name="translatedLookup">The original-to-translated lookup.</param>
    /// <param name="knownTexts">The known-text set to extend.</param>
    internal static void AppendStableHeaderFallbackTranslations(
        IDictionary<string, string> originalLookup,
        IDictionary<string, string> translatedLookup,
        ISet<string> knownTexts)
    {
        foreach (var (originalText, translatedText) in
                 StableHeaderFallbackTranslations)
        {
            if (!translatedLookup.ContainsKey(originalText))
            {
                translatedLookup[originalText] = translatedText;
            }

            if (!originalLookup.ContainsKey(translatedText))
            {
                originalLookup[translatedText] = originalText;
            }

            knownTexts.Add(originalText);
            knownTexts.Add(translatedText);
        }
    }

    /// <summary>
    ///     Tries to resolve one local fallback translation for the stable root
    ///     Character header labels.
    /// </summary>
    /// <param name="originalText">The original English header text.</param>
    /// <param name="translatedText">The translated fallback text.</param>
    /// <returns>
    ///     <see langword="true" /> when a local fallback exists; otherwise
    ///     <see langword="false" />.
    /// </returns>
    internal static bool TryGetStableHeaderFallbackTranslation(
        string originalText,
        out string translatedText)
    {
        return StableHeaderFallbackTranslations.TryGetValue(
            originalText,
            out translatedText!);
    }
}
