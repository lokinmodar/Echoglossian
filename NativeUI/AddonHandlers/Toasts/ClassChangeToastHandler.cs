// <copyright file="ClassChangeToastHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Toasts;

/// <summary>
///     Handles the "_TextClassChange" toast runtime inside the new addon-handler
///     model.
/// </summary>
internal sealed class ClassChangeToastHandler : AddonTextToastHandler
{
  private const string ClassChangeToastAddonName = "_TextClassChange";
  private const string ClassChangeToastType = "NonError";

  /// <summary>
  ///     Initializes a new instance of the
  ///     <see cref="ClassChangeToastHandler" /> class.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <param name="translationService">The translation service used by the plugin.</param>
  /// <param name="findToastMessage">
  ///     Delegate used to look up previously translated class-change toast
  ///     messages.
  /// </param>
  /// <param name="insertToastMessageAsync">
  ///     Delegate used to persist translated class-change toast messages.
  /// </param>
  /// <param name="updateOverlay">
  ///     Delegate used to publish translated content to the class-change toast
  ///     overlay.
  /// </param>
  /// <param name="clearOverlay">
  ///     Delegate used to clear the class-change toast overlay state.
  /// </param>
  /// <param name="updateOverlayBounds">
  ///     Delegate used to update the class-change toast overlay bounds from the
  ///     current live addon instance.
  /// </param>
  /// <param name="normalizeReplacementText">
  ///     Delegate used to normalize translated text before native replacement.
  /// </param>
  public unsafe ClassChangeToastHandler(
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
          ClassChangeToastAddonName,
          ClassChangeToastType,
          translationService,
          findToastMessage,
          insertToastMessageAsync,
          updateOverlay,
          clearOverlay,
          updateOverlayBounds,
          AddonTextNodeResolvers.ResolveFirstTextNode,
          normalizeReplacementText,
          cfg => cfg.TranslateClassChangeToast,
          cfg => cfg.ClassChangeToastTranslationDisplayMode)
  {
  }
}
