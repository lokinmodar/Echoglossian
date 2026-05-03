// <copyright file="QuestToastRuntimeRegistration.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
///     Handles bootstrap and teardown of the quest-toast runtime that hangs off
///     <see cref="IToastGui.QuestToast" /> instead of the addon-handler
///     registrar.
/// </summary>
public partial class Echoglossian
{
  /// <summary>
  ///     Creates the quest-toast runtime using the same shared toast cache,
  ///     persistence, overlay, and replacement helpers used by the rest of the
  ///     toast family.
  /// </summary>
  /// <returns>The initialized quest-toast runtime.</returns>
  private QuestToastRuntime CreateQuestToastRuntime()
  {
    return new QuestToastRuntime(
        this.configuration,
        TranslationService,
        this.FindAndReturnToastMessage,
        toastMessage => Task.Run(() => this.InsertToastMessageData(toastMessage)),
        (translatedName, translatedText, originalName) =>
            this.UpdateOverlayContent(
                this.questToastOverlay,
                translatedName,
                translatedText,
                originalName),
        () => this.ClearOverlay(this.questToastOverlay, clearText: true),
        text => this.RemoveDiacritics(
            text,
            this.SpecialCharsSupportedByGameFont));
  }

  /// <summary>
  ///     Registers the quest-toast runtime with Dalamud's toast callback
  ///     surface.
  /// </summary>
  private void RegisterQuestToastRuntime()
  {
    ToastGuiInterface.QuestToast += this.questToastRuntime.HandleQuestToast;
  }

  /// <summary>
  ///     Unregisters the quest-toast runtime from Dalamud's toast callback
  ///     surface.
  /// </summary>
  private void UnregisterQuestToastRuntime()
  {
    ToastGuiInterface.QuestToast -= this.questToastRuntime.HandleQuestToast;
  }
}
