// <copyright file="ChatGptEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Components;
using Echoglossian.Translators.OpenAI;

namespace Echoglossian.PluginUI.EngineConfigUI;

public static class ChatGPTEngineUI
{
    private static OpenAiProviderVariant lastRenderedVariant =
        OpenAiProviderVariant.OfficialOpenAI;

    private static bool customLiveModelFetchAttempted;
    private static bool customLiveModelFetchSucceeded;

    /// <summary>
    /// Draws the OpenAI-family engine configuration UI.
    /// </summary>
    /// <param name="config">The active plugin configuration.</param>
    /// <param name="promptManager">The prompt-template manager.</param>
    /// <returns><see langword="true" /> when the UI changed the config.</returns>
    public static bool Draw(Config config, PromptTemplateManager promptManager)
    {
        var changed = false;
        var settings = OpenAiProviderVariantHelper.ResolveActiveSettings(config);

        ImGui.TextWrapped(
            settings.Variant == OpenAiProviderVariant.CustomOpenAICompatible
                ? GetText(
                    "OpenAiProviderVariantCustomOpenAiCompatible",
                    "Custom OpenAI-Compatible")
                : Resources.SettingsForChatGptTransText);
        ImGui.Spacing();

        changed |= DrawProviderVariantSelector(config);
        ImGui.Spacing();

        if (config.OpenAiProviderVariant == OpenAiProviderVariant.OfficialOpenAI &&
            ImGui.Button(Resources.ChatGPTAPIKeyLink))
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName =
                        "https://platform.openai.com/settings/profile?tab=api-keys",
                    UseShellExecute = true,
                });
        }

        settings = OpenAiProviderVariantHelper.ResolveActiveSettings(config);
        if (settings.Variant == OpenAiProviderVariant.CustomOpenAICompatible)
        {
            ImGui.TextWrapped(GetText(
                "OpenAiCompatibleProviderDescription",
                "Configure an OpenAI-compatible endpoint, API key, and model. Live model fetch is optional; if the provider does not expose /models cleanly, keep a manual model id."));
            ImGui.Spacing();
        }

        changed |= DrawApiKeyField(config, settings.Variant);
        changed |= DrawBaseUrlField(config, settings.Variant);
        changed |= DrawLiveModelControls(config, settings);
        changed |= DrawModelSelection(config, settings);

        var temp = config.ChatGptTemperature;
        if (ImGui.SliderFloat(
                Resources.Temperature,
                ref temp,
                0.1f,
                1.0f,
                "%.1f"))
        {
            config.ChatGptTemperature = temp;
            changed = true;
        }

        PromptEditorUI.Draw(
            promptManager,
            Echoglossian.PromptType.ChatGPT,
            PromptTemplateManager.DefaultPrompt,
            Echoglossian.TransEngines.ChatGPT.ToString());

        if (changed)
        {
            FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
            Echoglossian.SaveConfig(config);
        }

        return changed;
    }

    /// <summary>
    /// Draws the provider-variant selector.
    /// </summary>
    /// <param name="config">The active plugin configuration.</param>
    /// <returns><see langword="true" /> when the selection changed.</returns>
    private static bool DrawProviderVariantSelector(Config config)
    {
        var changed = false;
        var selectedVariant = config.OpenAiProviderVariant;
        var preview = selectedVariant == OpenAiProviderVariant.CustomOpenAICompatible
            ? GetText(
                "OpenAiProviderVariantCustomOpenAiCompatible",
                "Custom OpenAI-Compatible")
            : GetText(
                "OpenAiProviderVariantOfficialOpenAi",
                "Official OpenAI");

        if (ImGui.BeginCombo(
                GetText("OpenAiProviderVariantLabel", "Provider"),
                preview))
        {
            foreach (var variant in Enum.GetValues<OpenAiProviderVariant>())
            {
                var label = variant == OpenAiProviderVariant.CustomOpenAICompatible
                    ? GetText(
                        "OpenAiProviderVariantCustomOpenAiCompatible",
                        "Custom OpenAI-Compatible")
                    : GetText(
                        "OpenAiProviderVariantOfficialOpenAi",
                        "Official OpenAI");
                var isSelected = variant == selectedVariant;
                if (ImGui.Selectable(label, isSelected))
                {
                    config.OpenAiProviderVariant = variant;
                    selectedVariant = variant;
                    changed = true;
                }

                if (isSelected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        if (lastRenderedVariant != config.OpenAiProviderVariant)
        {
            lastRenderedVariant = config.OpenAiProviderVariant;
            OpenAIModelManager.ResetToDefault();
            if (config.OpenAiProviderVariant == OpenAiProviderVariant.CustomOpenAICompatible)
            {
                customLiveModelFetchAttempted = false;
                customLiveModelFetchSucceeded = false;
            }

            var activeSettings = OpenAiProviderVariantHelper.ResolveActiveSettings(config);
            if (activeSettings.UseLiveModelList)
            {
                RequestLiveModelRefresh(config, activeSettings.Variant);
            }
        }

        return changed;
    }

    /// <summary>
    /// Draws the active provider API key field.
    /// </summary>
    /// <param name="config">The active plugin configuration.</param>
    /// <param name="variant">The selected provider variant.</param>
    /// <returns><see langword="true" /> when the field changed.</returns>
    private static bool DrawApiKeyField(Config config, OpenAiProviderVariant variant)
    {
        var changed = false;
        var label = variant == OpenAiProviderVariant.CustomOpenAICompatible
            ? Resources.APIKey
            : Resources.ChatGptApiKey;
        var value = variant == OpenAiProviderVariant.CustomOpenAICompatible
            ? config.CustomOpenAiCompatibleApiKey
            : config.ChatGptApiKey;

        bool isInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            label,
            ref value,
            400,
            out isInvalid);

        if (variant == OpenAiProviderVariant.CustomOpenAICompatible)
        {
            config.CustomOpenAiCompatibleApiKey = value;
        }
        else
        {
            config.ChatGptApiKey = value;
        }

        return changed;
    }

    /// <summary>
    /// Draws the active provider base URL field.
    /// </summary>
    /// <param name="config">The active plugin configuration.</param>
    /// <param name="variant">The selected provider variant.</param>
    /// <returns><see langword="true" /> when the field changed.</returns>
    private static bool DrawBaseUrlField(Config config, OpenAiProviderVariant variant)
    {
        var changed = false;
        var value = variant == OpenAiProviderVariant.CustomOpenAICompatible
            ? config.CustomOpenAiCompatibleBaseUrl
            : config.ChatGPTBaseUrl;

        bool isInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            Resources.ModelEndpoint,
            ref value,
            400,
            out isInvalid);

        if (variant == OpenAiProviderVariant.CustomOpenAICompatible)
        {
            config.CustomOpenAiCompatibleBaseUrl = value;
        }
        else
        {
            config.ChatGPTBaseUrl = value;
        }

        return changed;
    }

    /// <summary>
    /// Draws the live model-fetch controls for the active provider.
    /// </summary>
    /// <param name="config">The active plugin configuration.</param>
    /// <param name="settings">The resolved provider settings.</param>
    /// <returns><see langword="true" /> when the controls changed the config.</returns>
    private static bool DrawLiveModelControls(
        Config config,
        OpenAiProviderVariantHelper.OpenAiProviderSettings settings)
    {
        var changed = false;
        var useLiveModels = settings.Variant == OpenAiProviderVariant.CustomOpenAICompatible
            ? config.UseLiveCustomOpenAiCompatibleModelList
            : config.UseLiveOpenAIModelList;
        var previousUseLiveModels = useLiveModels;

        if (ImGui.Checkbox(Resources.FetchLiveModels, ref useLiveModels))
        {
            changed = true;
            SetUseLiveModelToggle(config, settings.Variant, useLiveModels);
            if (useLiveModels && !previousUseLiveModels)
            {
                RequestLiveModelRefresh(config, settings.Variant);
            }
            else if (!useLiveModels)
            {
                OpenAIModelManager.ResetToDefault();
                customLiveModelFetchAttempted = false;
                customLiveModelFetchSucceeded = false;
            }
        }

        if (useLiveModels)
        {
            ImGui.SameLine();
            if (ImGui.Button(Resources.Reload))
            {
                RequestLiveModelRefresh(config, settings.Variant);
            }
        }

        if (settings.Variant == OpenAiProviderVariant.CustomOpenAICompatible &&
            useLiveModels &&
            customLiveModelFetchAttempted &&
            !customLiveModelFetchSucceeded)
        {
            ImGui.TextWrapped(GetText(
                "OpenAiCompatibleLiveModelFetchFailed",
                "Could not fetch models from the configured OpenAI-compatible provider. Keep a manual model id or verify the endpoint and credentials."));
        }

        return changed;
    }

    /// <summary>
    /// Draws either a fetched-model dropdown or a manual model input for the
    /// active provider.
    /// </summary>
    /// <param name="config">The active plugin configuration.</param>
    /// <param name="settings">The resolved provider settings.</param>
    /// <returns><see langword="true" /> when the selected model changed.</returns>
    private static bool DrawModelSelection(
        Config config,
        OpenAiProviderVariantHelper.OpenAiProviderSettings settings)
    {
        var changed = false;
        if (settings.Variant == OpenAiProviderVariant.OfficialOpenAI)
        {
            var models = config.UseLiveOpenAIModelList
                ? OpenAIModelManager.CurrentModelList
                : OpenAITextModelDefaults.PredefinedModels;
            var selectedModel = config.OpenAILlmModel;
            changed |= ModelDropdownUI.Draw(
                Resources.LLMModel,
                ref selectedModel,
                models,
                settings.ProviderName,
                BuildOfficialModelTooltips());

            config.OpenAILlmModel = selectedModel;
            return changed;
        }

        var useLiveModels = config.UseLiveCustomOpenAiCompatibleModelList;
        if (useLiveModels && customLiveModelFetchSucceeded)
        {
            var models = OpenAIModelManager.CurrentModelList;
            var selectedModel = config.CustomOpenAiCompatibleModel;
            changed |= ModelDropdownUI.Draw(
                Resources.LLMModel,
                ref selectedModel,
                models,
                settings.ProviderName,
                BuildOfficialModelTooltips());
            config.CustomOpenAiCompatibleModel = selectedModel;
        }
        else
        {
            var modelValue = config.CustomOpenAiCompatibleModel;
            bool isInvalid;
            changed |= FieldValidationHelper.ValidatedInputText(
                Resources.LLMModel,
                ref modelValue,
                200,
                out isInvalid);
            config.CustomOpenAiCompatibleModel = modelValue;
            ImGui.TextWrapped(GetText(
                "OpenAiCompatibleManualModelHint",
                "Use the exact model id exposed by your OpenAI-compatible provider when live model listing is unavailable or disabled."));
        }

        return changed;
    }

    /// <summary>
    /// Stores the live-model toggle for the selected provider variant.
    /// </summary>
    /// <param name="config">The active plugin configuration.</param>
    /// <param name="variant">The provider variant being edited.</param>
    /// <param name="enabled">Whether live model listing is enabled.</param>
    private static void SetUseLiveModelToggle(
        Config config,
        OpenAiProviderVariant variant,
        bool enabled)
    {
        if (variant == OpenAiProviderVariant.CustomOpenAICompatible)
        {
            config.UseLiveCustomOpenAiCompatibleModelList = enabled;
        }
        else
        {
            config.UseLiveOpenAIModelList = enabled;
        }
    }

    /// <summary>
    /// Starts a live model refresh for the selected provider variant.
    /// </summary>
    /// <param name="config">The active plugin configuration.</param>
    /// <param name="variant">The provider variant being refreshed.</param>
    private static void RequestLiveModelRefresh(
        Config config,
        OpenAiProviderVariant variant)
    {
        if (variant == OpenAiProviderVariant.CustomOpenAICompatible)
        {
            customLiveModelFetchAttempted = true;
            string apiKey = config.CustomOpenAiCompatibleApiKey ?? string.Empty;
            string baseUrl = config.CustomOpenAiCompatibleBaseUrl ?? string.Empty;
            _ = RefreshCustomLiveModelsAsync(
                apiKey,
                baseUrl);
        }
        else
        {
            string apiKey = config.ChatGptApiKey ?? string.Empty;
            _ = OpenAIModelManager.RefreshAsync(apiKey);
        }
    }

    /// <summary>
    ///     Refreshes the custom OpenAI-compatible live model list and captures
    ///     the outcome for provider-specific UI feedback.
    /// </summary>
    /// <param name="apiKey">The custom provider API key.</param>
    /// <param name="baseUrl">The custom provider base URL.</param>
    private static async Task RefreshCustomLiveModelsAsync(
        string apiKey,
        string baseUrl)
    {
        customLiveModelFetchSucceeded =
            await OpenAIModelManager.RefreshAsync(
                apiKey,
                baseUrl,
                "OpenAI-Compatible");
    }

    /// <summary>
    /// Builds the official OpenAI tooltip map for known models.
    /// </summary>
    /// <returns>The tooltip dictionary.</returns>
    private static Dictionary<string, string> BuildOfficialModelTooltips()
    {
        return new Dictionary<string, string>
        {
            ["gpt-3.5-turbo"] = Resources.ResourceManager.GetString("ChatGptModelTooltipGpt35Turbo", Resources.Culture) ??
                                "⚡ Fast and affordable (4k tokens)",
            ["gpt-3.5-turbo-16k"] = Resources.ResourceManager.GetString("ChatGptModelTooltipGpt35Turbo16k", Resources.Culture) ??
                                    "⚡ 16k token context",
            ["gpt-4"] = Resources.ResourceManager.GetString("ChatGptModelTooltipGpt4", Resources.Culture) ??
                        "🧠 More capable but slower and costly",
            ["gpt-4-turbo"] = Resources.ResourceManager.GetString("ChatGptModelTooltipGpt4Turbo", Resources.Culture) ??
                              "🟢 Faster and cheaper GPT-4 variant",
            ["gpt-4o"] = Resources.ResourceManager.GetString("ChatGptModelTooltipGpt4o", Resources.Culture) ??
                         "👁 Multimodal and real-time model",
            ["gpt-4o-mini"] = Resources.ResourceManager.GetString("ChatGptModelTooltipGpt4oMini", Resources.Culture) ??
                              "⚡ GPT-4o Mini — fast and compact",
        };
    }

    /// <summary>
    /// Resolves a resource string with a fallback.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <param name="fallback">The fallback value.</param>
    /// <returns>The localized string or the fallback.</returns>
    private static string GetText(string key, string fallback)
    {
        return Resources.ResourceManager.GetString(key, Resources.Culture) ??
               fallback;
    }
}
