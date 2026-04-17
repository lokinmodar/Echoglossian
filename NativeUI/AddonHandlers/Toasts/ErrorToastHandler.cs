// <copyright file="ErrorToastHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Toasts;

/// <summary>
///     Handles the "_TextError" toast runtime inside the new addon-handler model.
/// </summary>
internal sealed class ErrorToastHandler : AddonTextToastHandler
{
  private const string ErrorToastAddonName = "_TextError";
  private const string ErrorToastType = "Error";

  /// <summary>
  ///     Initializes a new instance of the <see cref="ErrorToastHandler" />
  ///     class.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <param name="translationService">The translation service used by the plugin.</param>
  /// <param name="findToastMessage">
  ///     Delegate used to look up previously translated error toast messages.
  /// </param>
  /// <param name="insertToastMessageAsync">
  ///     Delegate used to persist translated error toast messages.
  /// </param>
  /// <param name="updateOverlay">
  ///     Delegate used to publish translated content to the error toast overlay.
  /// </param>
  /// <param name="clearOverlay">
  ///     Delegate used to clear the error toast overlay state.
  /// </param>
  /// <param name="updateOverlayBounds">
  ///     Delegate used to update the error toast overlay bounds from the current
  ///     live addon instance.
  /// </param>
  /// <param name="normalizeReplacementText">
  ///     Delegate used to normalize translated text before native replacement.
  /// </param>
  public unsafe ErrorToastHandler(
      Config config,
      TranslationService translationService,
      Func<ToastMessage, ToastMessage?> findToastMessage,
      Func<ToastMessage, Task<string>> insertToastMessageAsync,
      Action<string, string, string> updateOverlay,
      Action clearOverlay,
      UpdateToastOverlayBoundsDelegate updateOverlayBounds,
      Func<string, string> normalizeReplacementText)
      : base(
          config,
          ErrorToastAddonName,
          ErrorToastType,
          translationService,
          findToastMessage,
          insertToastMessageAsync,
          updateOverlay,
          clearOverlay,
          updateOverlayBounds,
          AddonTextNodeResolvers.ResolveFirstTextNode,
          normalizeReplacementText,
          cfg => cfg.TranslateErrorToast,
          cfg => cfg.ErrorToastTranslationDisplayMode)
  {
  }
}
