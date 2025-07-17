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

        bool isApiKeyInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            Resources.MicrosoftTranslatorAPIKey,
            ref config.MicrosoftTranslatorApiKey,
            200,
            out isApiKeyInvalid);

        bool isRegionInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            Resources.Region,
            ref config.MicrosoftTranslatorRegion,
            100,
            out isRegionInvalid);

        bool isEndpointInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            Resources.Endpoint,
            ref config.MicrosoftTranslatorEndpoint,
            300,
            out isEndpointInvalid);

        if (changed)
        {
            FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
            Echoglossian.SaveConfig(config);
        }

        return changed;
    }
}