// <copyright file="PluginUITranslationEnginesTab.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Globalization;
using Echoglossian.Helpers;
using Echoglossian.PluginUI.Helpers;
using Echoglossian.Translators;

namespace Echoglossian.PluginUI.Tabs;

/// <summary>
///     Renders the Translation Engines tab, supporting engine selection and
///     per-engine configuration.
/// </summary>
public static class TranslationEnginesTab
{
    /// <summary>
    ///     Draws the translation engine settings UI, allowing users to select and
    ///     configure translation engines.
    /// </summary>
    /// <param name="config">The configuration object containing translation settings.</param>
    /// <param name="languageIndex">
    ///     The index of the selected language in the language
    ///     list.
    /// </param>
    /// <param name="langDict">
    ///     Dictionary mapping language indices to their
    ///     information, including supported engines.
    /// </param>
    /// <param name="rebuildTranslationService">
    ///     The action to rebuild the translation
    ///     service when settings change.
    /// </param>
    /// <param name="runtimeActionsAvailable">
    ///     Whether live runtime-owned actions are available.
    /// </param>
    /// <returns>True if any settings were changed; otherwise, false.</returns>
    public static bool Draw(
        Config config,
        int languageIndex,
        Dictionary<int, LanguageInfo> langDict,
        Action rebuildTranslationService,
        bool runtimeActionsAvailable = true)
    {
        var changed = false;
        var promptManager = new PromptTemplateManager(config);

        changed |= ImGui.Checkbox(
            Resources.TranslateTextsAgain,
            ref config.TranslateAlreadyTranslatedTexts);

        var supportedEngines =
            langDict.TryGetValue(languageIndex, out var langInfo)
                ? langInfo.SupportedEngines ?? new List<int>()
                : new List<int>();

        var engineOptions = supportedEngines
            .Where(TranslationEngineSelectionMigrationHelper.IsConcreteEngineId)
            .Distinct()
            .OrderBy(id => id)
            .Select(id => new TranslationEngineOption(
                id,
                GetDisplayName((Echoglossian.TransEngines)id)))
            .ToArray();

        if (engineOptions.Length == 0)
        {
            ImGui.Text(Resources.NoSettingsForEngine);
            return changed;
        }

        if (TranslationEngineSelectionMigrationHelper.NormalizeAndSyncSelection(
                config,
                config.Version,
                supportedEngines))
        {
            rebuildTranslationService();
            changed = true;
        }

        if (LlmSurfaceGroupRoutingPolicy.NormalizeDialogueOverrideSelection(config))
        {
            rebuildTranslationService();
            changed = true;
        }

        var selected = Array.FindIndex(
            engineOptions,
            option => option.EngineId == config.ChosenTransEngine);
        if (selected < 0 && engineOptions.Length > 0)
        {
            selected = 0;
        }

        if (ImGui.Combo(
                Resources.TranslationEngineChoose,
                ref selected,
                engineOptions.Select(option => option.Label).ToArray(),
                engineOptions.Length))
        {
            TranslationEngineSelectionMigrationHelper.ApplyExplicitSelection(
                config,
                engineOptions[selected].EngineId);
            TranslationEngineSelectionMigrationHelper.NormalizeAndSyncSelection(
                config,
                config.Version,
                supportedEngines);
            rebuildTranslationService();
            changed = true;
        }

        ImGui.Separator();
        ImGui.BeginGroup();

        using var suppressedRefreshRequests = !runtimeActionsAvailable
            ? LiveModelRefreshCoordinator.SuppressRequests()
            : null;
        if (!runtimeActionsAvailable)
        {
            ImGui.BeginDisabled();
        }

        Echoglossian.TransEngines engine;
        try
        {
            engine = (Echoglossian.TransEngines)config.ChosenTransEngine;
            changed |= DrawEngineConfiguration(config, promptManager, engine);

            ImGui.EndGroup();
            ImGui.Separator();
            ImGui.Spacing();
            changed |= DrawDialogueOverrideSection(
                config,
                promptManager,
                engine,
                rebuildTranslationService);
        }
        finally
        {
            if (!runtimeActionsAvailable)
            {
                ImGui.EndDisabled();
            }
        }

        ImGui.Separator();
        ImGui.Spacing();
        changed |= DrawDialogueGlossarySection(config, runtimeActionsAvailable);

        return changed;
    }

    /// <summary>
    ///     Draws the settings UI for one specific translation engine.
    /// </summary>
    /// <param name="config">The active plugin configuration.</param>
    /// <param name="promptManager">The shared prompt template manager.</param>
    /// <param name="engine">The engine whose configuration should be shown.</param>
    /// <returns><see langword="true" /> when the configuration changed.</returns>
    private static bool DrawEngineConfiguration(
        Config config,
        PromptTemplateManager promptManager,
        Echoglossian.TransEngines engine)
    {
        var changed = false;
        switch (engine)
        {
            case Echoglossian.TransEngines.Google:
                changed |= GoogleEngineUI.Draw(config);
                break;
            case Echoglossian.TransEngines.Deepl:
                changed |= DeepLEngineUI.Draw(config);
                break;
            case Echoglossian.TransEngines.ChatGPT:
                changed |= ChatGPTEngineUI.Draw(config, promptManager);
                break;
            case Echoglossian.TransEngines.YandexCloud:
                changed |= YandexCloudEngineUI.Draw(config, promptManager);
                break;
            case Echoglossian.TransEngines.GTranslate:
                changed |= GTranslateEngineUI.Draw(config);
                break;
            case Echoglossian.TransEngines.DeepSeek:
                changed |= DeepSeekEngineUI.Draw(config, promptManager);
                break;
            case Echoglossian.TransEngines.Ollama:
                try
                {
                    changed |= OllamaEngineUI.Draw(config, promptManager);
                }
                catch (Exception ex)
                {
                    PluginRuntimeLog.Error(
                        $"OllamaEngineUI failed: {ex.Message}, {ex.StackTrace}");
                    ImGui.TextColored(
                        new Vector4(1f, 0.4f, 0.4f, 1f),
                        Resources.OllamaEngineUiFailedToRender);
                }

                break;
            case Echoglossian.TransEngines.LibreTranslate:
                changed |= LibreTranslateEngineUI.Draw(config, promptManager);
                break;
            case Echoglossian.TransEngines.Microsoft:
                changed |= MicrosoftEngineUI.Draw(config, promptManager);
                break;
            case Echoglossian.TransEngines.Amazon:
                changed |= AmazonEngineUI.Draw(config, promptManager);
                break;
            case Echoglossian.TransEngines.Gemini:
                changed |= GeminiEngineUI.Draw(config, promptManager);
                break;
            case Echoglossian.TransEngines.YandexPublic:
                changed |= YandexPublicEngineUI.Draw(config);
                break;
            case Echoglossian.TransEngines.OpenRouter:
                changed |= OpenRouterEngineUI.Draw(config, promptManager);
                break;
            case Echoglossian.TransEngines.LmStudio:
                changed |= LmStudioEngineUI.Draw(config, promptManager);
                break;
            case Echoglossian.TransEngines.Claude:
                changed |= ClaudeEngineUI.Draw(config, promptManager);
                break;
            default:
                ImGui.Text(Resources.NoSettingsForEngine);
                break;
        }

        return changed;
    }

    /// <summary>
    ///     Draws the first-pass operator-facing LLM-only routing override for
    ///     dialogue-family surfaces.
    /// </summary>
    /// <param name="config">The active plugin configuration.</param>
    /// <param name="promptManager">The shared prompt template manager.</param>
    /// <param name="primaryEngine">The currently selected global engine.</param>
    /// <param name="rebuildTranslationService">
    ///     The action used to rebuild runtime
    ///     translator instances immediately after routing changes.
    /// </param>
    /// <returns><see langword="true" /> when the configuration changed.</returns>
    private static bool DrawDialogueOverrideSection(
        Config config,
        PromptTemplateManager promptManager,
        Echoglossian.TransEngines primaryEngine,
        Action rebuildTranslationService)
    {
        var changed = false;
        DrawSubsectionHeader(
            Resources.DialogueLlmOverrideSectionLabel);
        ImGui.TextWrapped(
            Resources.DialogueLlmOverrideDescription);

        var overrideToggleChanged = ImGui.Checkbox(
            Resources.UseDialogueLlmOverrideLabel,
            ref config.UseDialogueLlmOverride);
        changed |= overrideToggleChanged;
        if (overrideToggleChanged)
        {
            rebuildTranslationService();
        }

        var llmEngineOptions = Enum.GetValues<Echoglossian.TransEngines>()
            .Where(LlmSurfaceGroupRoutingPolicy.IsLlmEngine)
            .Select(engine => new TranslationEngineOption(
                (int)engine,
                GetDisplayName(engine)))
            .ToArray();

        var selected = Array.FindIndex(
            llmEngineOptions,
            option => option.EngineId == config.DialogueLlmEngine);
        if (selected < 0 && llmEngineOptions.Length > 0)
        {
            selected = 0;
        }

        ImGui.BeginDisabled(!config.UseDialogueLlmOverride);
        if (ImGui.Combo(
                Resources.DialogueLlmEngineLabel,
                ref selected,
                llmEngineOptions.Select(option => option.Label).ToArray(),
                llmEngineOptions.Length))
        {
            config.DialogueLlmEngine = llmEngineOptions[selected].EngineId;
            config.DialogueLlmEngineKey =
                ((Echoglossian.TransEngines)config.DialogueLlmEngine).ToString();
            rebuildTranslationService();
            changed = true;
        }

        if (config.UseDialogueLlmOverride)
        {
            var overrideEngine =
                (Echoglossian.TransEngines)config.DialogueLlmEngine;
            var overrideConfigured =
                TranslationEngineConfigurationHelper.IsConfigured(
                    config,
                    overrideEngine);
            if (!overrideConfigured)
            {
                ImGui.TextColored(
                    new Vector4(1f, 0.4f, 0.4f, 1f),
                    Resources.DialogueLlmOverrideNeedsConfigurationText);
            }

            if (overrideEngine == primaryEngine)
            {
                ImGui.TextWrapped(
                    Resources.DialogueLlmOverrideMatchesPrimaryText);
            }
            else
            {
                ImGui.Separator();
                changed |= DrawEngineConfiguration(
                    config,
                    promptManager,
                    overrideEngine);
            }
        }

        ImGui.EndDisabled();

        return changed;
    }

    /// <summary>
    ///     Draws the operator-facing structured dialogue glossary settings used
    ///     by issue 148.
    /// </summary>
    /// <param name="config">The active plugin configuration.</param>
    /// <param name="runtimeActionsAvailable">
    ///     Whether live glossary actions are available.
    /// </param>
    /// <returns><see langword="true" /> when the configuration changed.</returns>
    private static bool DrawDialogueGlossarySection(
        Config config,
        bool runtimeActionsAvailable)
    {
        var changed = false;
        var snapshot = StructuredDialogueGlossaryStore.GetSnapshot();
        DrawSubsectionHeader(
            Resources.DialogueGlossarySectionLabel);
        ImGui.TextWrapped(
            Resources.DialogueGlossaryDescription);

        changed |= ImGui.Checkbox(
            Resources.EnableDialogueGlossaryInjectionLabel,
            ref config.EnableDialogueGlossaryInjection);

        var glossaryActionsAvailable =
            runtimeActionsAvailable &&
            config.EnableDialogueGlossaryInjection;
        ImGui.BeginDisabled(!glossaryActionsAvailable);
        var glossaryPathLabel = Resources.DialogueGlossaryFilePathLabel;
        changed |= FieldValidationHelper.ValidatedInputText(
            glossaryPathLabel,
            ref config.DialogueGlossaryFilePath,
            1024,
            out _);

        if (ImGui.Button(
                Resources.ReloadDialogueGlossaryButtonLabel))
        {
            StructuredDialogueGlossaryStore.Refresh(
                config.DialogueGlossaryFilePath);
        }

        ImGui.EndDisabled();
        if (!runtimeActionsAvailable &&
            ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip(Resources.PreviewImageryUnavailableText);
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(!runtimeActionsAvailable);
        if (ImGui.Button(
                Resources.ClearDialogueGlossaryButtonLabel))
        {
            StructuredDialogueGlossaryStore.Clear();
        }

        ImGui.EndDisabled();
        if (!runtimeActionsAvailable &&
            ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip(Resources.PreviewImageryUnavailableText);
        }

        DrawDialogueGlossarySnapshot(snapshot);
        return changed;
    }

    /// <summary>
    ///     Draws the current shared glossary snapshot inline in the translation
    ///     engines tab.
    /// </summary>
    /// <param name="snapshot">The current glossary snapshot.</param>
    private static void DrawDialogueGlossarySnapshot(
        StructuredDialogueGlossaryStore.StructuredDialogueGlossarySnapshot snapshot)
    {
        var statusText = snapshot.LastLoadSucceeded switch
        {
            true => Resources.DialogueGlossaryStatusLoaded,
            false => Resources.DialogueGlossaryStatusFailed,
            _ => Resources.DialogueGlossaryStatusIdle,
        };
        ImGui.TextWrapped(
            string.Format(
                CultureInfo.CurrentCulture,
                Resources.DialogueGlossarySnapshotSummary,
                statusText,
                snapshot.EntryCount,
                snapshot.SkippedEntryCount));

        if (!string.IsNullOrWhiteSpace(snapshot.LastLoadPath))
        {
            ImGui.TextWrapped(
                string.Format(
                    CultureInfo.CurrentCulture,
                    Resources.DialogueGlossarySnapshotPath,
                    snapshot.LastLoadPath));
        }

        if (snapshot.LastLoadObservedAtUtc.HasValue)
        {
            ImGui.TextWrapped(
                string.Format(
                    CultureInfo.CurrentCulture,
                    Resources.DialogueGlossarySnapshotUtc,
                    snapshot.LastLoadObservedAtUtc.Value.ToString(
                        "u",
                        CultureInfo.InvariantCulture)));
        }

        if (!string.IsNullOrWhiteSpace(snapshot.LastLoadFailureDetail))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.4f, 0.4f, 1f));
            ImGui.TextWrapped(
                string.Format(
                    CultureInfo.CurrentCulture,
                    Resources.DialogueGlossarySnapshotFailure,
                    snapshot.LastLoadFailureDetail));
            ImGui.PopStyleColor();
        }
    }

    /// <summary>
    ///     Draws a muted subsection header used to visually separate nested
    ///     configuration groups inside the translation engine section.
    /// </summary>
    /// <param name="title">The subsection title to render.</param>
    private static void DrawSubsectionHeader(string title)
    {
        ImGui.TextDisabled(title);
        ImGui.Separator();
        ImGui.Spacing();
    }

    /// <summary>
    ///     Resolves the user-facing display name for one concrete translation
    ///     engine without depending on list ordering.
    /// </summary>
    /// <param name="engine">The concrete engine enum value.</param>
    /// <returns>The display label shown in the combo.</returns>
    private static string GetDisplayName(Echoglossian.TransEngines engine)
    {
        return engine switch
        {
            Echoglossian.TransEngines.Google => "Google",
            Echoglossian.TransEngines.Deepl => "DeepL",
            Echoglossian.TransEngines.ChatGPT => "ChatGPT",
            Echoglossian.TransEngines.YandexCloud => "YandexCloud",
            Echoglossian.TransEngines.GTranslate => "GTranslate",
            Echoglossian.TransEngines.DeepSeek => "DeepSeek",
            Echoglossian.TransEngines.Ollama => "Ollama",
            Echoglossian.TransEngines.LibreTranslate => "LibreTranslate",
            Echoglossian.TransEngines.Microsoft => "Microsoft",
            Echoglossian.TransEngines.Amazon => "Amazon",
            Echoglossian.TransEngines.Gemini => "Gemini",
            Echoglossian.TransEngines.YandexPublic => "YandexPublic",
            Echoglossian.TransEngines.OpenRouter => "OpenRouter",
            Echoglossian.TransEngines.LmStudio => "LmStudio",
            Echoglossian.TransEngines.Claude => "Claude",
            _ => engine.ToString(),
        };
    }

    /// <summary>
    ///     Represents one user-facing translation engine choice in the settings
    ///     combo.
    /// </summary>
    /// <param name="EngineId">The concrete engine id persisted in config.</param>
    /// <param name="Label">The user-facing label.</param>
    private sealed record TranslationEngineOption(int EngineId, string Label);
}
