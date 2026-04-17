// <copyright file="AreaToastHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Toasts;

/// <summary>
///     Handles the "_AreaText" toast runtime inside the new addon-handler model.
/// </summary>
internal sealed class AreaToastHandler : AddonTextToastHandler
{
  private const string AreaToastAddonName = "_AreaText";
  private const string AreaToastType = "NonError";

  /// <summary>
  ///     Initializes a new instance of the <see cref="AreaToastHandler" />
  ///     class.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <param name="translationService">The translation service used by the plugin.</param>
  /// <param name="findToastMessage">
  ///     Delegate used to look up previously translated area toast messages.
  /// </param>
  /// <param name="insertToastMessageAsync">
  ///     Delegate used to persist translated area toast messages.
  /// </param>
  /// <param name="updateOverlay">
  ///     Delegate used to publish translated content to the area toast overlay.
  /// </param>
  /// <param name="clearOverlay">
  ///     Delegate used to clear the area toast overlay state.
  /// </param>
  /// <param name="updateOverlayBounds">
  ///     Delegate used to update the area toast overlay bounds from the current
  ///     live addon instance.
  /// </param>
  /// <param name="normalizeReplacementText">
  ///     Delegate used to normalize translated text before native replacement.
  /// </param>
  public unsafe AreaToastHandler(
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
          AreaToastAddonName,
          AreaToastType,
          translationService,
          findToastMessage,
          insertToastMessageAsync,
          updateOverlay,
          clearOverlay,
          updateOverlayBounds,
          AddonTextNodeResolvers.ResolveAreaTextNode,
          normalizeReplacementText,
          cfg => cfg.TranslateAreaToast,
          cfg => cfg.AreaToastTranslationDisplayMode)
  {
  }
}
