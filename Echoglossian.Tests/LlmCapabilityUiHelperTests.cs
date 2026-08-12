// <copyright file="LlmCapabilityUiHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.PluginUI.EngineConfigUI;
using Echoglossian.Properties;
using Echoglossian.Translators.Capabilities;

using FluentAssertions;

using System.Globalization;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers policy-driven temperature slider state for LLM engine UI.
/// </summary>
public sealed class LlmCapabilityUiHelperTests
{
    /// <summary>
    ///     Ensures default-only temperature decisions disable the control and
    ///     explain the provider restriction.
    /// </summary>
    [Fact]
    public void GetTemperatureSliderState_WithDefaultOnlyDecision_DisablesControlAndExplainsWhy()
    {
        var originalCulture = Resources.Culture;
        Resources.Culture = CultureInfo.GetCultureInfo("en-US");
        var scope = new LlmCapabilityScope(
            Echoglossian.TransEngines.ChatGPT,
            "OpenAI",
            "https://api.openai.com/v1",
            "gpt-5.6-terra");

        try
        {
            var state = LlmCapabilityUiHelper.GetTemperatureSliderState(
                scope,
                0.1f,
                1.0f);

            state.IsEnabled.Should().BeFalse();
            state.TooltipText.Should().Contain("default-only");
        }
        finally
        {
            Resources.Culture = originalCulture;
        }
    }

    /// <summary>
    ///     Ensures unknown temperature capability remains disabled instead of
    ///     implying that the configured value will be sent.
    /// </summary>
    [Fact]
    public void GetTemperatureSliderState_WithUnknownDecision_DisablesControlAndExplainsWhy()
    {
        var originalCulture = Resources.Culture;
        Resources.Culture = CultureInfo.GetCultureInfo("en-US");
        var scope = new LlmCapabilityScope(
            Echoglossian.TransEngines.OpenRouter,
            "OpenRouter",
            "https://openrouter.ai/api/v1",
            "unknown-model");

        try
        {
            var state = LlmCapabilityUiHelper.GetTemperatureSliderState(
                scope,
                0.1f,
                1.0f);

            state.IsEnabled.Should().BeFalse();
            state.MinValue.Should().Be(0.1f);
            state.MaxValue.Should().Be(1.0f);
            state.TooltipText.Should().Contain("unknown");
        }
        finally
        {
            Resources.Culture = originalCulture;
        }
    }

    /// <summary>
    ///     Ensures supported overlay ranges replace the engine fallback
    ///     slider range.
    /// </summary>
    [Fact]
    public void GetTemperatureSliderState_WithSupportedOverlay_UsesResolvedRange()
    {
        var scope = new LlmCapabilityScope(
            Echoglossian.TransEngines.OpenRouter,
            "OpenRouter",
            "https://openrouter.ai/api/v1",
            "range-model");
        LlmCapabilityCacheManager.Clear();
        LlmCapabilityCacheManager.PublishRule(
            LlmCapabilityRuleDefinition.ExactModel(
                "OpenRouter",
                "OpenRouter",
                "https://openrouter.ai/api/v1",
                "range-model",
                LlmCapabilityParameterName.Temperature,
                LlmCapabilitySupportState.Supported,
                minValue: 0.2f,
                maxValue: 0.8f,
                reason: "Provider metadata confirmed the temperature range."));

        try
        {
            var state = LlmCapabilityUiHelper.GetTemperatureSliderState(
                scope,
                0.1f,
                1.0f);

            state.IsEnabled.Should().BeTrue();
            state.MinValue.Should().Be(0.2f);
            state.MaxValue.Should().Be(0.8f);
            state.TooltipText.Should().BeEmpty();
        }
        finally
        {
            LlmCapabilityCacheManager.Clear();
        }
    }
}
