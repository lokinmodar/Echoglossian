// <copyright file="LibreTranslateEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.LibreTranslate;

namespace Echoglossian.PluginUI.EngineConfigUI;

public static class LibreTranslateEngineUI
{
    private static readonly string[] InstanceLabels =
        { "libretranslate.com", "libretranslate.de", "Custom" };

    public static bool Draw(Config config, PromptTemplateManager _)
    {
        var changed = false;

        ImGui.TextWrapped(Resources.SettingsForLibreTranslateText);

        // Instance type dropdown
        var currentInstanceIndex = (int)config.LibreTranslateInstanceType;
        if (ImGui.Combo(
                "Instance",
                ref currentInstanceIndex,
                InstanceLabels,
                InstanceLabels.Length))
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