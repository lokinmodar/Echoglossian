// <copyright file="ToastGuiSupportedToastRuntimeRegistration.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
///     Handles bootstrap and teardown for the alternate callback-owned
///     ToastGui route that owns supported normal and error toasts.
/// </summary>
public partial class Echoglossian
{
  /// <summary>
  ///     Creates the alternate supported-toast runtime using the same shared
  ///     toast persistence and overlay helpers already used elsewhere in the
  ///     plugin.
  /// </summary>
  /// <returns>The initialized supported-toast runtime.</returns>
  private ToastGuiSupportedToastRuntime CreateToastGuiSupportedToastRuntime()
  {
    return new ToastGuiSupportedToastRuntime(
        this.configuration,
        TranslationService,
        this.FindAndReturnToastMessage,
        toastMessage => Task.Run(() => this.InsertToastMessageData(toastMessage)),
        (translatedName, translatedText, originalName) =>
            this.UpdateOverlayContent(
                this.toastOverlay,
                translatedName,
                translatedText,
                originalName),
        () => this.ClearOverlay(this.toastOverlay, clearText: true),
        (translatedName, translatedText, originalName) =>
            this.UpdateOverlayContent(
                this.errorToastOverlay,
                translatedName,
                translatedText,
                originalName),
        () => this.ClearOverlay(this.errorToastOverlay, clearText: true),
        text => this.RemoveDiacritics(
            text,
            this.SpecialCharsSupportedByGameFont));
  }

  /// <summary>
  ///     Registers the alternate supported-toast runtime with Dalamud's normal
  ///     and error-toast callbacks.
  /// </summary>
  private void RegisterToastGuiSupportedToastRuntime()
  {
    ToastGuiInterface.Toast += this.toastGuiSupportedToastRuntime.HandleNormalToast;
    ToastGuiInterface.ErrorToast += this.toastGuiSupportedToastRuntime.HandleErrorToast;
  }

  /// <summary>
  ///     Unregisters the alternate supported-toast runtime from Dalamud's
  ///     normal and error-toast callbacks.
  /// </summary>
  private void UnregisterToastGuiSupportedToastRuntime()
  {
    ToastGuiInterface.Toast -= this.toastGuiSupportedToastRuntime.HandleNormalToast;
    ToastGuiInterface.ErrorToast -= this.toastGuiSupportedToastRuntime.HandleErrorToast;
  }
}
