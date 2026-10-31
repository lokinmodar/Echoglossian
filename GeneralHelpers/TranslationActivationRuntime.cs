// <copyright file="TranslationActivationRuntime.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
/// Hosts Framework-thread runtime work for global translation activation.
/// </summary>
public partial class Echoglossian
{
  /// <summary>
  /// Restores only handlers that explicitly own mutation-aware cleanup.
  /// </summary>
  /// <param name="handlers">The currently registered addon handlers.</param>
  internal static void RestoreMutatedAddonHandlers(
      IEnumerable<(string AddonName, IAddonTranslationHandler Handler)> handlers)
  {
    foreach (var (addonName, handler) in handlers)
    {
      if (handler is IPluginUnloadAwareAddonHandler unloadAwareHandler)
      {
        try
        {
          unloadAwareHandler.OnPluginUnload();
        }
        catch (Exception exception)
        {
          PluginRuntimeLog.Error(
              "TranslationActivation",
              $"Failed to restore addon handler '{addonName}': {exception}");
        }
      }
    }
  }

  /// <summary>
  /// Stops translation work and removes translated presentation while keeping
  /// committed database and memory cache state intact.
  /// </summary>
  private void DisableTranslationRuntime()
  {
    this.queuedTranslationBroker.Dispose();
    this.ClearAcceptedQuestPrefetchState();
    this.ClearActionDetailPrefetchState();
    this.ClearItemDetailPrefetchState();
    this.ClearTraitDetailPrefetchState();
    this.ClearReferenceTextPrefetchState();
    this.ClearNamePlatePrefetchState();

    this.RestoreVisibleAddonPresentationStateBeforeRuntimeReset();
    this.ResetStructuredTooltipUiRuntime();
    this.ClearTranslationPresentationForDisable();

    this.UnregisterQuestToastRuntime();
    this.UnregisterToastGuiSupportedToastRuntime();
    this.UnregisterToastGuiCaptureRuntime();
    this.UnregisterNamePlateTranslationRuntime();
    this.namePlateTranslationRuntime.Dispose();
    NamePlateGuiInterface.RequestRedraw();
    this.RefreshAddonHandlerRegistrations();
    this.SynchronizeTranslationActivationSignatures();
  }

  /// <summary>
  /// Restarts shared translation runtimes and lets configured visible handlers
  /// consume committed cache entries before admitting missing work.
  /// </summary>
  private void EnableTranslationRuntime()
  {
    this.RebuildQueuedTranslationBroker();
    this.RebuildToastGuiRuntimes();
    this.namePlateTranslationRuntime = this.CreateNamePlateTranslationRuntime();
    this.RegisterNamePlateTranslationRuntime();
    this.RefreshAddonHandlerRegistrations();

    // Registered PreDraw handlers reapply committed cache hits on the next
    // visible frame; NamePlateGui requires an explicit redraw request.
    NamePlateGuiInterface.RequestRedraw();
    this.SynchronizeTranslationActivationSignatures();
  }

  /// <summary>
  /// Clears only translated presentation and transient operation state used by
  /// the disabled runtime.
  /// </summary>
  private void ClearTranslationPresentationForDisable()
  {
    this.hoverTooltipManager.Clear();
    this.rtlTexturePresentationService.Clear();
    this.ClearOverlay(this.talkOverlay, clearText: true);
    this.ClearOverlay(this.battleTalkOverlay, clearText: true);
    this.ClearOverlay(this.talkSubtitleOverlay, clearText: true);
    this.ClearOverlay(this.toastOverlay, clearText: true);
    this.ClearOverlay(this.errorToastOverlay, clearText: true);
    this.ClearOverlay(this.areaToastOverlay, clearText: true);
    this.ClearOverlay(this.classChangeToastOverlay, clearText: true);
    this.ClearOverlay(this.questToastOverlay, clearText: true);
    this.ClearOverlay(this.cutSceneSelectStringOverlay, clearText: true);
    this.ClearOverlay(this.textGimmickHintOverlay, clearText: true);
    this.ClearOverlay(this.chatBubbleOverlay, clearText: true);
    this.ClearOverlay(this.actionDetailOverlay, clearText: true);
    this.ClearOverlay(this.itemDetailOverlay, clearText: true);
    this.DisposeMiniTalkBubbleOverlays();
    DialogueTranslationSessionStore.Clear();
    QuestHoverTranslationCache.Clear();
  }

  /// <summary>
  /// Synchronizes refresh signatures after the coordinator has already applied
  /// the global transition.
  /// </summary>
  private void SynchronizeTranslationActivationSignatures()
  {
    this.translationRuntimeSignature = this.ComputeTranslationRuntimeSignature();
    this.namePlatePresentationSignature = this.ComputeNamePlatePresentationSignature();
    this.addonHandlerRegistrationSignature =
        this.ComputeAddonHandlerRegistrationSignature();
  }
}
