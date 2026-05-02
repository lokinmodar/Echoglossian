// <copyright file="MicrosoftEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.EngineConfigUI;

public static class MicrosoftEngineUI
{
    public static bool Draw(Config config, PromptTemplateManager _)
    {
        var changed = false;

        ImGui.TextWrapped(Resources.SettingsForMicrosoftText);

        var apiKey = config.MicrosoftTranslatorApiKey ?? string.Empty;
        bool isApiKeyInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            Resources.MicrosoftTranslatorAPIKey,
            ref apiKey,
            200,
            out isApiKeyInvalid);
        config.MicrosoftTranslatorApiKey = apiKey;

        var region = config.MicrosoftTranslatorRegion ?? string.Empty;
        bool isRegionInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            Resources.Region,
            ref region,
            100,
            out isRegionInvalid);
        config.MicrosoftTranslatorRegion = region;

        var endpoint = config.MicrosoftTranslatorEndpoint ?? string.Empty;
        bool isEndpointInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            Resources.Endpoint,
            ref endpoint,
            300,
            out isEndpointInvalid);
        config.MicrosoftTranslatorEndpoint = endpoint;

        if (changed)
        {
            FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
            Echoglossian.SaveConfig(config);
        }

        return changed;
    }
}
