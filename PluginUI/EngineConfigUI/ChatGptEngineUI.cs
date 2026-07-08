// <copyright file="ChatGptEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Components;
using Echoglossian.Translators.OpenAI;

namespace Echoglossian.PluginUI.EngineConfigUI;

public static class ChatGPTEngineUI
{
    private const string CustomLiveModelRefreshScope = "OpenAICompatible";
    private const string OfficialLiveModelRefreshScope = "OpenAI";
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
                ? Resources.OpenAiProviderVariantCustomOpenAiCompatible
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
            ImGui.TextWrapped(Resources.OpenAiCompatibleProviderDescription);
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
            ? Resources.OpenAiProviderVariantCustomOpenAiCompatible
            : Resources.OpenAiProviderVariantOfficialOpenAi;

        if (ImGui.BeginCombo(
                Resources.OpenAiProviderVariantLabel,
                preview))
        {
            foreach (var variant in Enum.GetValues<OpenAiProviderVariant>())
            {
                var label = variant == OpenAiProviderVariant.CustomOpenAICompatible
                    ? Resources.OpenAiProviderVariantCustomOpenAiCompatible
                    : Resources.OpenAiProviderVariantOfficialOpenAi;
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
                ForceLiveModelRefresh(config, activeSettings.Variant);
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
                ForceLiveModelRefresh(config, settings.Variant);
            }
            else if (!useLiveModels)
            {
                OpenAIModelManager.ResetToDefault();
                customLiveModelFetchAttempted = false;
                customLiveModelFetchSucceeded = false;
                LiveModelRefreshCoordinator.Clear(GetLiveModelRefreshScope(settings.Variant));
            }
        }

        if (useLiveModels)
        {
            ImGui.SameLine();
            if (ImGui.Button(Resources.Reload))
            {
                ForceLiveModelRefresh(config, settings.Variant);
            }
        }

        RequestLiveModelRefreshIfNeeded(config, settings.Variant);

        if (settings.Variant == OpenAiProviderVariant.CustomOpenAICompatible &&
            useLiveModels &&
            customLiveModelFetchAttempted &&
            !customLiveModelFetchSucceeded)
        {
            ImGui.TextWrapped(Resources.OpenAiCompatibleLiveModelFetchFailed);
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
            ImGui.TextWrapped(Resources.OpenAiCompatibleManualModelHint);
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
    ///     Forces a live model refresh for the selected provider variant.
    /// </summary>
    /// <param name="config">The active plugin configuration.</param>
    /// <param name="variant">The provider variant being refreshed.</param>
    private static void ForceLiveModelRefresh(
        Config config,
        OpenAiProviderVariant variant)
    {
        LiveModelRefreshCoordinator.ForceRefresh(
            GetLiveModelRefreshScope(variant),
            BuildLiveModelRefreshSignature(config, variant),
            () => RefreshLiveModelsAsync(config, variant));
    }

    /// <summary>
    ///     Requests a refresh only when the active provider inputs changed
    ///     while live model fetching remains enabled.
    /// </summary>
    /// <param name="config">The active plugin configuration.</param>
    /// <param name="variant">The provider variant being refreshed.</param>
    private static void RequestLiveModelRefreshIfNeeded(
        Config config,
        OpenAiProviderVariant variant)
    {
        var useLiveModels = variant == OpenAiProviderVariant.CustomOpenAICompatible
            ? config.UseLiveCustomOpenAiCompatibleModelList
            : config.UseLiveOpenAIModelList;
        LiveModelRefreshCoordinator.RequestIfNeeded(
            GetLiveModelRefreshScope(variant),
            useLiveModels,
            BuildLiveModelRefreshSignature(config, variant),
            () => RefreshLiveModelsAsync(config, variant));
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
    ///     Refreshes the live model list for the selected provider variant.
    /// </summary>
    /// <param name="config">The active plugin configuration.</param>
    /// <param name="variant">The provider variant being refreshed.</param>
    private static async Task RefreshLiveModelsAsync(
        Config config,
        OpenAiProviderVariant variant)
    {
        if (variant == OpenAiProviderVariant.CustomOpenAICompatible)
        {
            customLiveModelFetchAttempted = true;
            string apiKey = config.CustomOpenAiCompatibleApiKey ?? string.Empty;
            string baseUrl = config.CustomOpenAiCompatibleBaseUrl ?? string.Empty;
            await RefreshCustomLiveModelsAsync(apiKey, baseUrl);
            return;
        }

        string officialApiKey = config.ChatGptApiKey ?? string.Empty;
        string officialBaseUrl = config.ChatGPTBaseUrl ?? string.Empty;
        await OpenAIModelManager.RefreshAsync(officialApiKey, officialBaseUrl, "OpenAI");
    }

    /// <summary>
    ///     Builds the refresh signature for the selected provider variant.
    /// </summary>
    /// <param name="config">The active plugin configuration.</param>
    /// <param name="variant">The provider variant being refreshed.</param>
    /// <returns>The stable refresh signature.</returns>
    private static string BuildLiveModelRefreshSignature(
        Config config,
        OpenAiProviderVariant variant)
    {
        if (variant == OpenAiProviderVariant.CustomOpenAICompatible)
        {
            return LiveModelRefreshSignatureHelper.Build(
                new LiveModelRefreshSignatureComponent(
                    "apiKeyHash",
                    config.CustomOpenAiCompatibleApiKey,
                    Sensitive: true),
                new LiveModelRefreshSignatureComponent(
                    "baseUrl",
                    config.CustomOpenAiCompatibleBaseUrl));
        }

        return LiveModelRefreshSignatureHelper.Build(
            new LiveModelRefreshSignatureComponent(
                "apiKeyHash",
                config.ChatGptApiKey,
                Sensitive: true),
            new LiveModelRefreshSignatureComponent(
                "baseUrl",
                config.ChatGPTBaseUrl));
    }

    /// <summary>
    ///     Resolves the refresh coordinator scope for the selected provider
    ///     variant.
    /// </summary>
    /// <param name="variant">The provider variant being refreshed.</param>
    /// <returns>The refresh scope key.</returns>
    private static string GetLiveModelRefreshScope(OpenAiProviderVariant variant)
    {
        return variant == OpenAiProviderVariant.CustomOpenAICompatible
            ? CustomLiveModelRefreshScope
            : OfficialLiveModelRefreshScope;
    }

    /// <summary>
    /// Builds the official OpenAI tooltip map for known models.
    /// </summary>
    /// <returns>The tooltip dictionary.</returns>
    private static Dictionary<string, string> BuildOfficialModelTooltips()
    {
        return new Dictionary<string, string>
        {
            ["gpt-3.5-turbo"] = Resources.ChatGptModelTooltipGpt35Turbo,
            ["gpt-3.5-turbo-16k"] = Resources.ChatGptModelTooltipGpt35Turbo16k,
            ["gpt-4"] = Resources.ChatGptModelTooltipGpt4,
            ["gpt-4-turbo"] = Resources.ChatGptModelTooltipGpt4Turbo,
            ["gpt-4o"] = Resources.ChatGptModelTooltipGpt4o,
            ["gpt-4o-mini"] = Resources.ChatGptModelTooltipGpt4oMini,
        };
    }
}
