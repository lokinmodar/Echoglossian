// <copyright file="LibreTranslateEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.LibreTranslate;

namespace Echoglossian.PluginUI.EngineConfigUI;

public static class LibreTranslateEngineUI
{
    public static bool Draw(Config config, PromptTemplateManager _)
    {
        var changed = false;
        var instanceLabels = new[]
        {
            "libretranslate.com",
            "libretranslate.de",
            Resources.ResourceManager.GetString("CustomLabel", Resources.Culture) ??
            "Custom",
        };

        ImGui.TextWrapped(Resources.SettingsForLibreTranslateText);

        // Instance type dropdown
        var currentInstanceIndex = (int)config.LibreTranslateInstanceType;
        if (ImGui.Combo(
                Resources.ResourceManager.GetString("LibreTranslateInstanceLabel", Resources.Culture) ??
                "Instance",
                ref currentInstanceIndex,
                instanceLabels,
                instanceLabels.Length))
        {
            config.LibreTranslateInstanceType =
                (LibreTranslateInstanceType)currentInstanceIndex;
            changed = true;
        }

        // Custom endpoint field
        if (config.LibreTranslateInstanceType ==
            LibreTranslateInstanceType.Custom)
        {
            bool isEndpointInvalid;
            changed |= FieldValidationHelper.ValidatedInputText(
                Resources.LibreTranslateAPIEndpoint,
                ref config.LibreTranslateUrl,
                300,
                out isEndpointInvalid);
        }

        // Optional API key
        bool isApiKeyInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            Resources.ResourceManager.GetString("OptionalApiKeyLabel", Resources.Culture) ??
            "API Key (optional)",
            ref config.LibreTranslateApiKey,
            300,
            out isApiKeyInvalid);

        if (changed)
        {
            FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
            Echoglossian.SaveConfig(config);
        }

        return changed;
    }
}
