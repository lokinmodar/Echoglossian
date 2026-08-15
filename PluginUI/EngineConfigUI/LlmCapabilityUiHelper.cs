// <copyright file="LlmCapabilityUiHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.Capabilities;

namespace Echoglossian.PluginUI.EngineConfigUI;

/// <summary>
///     Resolves LLM configuration control state from the shared capability
///     policy used for provider request payloads.
/// </summary>
public static class LlmCapabilityUiHelper
{
    /// <summary>
    ///     Gets the temperature slider state for the active LLM capability
    ///     scope.
    /// </summary>
    /// <param name="scope">The active capability lookup scope.</param>
    /// <param name="fallbackMin">The engine-specific default minimum value.</param>
    /// <param name="fallbackMax">The engine-specific default maximum value.</param>
    /// <returns>The effective slider state and disabled-control explanation.</returns>
    public static LlmCapabilitySliderState GetTemperatureSliderState(
        LlmCapabilityScope scope,
        float fallbackMin,
        float fallbackMax)
    {
        var decision = LlmCapabilityPolicyService.GetSnapshot(scope)
            .GetDecision(LlmCapabilityParameterName.Temperature);
        var minValue = decision.MinValue ?? fallbackMin;
        var maxValue = decision.MaxValue ?? fallbackMax;

        if (decision.SupportState == LlmCapabilitySupportState.Supported &&
            !decision.OmitWhenDefaultOnly)
        {
            return new LlmCapabilitySliderState(true, minValue, maxValue, string.Empty);
        }

        var tooltipText = decision.OmitWhenDefaultOnly
            ? Resources.TemperatureControlDefaultOnlyTooltip
            : decision.SupportState == LlmCapabilitySupportState.Unknown
                ? Resources.TemperatureControlUnknownTooltip
                : Resources.TemperatureControlUnsupportedTooltip;
        return new LlmCapabilitySliderState(false, minValue, maxValue, tooltipText);
    }
}

/// <summary>
///     Describes the effective UI state for an LLM temperature slider.
/// </summary>
/// <param name="IsEnabled">Whether the control may accept user input.</param>
/// <param name="MinValue">The inclusive slider minimum value.</param>
/// <param name="MaxValue">The inclusive slider maximum value.</param>
/// <param name="TooltipText">The explanation shown for a disabled control.</param>
public readonly record struct LlmCapabilitySliderState(
    bool IsEnabled,
    float MinValue,
    float MaxValue,
    string TooltipText);
