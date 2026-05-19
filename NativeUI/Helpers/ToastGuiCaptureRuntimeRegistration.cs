// <copyright file="ToastGuiCaptureRuntimeRegistration.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
///     Handles bootstrap and teardown of the experimental ToastGui-assisted
///     toast capture runtime for supported normal and error toasts.
/// </summary>
public partial class Echoglossian
{
  /// <summary>
  ///     Creates the ToastGui-assisted capture runtime using the same shared
  ///     toast persistence helpers already used by the addon-handler path.
  /// </summary>
  /// <returns>The initialized runtime.</returns>
  private ToastGuiCaptureRuntime CreateToastGuiCaptureRuntime()
  {
    return new ToastGuiCaptureRuntime(
        this.configuration,
        TranslationService,
        this.FindAndReturnToastMessage,
        toastMessage => Task.Run(() => this.InsertToastMessageData(toastMessage)));
  }

  /// <summary>
  ///     Registers the ToastGui-assisted capture runtime with Dalamud's
  ///     callback surface for supported normal and error toasts.
  /// </summary>
  private void RegisterToastGuiCaptureRuntime()
  {
    ToastGuiInterface.Toast += this.toastGuiCaptureRuntime.HandleNormalToast;
    ToastGuiInterface.ErrorToast += this.toastGuiCaptureRuntime.HandleErrorToast;
  }

  /// <summary>
  ///     Unregisters the ToastGui-assisted capture runtime from Dalamud's
  ///     callback surface.
  /// </summary>
  private void UnregisterToastGuiCaptureRuntime()
  {
    ToastGuiInterface.Toast -= this.toastGuiCaptureRuntime.HandleNormalToast;
    ToastGuiInterface.ErrorToast -= this.toastGuiCaptureRuntime.HandleErrorToast;
  }
}
