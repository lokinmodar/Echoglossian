// <copyright file="PluginRuntimeUi.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

public partial class Echoglossian
{
  /// <summary>
  /// Updates the plugin's state on each tick.
  /// </summary>
  private void Tick(IFramework tFramework)
  {
#if DEBUG
    this.addonProbeWatch?.Tick();
    if (this.addonProbeWatch?.IsDisposed == true)
    {
      this.addonProbeWatch = null;
    }
#endif

    this.ApplyPendingRuntimeConfigurationChanges();

    if (!this.configuration.Translate)
    {
      return;
    }

    this.TickAcceptedQuestPrefetch();
    this.TickActionDetailPrefetch();
    this.TickTraitDetailPrefetch();
    this.TickReferenceTextPrefetch();
    this.TickItemDetailPrefetch();

    switch (NativeUI.Helpers.TranslationDisplayModeHelper.UsesOverlayPresentation(
                this.configuration.TalkTranslationDisplayMode,
                this.configuration.OverlayOnlyLanguage) ||
            NativeUI.Helpers.TranslationDisplayModeHelper.UsesOverlayPresentation(
                this.configuration.BattleTalkTranslationDisplayMode,
                this.configuration.OverlayOnlyLanguage) ||
            this.configuration.OverlayOnlyLanguage ||
            NativeUI.Helpers.TranslationDisplayModeHelper.UsesOverlayPresentation(
                this.configuration.MiniTalkTranslationDisplayMode,
                this.configuration.OverlayOnlyLanguage) ||
            NativeUI.Helpers.TranslationDisplayModeHelper.UsesOverlayPresentation(
                this.configuration.CutSceneSelectStringTranslationDisplayMode,
                this.configuration.OverlayOnlyLanguage) ||
            NativeUI.Helpers.TranslationDisplayModeHelper.UsesOverlayPresentation(
                this.configuration.WideTextToastTranslationDisplayMode,
                this.configuration.OverlayOnlyLanguage) ||
            NativeUI.Helpers.TranslationDisplayModeHelper.UsesOverlayPresentation(
                this.configuration.ErrorToastTranslationDisplayMode,
                this.configuration.OverlayOnlyLanguage) ||
            NativeUI.Helpers.TranslationDisplayModeHelper.UsesOverlayPresentation(
                this.configuration.AreaToastTranslationDisplayMode,
                this.configuration.OverlayOnlyLanguage) ||
            NativeUI.Helpers.TranslationDisplayModeHelper.UsesOverlayPresentation(
                this.configuration.ClassChangeToastTranslationDisplayMode,
                this.configuration.OverlayOnlyLanguage) ||
            NativeUI.Helpers.TranslationDisplayModeHelper.UsesOverlayPresentation(
                this.configuration.QuestToastTranslationDisplayMode,
                this.configuration.OverlayOnlyLanguage))
    {
      case true when !this.FontLoaded || this.FontLoadFailed:
        return;
      case true:
        return;
    }
  }

  /// <summary>
  ///     Builds the UI for the plugin.
  /// </summary>
  private void BuildUi()
  {
    if (this.config)
    {
      this.EchoglossianConfigUi();
    }

    if (AssetsManager.RequiresDownloadedAssets(SelectedLanguage) &&
        !AssetsManager.PluginAssetsDownloaded)
    {
      this.ResetStructuredTooltipUiRuntime();
      return;
    }

    if (this.configuration.FontChangeTime > 0)
    {
      if (DateTime.Now.Ticks - 10000000 >
          this.configuration.FontChangeTime)
      {
        this.configuration.FontChangeTime = 0;
        this.FontLoadFailed = false;
      }
    }

    this.UpdateStructuredTooltipUiRuntime();

    if (!this.configuration.Translate)
    {
      return;
    }

    foreach (var overlayRegistration in this.registeredOverlays)
    {
      if (overlayRegistration.IsEnabled is not null &&
          !overlayRegistration.IsEnabled())
      {
        continue;
      }

      if (overlayRegistration.SyncBeforeDraw is not null &&
          !overlayRegistration.SyncBeforeDraw())
      {
        continue;
      }

      overlayRegistration.Overlay.Semaphore.Wait();
      var shouldDisplay = overlayRegistration.Overlay.Display;
      overlayRegistration.Overlay.Semaphore.Release();

      if (!shouldDisplay)
      {
        continue;
      }

      // Title is now resolved inside DrawTranslationWindow, so no need to pass customTitle
      this.DrawTranslationWindow(
          overlayRegistration.Overlay,
          overlayRegistration.Config,
          overlayRegistration.CustomTitleGetter?.Invoke());
    }

    this.DrawMiniTalkBubbleOverlays();
    this.DrawStructuredTooltipOverlays();
    var shouldDrawHoverTooltips = this.ShouldDrawHoverTooltips;

    if (shouldDrawHoverTooltips)
    {
      this.hoverTooltipManager.Draw();
    }
    else
    {
      this.hoverTooltipManager.Clear();
    }
  }

  /// <summary>
  /// Draws the database editor window.
  /// </summary>
  private void DrawDbEditorWindow()
  {
    this.dbEditorWindow?.Draw();
  }

  /// <summary>
  /// Open the Echoglossian DB Editor window when the command is executed.
  /// </summary>
  /// <param name="command">Command name.</param>
  /// <param name="args">Command arguments.</param>
  private void OnEgloDbEditorCommand(string command, string args)
  {
    this.dbEditorWindow?.IsOpen = true;
  }

  /// <summary>
  /// Sets the configuration flag to true when the config window is opened.
  /// </summary>
  private void ConfigWindow()
  {
    this.config = true;
  }

  /// <summary>
  /// Sets the configuration flag to true when the command is executed.
  /// </summary>
  /// <param name="command">The command that triggered the execution.</param>
  /// <param name="arguments">Arguments associated with the command.</param>
  private void Command(string command, string arguments)
  {
    this.config = true;
  }
}
