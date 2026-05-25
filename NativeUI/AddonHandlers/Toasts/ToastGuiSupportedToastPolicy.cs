// <copyright file="ToastGuiSupportedToastPolicy.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Toasts;

/// <summary>
///     Centralizes the family-level routing rules for the alternate
///     ToastGui-owned runtime path. Under that route, supported normal toasts
///     are intentionally treated as one unified logical family rather than as
///     addon-specific subtypes.
/// </summary>
internal static class ToastGuiSupportedToastPolicy
{
  /// <summary>
  ///     Gets whether the alternate callback-owned ToastGui route is enabled at
  ///     all.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <returns>
  ///     <see langword="true" /> when the alternate ToastGui route is enabled;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  public static bool UseSupportedToastGuiRuntime(Config config)
  {
    return config.TranslateToast &&
           config.UseToastGuiRuntimeForSupportedToasts;
  }

  /// <summary>
  ///     Gets whether the alternate callback-owned ToastGui route should own
  ///     the unified normal-toast family.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <returns>
  ///     <see langword="true" /> when the alternate route should own the
  ///     supported non-error toast family; otherwise, <see langword="false" />.
  /// </returns>
  public static bool UseSupportedNormalToastRuntime(Config config)
  {
    return UseSupportedToastGuiRuntime(config);
  }

  /// <summary>
  ///     Gets whether the alternate callback-owned ToastGui route should own
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
  ///     Gets the canonical family-level display mode used by the alternate
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
  ///     Gets whether the legacy ToastGui prefetch-only path should stay active
  ///     for supported normal toasts.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <returns>
  ///     <see langword="true" /> when the capture-only path should remain
  ///     active; otherwise, <see langword="false" />.
  /// </returns>
  public static bool UseLegacyNormalToastCapturePrefetch(Config config)
  {
    return config.TranslateToast &&
           config.UseToastGuiCaptureForSupportedToasts &&
           !UseSupportedNormalToastRuntime(config) &&
           (config.TranslateWideTextToast ||
            config.TranslateAreaToast ||
            config.TranslateClassChangeToast);
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
    return config.TranslateToast &&
           config.UseToastGuiCaptureForSupportedToasts &&
           !UseSupportedErrorToastRuntime(config) &&
           config.TranslateErrorToast;
  }

}
