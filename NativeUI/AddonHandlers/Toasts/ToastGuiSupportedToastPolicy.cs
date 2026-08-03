// <copyright file="ToastGuiSupportedToastPolicy.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Game.Gui.Toast;

namespace Echoglossian.NativeUI.AddonHandlers.Toasts;

/// <summary>
///     Centralizes the family-level routing rules for the ToastGui-owned
///     runtime path. Supported normal toasts are intentionally treated as one
///     unified logical family rather than as addon-specific subtypes.
/// </summary>
internal static class ToastGuiSupportedToastPolicy
{
  /// <summary>
  ///     Gets the effective route state for the supported normal-toast family.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <returns>The effective route state for supported normal toasts.</returns>
  public static ToastGuiRouteState GetSupportedNormalToastRouteState(
      Config config)
  {
    if (UseSupportedNormalToastRuntime(config))
    {
      return ToastGuiRouteState.ToastGuiFullRuntime;
    }

    if (UseLegacyNormalToastCapturePrefetch(config))
    {
      return ToastGuiRouteState.ToastGuiCapturePrefetch;
    }

    return ToastGuiRouteState.LegacyAddonHandlers;
  }

  /// <summary>
  ///     Gets the effective route state for supported error toasts.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <returns>The effective route state for supported error toasts.</returns>
  public static ToastGuiRouteState GetSupportedErrorToastRouteState(
      Config config)
  {
    if (UseSupportedErrorToastRuntime(config))
    {
      return ToastGuiRouteState.ToastGuiFullRuntime;
    }

    if (UseLegacyErrorToastCapturePrefetch(config))
    {
      return ToastGuiRouteState.ToastGuiCapturePrefetch;
    }

    return ToastGuiRouteState.LegacyAddonHandlers;
  }

  /// <summary>
  ///     Gets whether the callback-owned ToastGui route is enabled at all.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <returns>
  ///     <see langword="true" /> when the ToastGui route is enabled;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  public static bool UseSupportedToastGuiRuntime(Config config)
  {
    return config.TranslateToast;
  }

  /// <summary>
  ///     Gets whether the callback-owned ToastGui route should own
  ///     the unified normal-toast family.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <returns>
  ///     <see langword="true" /> when the alternate route should own the
  ///     supported non-error toast family; otherwise, <see langword="false" />.
  /// </returns>
  public static bool UseSupportedNormalToastRuntime(Config config)
  {
    return UseSupportedToastGuiRuntime(config) &&
           HasAnyEnabledNormalToastType(config);
  }

  /// <summary>
  ///     Gets whether the callback-owned ToastGui route should own
  ///     error toasts.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <returns>
  ///     <see langword="true" /> when the alternate route should own error
  ///     toasts; otherwise, <see langword="false" />.
  /// </returns>
  public static bool UseSupportedErrorToastRuntime(Config config)
  {
    return UseSupportedToastGuiRuntime(config) &&
           config.TranslateErrorToast;
  }

  /// <summary>
  ///     Gets the canonical family-level display mode used by the
  ///     callback-owned runtime for the unified normal-toast family.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <returns>
  ///     The effective family-level display mode for supported non-error
  ///     toasts, currently sourced from the wide-text toast config.
  /// </returns>
  public static JournalTranslationDisplayMode GetNormalToastDisplayMode(
      Config config)
  {
    return config.WideTextToastTranslationDisplayMode;
  }

  /// <summary>
  ///     Gets the placement-specific display mode used by the callback-owned
  ///     normal-toast runtime.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <param name="position">The runtime toast placement.</param>
  /// <returns>The effective display mode for the current placement bucket.</returns>
  public static JournalTranslationDisplayMode GetNormalToastDisplayMode(
      Config config,
      ToastPosition position)
  {
    return position == ToastPosition.Bottom
        ? config.BottomToastTranslationDisplayMode
        : config.TopToastTranslationDisplayMode;
  }

  /// <summary>
  ///     Gets whether the legacy ToastGui prefetch-only path should stay active.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <returns>
  ///     <see langword="true" /> when the capture-only path should remain
  ///     active; otherwise, <see langword="false" />.
  /// </returns>
  public static bool UseLegacyNormalToastCapturePrefetch(Config config)
  {
    _ = config;
    return false;
  }

  /// <summary>
  ///     Gets whether the legacy ToastGui prefetch-only path should stay active
  ///     for error toasts.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <returns>
  ///     <see langword="true" /> when the capture-only error-toast path should
  ///     remain active; otherwise, <see langword="false" />.
  /// </returns>
  public static bool UseLegacyErrorToastCapturePrefetch(Config config)
  {
    _ = config;
    return false;
  }

  /// <summary>
  ///     Gets whether at least one supported normal-toast type is enabled in
  ///     the user configuration.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <returns>
  ///     <see langword="true" /> when at least one supported normal-toast
  ///     toggle is enabled; otherwise, <see langword="false" />.
  /// </returns>
  private static bool HasAnyEnabledNormalToastType(Config config)
  {
    return config.TranslateWideTextToast ||
           config.TranslateAreaToast ||
           config.TranslateClassChangeToast;
  }

}
