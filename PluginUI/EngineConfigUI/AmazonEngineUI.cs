// <copyright file="AmazonEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.EngineConfigUI;

public static class AmazonEngineUI
{
    public static bool Draw(Config config, PromptTemplateManager promptManager)
    {
        var changed = false;

        ImGui.TextWrapped(Resources.SettingsForAmazonTranslateText);

        var awsAccessKey = config.AwsAccessKey ?? string.Empty;
        bool isAccessKeyInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            Resources.AWSAccessKey,
            ref awsAccessKey,
            200,
            out isAccessKeyInvalid);
        config.AwsAccessKey = awsAccessKey;

        var awsSecretKey = config.AwsSecretKey ?? string.Empty;
        bool isSecretKeyInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            Resources.AWSSecretKey,
            ref awsSecretKey,
            200,
            out isSecretKeyInvalid);
        config.AwsSecretKey = awsSecretKey;

        var awsRegion = config.AwsRegion ?? string.Empty;
        bool isRegionInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            Resources.Region,
            ref awsRegion,
            100,
            out isRegionInvalid);
        config.AwsRegion = awsRegion;

        if (changed)
        {
            FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
            Echoglossian.SaveConfig(config);
        }

        return changed;
    }
}
