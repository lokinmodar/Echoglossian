// <copyright file="ToastGuiRouteState.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Toasts;

/// <summary>
///     Describes which family-level route currently owns one supported toast
///     path at runtime.
/// </summary>
internal enum ToastGuiRouteState
{
  /// <summary>
  ///     The legacy addon-handler route remains active.
  /// </summary>
  LegacyAddonHandlers,

  /// <summary>
  ///     The legacy ToastGui capture-only prefetch path is active.
  /// </summary>
  ToastGuiCapturePrefetch,

  /// <summary>
  ///     The alternate full ToastGui runtime owns the route.
  /// </summary>
  ToastGuiFullRuntime,
}
