// <copyright file="WideTextToastHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Toasts;

/// <summary>
///     Handles the "_WideText" toast runtime inside the new addon-handler model.
/// </summary>
internal sealed class WideTextToastHandler : AddonTextToastHandler
{
  private const string WideTextAddonName = "_WideText";
  private const string WideTextToastType = "NonError";

  /// <summary>
  ///     Initializes a new instance of the <see cref="WideTextToastHandler" />
  ///     class.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <param name="translationService">The translation service used by the plugin.</param>
  /// <param name="findToastMessage">
  ///     Delegate used to look up previously translated non-error toast messages.
  /// </param>
  /// <param name="insertToastMessageAsync">
  ///     Delegate used to persist translated non-error toast messages.
  /// </param>
  /// <param name="updateOverlay">
  ///     Delegate used to publish translated content to the shared toast overlay.
  /// </param>
  /// <param name="clearOverlay">
  ///     Delegate used to clear the shared toast overlay state.
  /// </param>
  /// <param name="updateOverlayBounds">
  ///     Delegate used to update the shared toast overlay bounds from the current
  ///     live addon instance.
  /// </param>
  /// <param name="normalizeReplacementText">
  ///     Delegate used to normalize translated text before native replacement.
  /// </param>
  public unsafe WideTextToastHandler(
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
          WideTextAddonName,
          WideTextToastType,
          translationService,
          findToastMessage,
          insertToastMessageAsync,
          updateOverlay,
          clearOverlay,
          updateOverlayBounds,
          // Reference: Glyceri/TalkCopy (TalkHooks/Hooks/WideTextHook.cs)
          // reads "_WideText" from text node 3 during AddonLifecycle.PreUpdate.
          AddonTextNodeResolvers.ResolveWideTextNode,
          normalizeReplacementText,
          cfg => cfg.TranslateWideTextToast,
          cfg => cfg.WideTextToastTranslationDisplayMode)
  {
  }
}
