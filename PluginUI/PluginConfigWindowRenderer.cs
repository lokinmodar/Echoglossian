// <copyright file="PluginConfigWindowRenderer.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Components;
using Echoglossian.PluginUI.Helpers;
using Echoglossian.PluginUI.Tabs;

namespace Echoglossian.PluginUI;

/// <summary>
/// Draws the reusable Echoglossian configuration window.
/// </summary>
public sealed class PluginConfigWindowRenderer
{
    /// <summary>
    /// Gets the bounds captured during the most recent successful draw.
    /// </summary>
    internal RectangleF? LastWindowBounds { get; private set; }

    /// <summary>
    /// Draws the configuration window and saves any changes through the active
    /// configuration-save path.
    /// </summary>
    /// <param name="context">The state and runtime dependencies for the window.</param>
    /// <param name="isOpen">The window open state.</param>
    /// <returns><see langword="true" /> when the configuration changed.</returns>
    public bool Draw(PluginConfigWindowContext context, ref bool isOpen)
    {
        var changed = false;
        var languages = context.Languages as Dictionary<int, LanguageInfo> ??
                        new Dictionary<int, LanguageInfo>(context.Languages);
        Echoglossian.LangDict = languages;
        Echoglossian.LanguageInt = context.Configuration.Lang;
        Echoglossian.SelectedLanguage = languages[context.Configuration.Lang];
        LanguageDropdownHelper.Initialize(languages);

        ImGui.SetNextWindowSizeConstraints(
            new Vector2(900, 900),
            new Vector2(1920, 1080));
        ImGui.Begin(
            $"{Resources.ConfigWindowTitle} - Plugin Version: {context.PluginVersion}",
            ref isOpen);

        this.DrawTranslationStatusHeader(context.Configuration);
        ImGui.Spacing();
        ImGui.BeginGroup();

        if (ImGui.BeginTabBar(
                "TabBar",
                ImGuiTabBarFlags.NoCloseWithMiddleMouseButton))
        {
            if (ImGui.BeginTabItem(Resources.ConfigTab7Name))
            {
                changed |= this.DrawTranslationSetupTab(context, languages);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(Resources.ConfigTab0Name))
            {
                changed |= OverlayTab.Draw(context.Configuration);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(Resources.ConfigTab8Name))
            {
                changed |= TroubleshootingTab.Draw(
                    context.Configuration,
                    context.RuntimeActionsAvailable);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(Resources.ConfigTabAbout))
            {
                changed |= AboutTab.Draw(
                    context.Configuration,
                    context.LogoTextureHandle,
                    context.ImagesAvailable);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.EndGroup();

        PluginConfigWindowFooter.DrawFooter(
            ref isOpen,
            ref changed,
            context.PixTextureHandle,
            context.CryptoTextureHandle,
            context.ImagesAvailable);

        if (context.RuntimeActionsAvailable)
        {
            changed |= PluginAssetRequirementUiHelper.DrawMissingAssetsPopup(
                context.Configuration);
        }

        this.LastWindowBounds = new RectangleF(
            ImGui.GetWindowPos().X,
            ImGui.GetWindowPos().Y,
            ImGui.GetWindowSize().X,
            ImGui.GetWindowSize().Y);

        ImGui.End();

        if (changed)
        {
            Echoglossian.SaveConfig(context.Configuration);
        }

        return changed;
    }

    /// <summary>
    /// Draws the compact translation status line outside the tab content.
    /// </summary>
    /// <param name="config">The configuration being edited.</param>
    private void DrawTranslationStatusHeader(Config config)
    {
        var blockReason = TranslationActivationGuard.GetBlockReason(
            config,
            Echoglossian.SelectedLanguage);
        var translationBlockedByMissingAssets =
            blockReason == TranslationActivationGuard.BlockReason
                .MissingRequiredAssets;
        var translationBlockedByEngineConfiguration =
            blockReason == TranslationActivationGuard.BlockReason
                .EngineConfigurationIncomplete;

        if (translationBlockedByMissingAssets)
        {
            ImGui.TextColored(
                new Vector4(255, 165, 0, 255),
                Resources.TranslationBlockedByMissingAssetsStatusText);
            return;
        }

        if (translationBlockedByEngineConfiguration)
        {
            ImGui.TextColored(
                new Vector4(255, 165, 0, 255),
                Resources.TranslationBlockedByEngineConfigurationText);
            return;
        }

        if (config.Translate)
        {
            ImGui.TextColored(
                new Vector4(0, 255, 0, 255),
                Resources.TranslationEnabled);
            return;
        }

        ImGui.TextColored(
            new Vector4(255, 255, 0, 255),
            Resources.TranslationDisabled);
    }

    /// <summary>
    /// Draws the combined language, engine, activation, and general settings
    /// tab.
    /// </summary>
    /// <param name="context">The configuration-window context.</param>
    /// <param name="languages">The mutable language dictionary used by existing tabs.</param>
    /// <returns><see langword="true" /> when the configuration changed.</returns>
    private bool DrawTranslationSetupTab(
        PluginConfigWindowContext context,
        Dictionary<int, LanguageInfo> languages)
    {
        var changed = false;

        using var scrollingChild = ImRaii.Child(
            "TranslationSetupSettings",
            new Vector2(-1, -100),
            false,
            ImGuiWindowFlags.NoBackground);

        if (!scrollingChild)
        {
            return false;
        }

        this.DrawTranslationSetupSectionHeader(
            Resources.LanguageSelectLabelText);
        changed |= this.DrawTranslationLanguageSelectionSection(
            context,
            languages);
        this.DrawTranslationSetupSectionBreak();

        this.DrawTranslationSetupSectionHeader(
            Resources.TranslationEngineChoose);
        changed |= TranslationEnginesTab.Draw(
            context.Configuration,
            context.Configuration.Lang,
            languages,
            context.RebuildTranslationService,
            context.RuntimeActionsAvailable);

        this.DrawTranslationSetupSectionBreak();
        this.DrawTranslationSetupSectionHeader(Resources.EnableTranslation);
        changed |= this.DrawTranslationActivationSection(
            context.Configuration,
            context.RuntimeActionsAvailable);

        this.DrawTranslationSetupSectionBreak();
        this.DrawTranslationSetupSectionHeader(Resources.ConfigTabGeneralName);
        changed |= GeneralTab.Draw(context.Configuration);

        return changed;
    }

    /// <summary>
    /// Draws the compact heading used to separate setup-tab option groups.
    /// </summary>
    /// <param name="title">The section title to render.</param>
    private void DrawTranslationSetupSectionHeader(string title)
    {
        ImGui.TextDisabled(title);
        ImGui.Separator();
        ImGui.Spacing();
    }

    /// <summary>
    /// Draws spacing between setup-tab option groups.
    /// </summary>
    private void DrawTranslationSetupSectionBreak()
    {
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
    }

    /// <summary>
    /// Draws language selection and applies its existing runtime side effects.
    /// </summary>
    /// <param name="context">The configuration-window context.</param>
    /// <param name="languages">The available languages.</param>
    /// <returns><see langword="true" /> when the configuration changed.</returns>
    private bool DrawTranslationLanguageSelectionSection(
        PluginConfigWindowContext context,
        Dictionary<int, LanguageInfo> languages)
    {
        var config = context.Configuration;
        var changed = false;

        using (context.PushGeneralFont())
        {
            Echoglossian.LangToRemoveDiacritics =
                languages.TryGetValue(config.Lang, out var selectedLanguage) &&
                selectedLanguage.SupportsNativeReplacementDiacriticsFallback;

            if (LanguageDropdownHelper.DrawLanguageDropdown(
                    ref config.Lang,
                    Resources.LanguageSelectLabelText))
            {
                Echoglossian.LanguageInt = config.Lang;
                Echoglossian.SpecialFontFileName = languages[config.Lang].FontName;
                Echoglossian.SelectedLanguage = languages[config.Lang];
                Echoglossian.LangToRemoveDiacritics =
                    Echoglossian.SelectedLanguage
                        .SupportsNativeReplacementDiacriticsFallback;
                LanguagePresentationPolicy.ApplyLanguageFlags(config);

                if (TranslationEngineSelectionMigrationHelper
                    .NormalizeAndSyncSelection(
                        config,
                        config.Version,
                        languages[config.Lang].SupportedEngines))
                {
                    context.RebuildTranslationService();
                    changed = true;
                }

                changed = true;
                context.ApplyLanguageRuntimeChanges(
                    config,
                    Echoglossian.SelectedLanguage);
            }
        }

        ImGui.SameLine();
        ImGui.Text(Resources.HoverTooltipIndicator);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Resources.LanguageSelectionTooltip);
        }

        if (config.UnsupportedLanguage)
        {
            ImGui.Text(Resources.LanguageNotSupportedText);
        }

        if (config.OverlayOnlyLanguage)
        {
            ImGui.Text(Resources.LanguageOnlySupportedUsingOverlay);
        }

        return changed;
    }

    /// <summary>
    /// Draws translation activation and blocks it while dependencies are not
    /// ready.
    /// </summary>
    /// <param name="config">The configuration being edited.</param>
    /// <param name="runtimeActionsAvailable">Whether asset management actions are available.</param>
    /// <returns><see langword="true" /> when the configuration changed.</returns>
    private bool DrawTranslationActivationSection(
        Config config,
        bool runtimeActionsAvailable)
    {
        var changed = false;

        var blockReason = TranslationActivationGuard.GetBlockReason(
            config,
            Echoglossian.SelectedLanguage);
        var translationBlockedByMissingAssets =
            blockReason == TranslationActivationGuard.BlockReason
                .MissingRequiredAssets;
        var translationBlockedByEngineConfiguration =
            blockReason == TranslationActivationGuard.BlockReason
                .EngineConfigurationIncomplete;
        var translationShouldBeBlocked =
            blockReason != TranslationActivationGuard.BlockReason.None;

        if (translationShouldBeBlocked)
        {
            changed |= this.AssignIfChanged(ref config.Translate, false);
        }

        if (!config.UnsupportedLanguage)
        {
            if (translationBlockedByMissingAssets ||
                translationBlockedByEngineConfiguration)
            {
                ImGui.BeginDisabled();
            }

            if (ImGui.Checkbox(Resources.EnableTranslation, ref config.Translate))
            {
                changed = true;
            }

            if (translationBlockedByMissingAssets ||
                translationBlockedByEngineConfiguration)
            {
                ImGui.EndDisabled();
            }

            if ((translationBlockedByMissingAssets ||
                    translationBlockedByEngineConfiguration) &&
                ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip(
                    translationBlockedByMissingAssets
                        ? Resources.TranslationRequiresDownloadedAssetsText
                        : Resources.TranslationBlockedByEngineConfigurationText);
            }
        }

        if (translationBlockedByMissingAssets)
        {
            ImGui.SameLine();
            ImGui.TextColored(
                new Vector4(255, 165, 0, 255),
                Resources.TranslationBlockedByMissingAssetsStatusText);
            changed |= PluginAssetRequirementUiHelper.DrawInlineWarning(
                config,
                runtimeActionsAvailable);
        }
        else if (translationBlockedByEngineConfiguration)
        {
            ImGui.TextWrapped(
                Resources.TranslationBlockedByEngineConfigurationText);
        }

        return changed;
    }

    /// <summary>
    /// Assigns a value only when it differs from the current value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="target">The value to update.</param>
    /// <param name="newValue">The proposed replacement value.</param>
    /// <returns><see langword="true" /> when the value changed.</returns>
    private bool AssignIfChanged<T>(ref T target, T newValue)
        where T : IEquatable<T>
    {
        if (target.Equals(newValue))
        {
            return false;
        }

        target = newValue;
        return true;
    }
}
