// <copyright file="VisibleStorySurfaceRetranslationDispatcher.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Talk;
using Echoglossian.NativeUI.Handlers;

namespace Echoglossian.PluginUI.Helpers;

/// <summary>
///     Dispatches explicit visible retranslation requests across registered
///     story-facing addon handlers.
/// </summary>
public sealed class VisibleStorySurfaceRetranslationDispatcher
{
  /// <summary>
  ///     Dispatches the explicit retranslation request to the first applicable
  ///     visible story-surface handler.
  /// </summary>
  /// <param name="handlers">The registered addon handlers to inspect.</param>
  /// <returns>
  ///     The first applicable visible retranslation result, or a non-applicable
  ///     result when no visible story surface is currently available.
  /// </returns>
  public async Task<VisibleDialogueRetranslationResult> DispatchAsync(
      IEnumerable<(string AddonName, IAddonTranslationHandler Handler)> handlers)
  {
    foreach (var (_, handler) in handlers)
    {
      if (handler is not IVisibleDialogueRetranslationHandler
          visibleDialogueRetranslationHandler)
      {
        continue;
      }

      var result = await visibleDialogueRetranslationHandler
          .RetranslateVisibleTextAndPersistAsync()
          .ConfigureAwait(false);
      if (result.IsApplicable)
      {
        return result;
      }
    }

    return new VisibleDialogueRetranslationResult(
        false,
        false,
        null,
        Resources.TranslatorDebuggerUnknown,
        VisibleStorySurfaceText.GetNoVisibleSurfaceAvailableMessage());
  }
}
