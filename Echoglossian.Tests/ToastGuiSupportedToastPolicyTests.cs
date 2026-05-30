// <copyright file="ToastGuiSupportedToastPolicyTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Toasts;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the merged family-level semantics used by the ToastGui runtime
///     path for supported toasts.
/// </summary>
public class ToastGuiSupportedToastPolicyTests
{
    /// <summary>
    ///     Ensures the supported normal-toast route remains on the legacy
    ///     addon-handler path while global toast translation is disabled.
    /// </summary>
    [Fact]
    public void GetSupportedNormalToastRouteState_ReturnsLegacyAddonHandlers_WhenToastTranslationIsDisabled()
    {
        var config = new Config
        {
            TranslateToast = false,
        };

        Assert.Equal(
            ToastGuiRouteState.LegacyAddonHandlers,
            ToastGuiSupportedToastPolicy.GetSupportedNormalToastRouteState(config));
    }

    /// <summary>
    ///     Ensures the supported normal-toast route is callback-owned when
    ///     global toast translation is enabled and at least one supported
    ///     normal toast type is enabled.
    /// </summary>
    [Fact]
    public void GetSupportedNormalToastRouteState_ReturnsToastGuiFullRuntime_WhenToastTranslationAndNormalTypeAreEnabled()
    {
        var config = new Config
        {
            TranslateToast = true,
            TranslateWideTextToast = true,
        };

        Assert.Equal(
            ToastGuiRouteState.ToastGuiFullRuntime,
            ToastGuiSupportedToastPolicy.GetSupportedNormalToastRouteState(config));
    }

    /// <summary>
    ///     Ensures the supported normal-toast route remains on the legacy path
    ///     when global toast translation is enabled but all normal toast type
    ///     toggles are disabled.
    /// </summary>
    [Fact]
    public void GetSupportedNormalToastRouteState_ReturnsLegacyAddonHandlers_WhenNoNormalToastTypeIsEnabled()
    {
        var config = new Config
        {
            TranslateToast = true,
            TranslateWideTextToast = false,
            TranslateAreaToast = false,
            TranslateClassChangeToast = false,
        };

        Assert.Equal(
            ToastGuiRouteState.LegacyAddonHandlers,
            ToastGuiSupportedToastPolicy.GetSupportedNormalToastRouteState(config));
    }

    /// <summary>
    ///     Ensures the supported error-toast route remains on the legacy
    ///     addon-handler path while global toast translation is disabled.
    /// </summary>
    [Fact]
    public void GetSupportedErrorToastRouteState_ReturnsLegacyAddonHandlers_WhenToastTranslationIsDisabled()
    {
        var config = new Config
        {
            TranslateToast = false,
            TranslateErrorToast = true,
        };

        Assert.Equal(
            ToastGuiRouteState.LegacyAddonHandlers,
            ToastGuiSupportedToastPolicy.GetSupportedErrorToastRouteState(config));
    }

    /// <summary>
    ///     Ensures the supported error-toast route remains callback-owned while
    ///     global toast translation and error-toast translation are both
    ///     enabled.
    /// </summary>
    [Fact]
    public void GetSupportedErrorToastRouteState_ReturnsToastGuiFullRuntime_WhenToastAndErrorTranslationAreEnabled()
    {
        var config = new Config
        {
            TranslateToast = true,
            TranslateErrorToast = true,
        };

        Assert.Equal(
            ToastGuiRouteState.ToastGuiFullRuntime,
            ToastGuiSupportedToastPolicy.GetSupportedErrorToastRouteState(config));
    }

    /// <summary>
    ///     Ensures legacy hidden toggles no longer change the supported
    ///     error-toast route while toast translation is enabled.
    /// </summary>
    [Fact]
    public void GetSupportedErrorToastRouteState_ReturnsToastGuiFullRuntime_WhenLegacyCaptureToggleIsEnabled()
    {
        var config = new Config
        {
            TranslateToast = true,
            TranslateErrorToast = true,
            UseToastGuiCaptureForSupportedToasts = true,
            UseToastGuiRuntimeForSupportedToasts = false,
        };

        Assert.Equal(
            ToastGuiRouteState.ToastGuiFullRuntime,
            ToastGuiSupportedToastPolicy.GetSupportedErrorToastRouteState(config));
    }

    /// <summary>
    ///     Ensures the legacy prefetch-only policy gates remain disabled now
    ///     that supported toasts are permanently callback-owned.
    /// </summary>
    [Fact]
    public void UseLegacyCapturePolicies_ReturnFalse()
    {
        var config = new Config
        {
            TranslateToast = true,
            TranslateWideTextToast = true,
            TranslateErrorToast = true,
            UseToastGuiCaptureForSupportedToasts = true,
            UseToastGuiRuntimeForSupportedToasts = false,
        };

        Assert.False(ToastGuiSupportedToastPolicy.UseLegacyNormalToastCapturePrefetch(config));
        Assert.False(ToastGuiSupportedToastPolicy.UseLegacyErrorToastCapturePrefetch(config));
    }

    /// <summary>
    ///     Ensures the unified normal-toast family reuses the canonical
    ///     wide-text toast display mode while the alternate ToastGui path is
    ///     enabled.
    /// </summary>
    [Fact]
    public void GetNormalToastDisplayMode_ReturnsWideTextToastDisplayMode()
    {
        var config = new Config
        {
            WideTextToastTranslationDisplayMode = JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips,
            AreaToastTranslationDisplayMode = JournalTranslationDisplayMode.TooltipTranslation,
            ClassChangeToastTranslationDisplayMode = JournalTranslationDisplayMode.NativeUiTranslation,
        };

        Assert.Equal(
            JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips,
            ToastGuiSupportedToastPolicy.GetNormalToastDisplayMode(config));
    }
}
