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
    this.addonProbeWatch?.Tick();
    if (this.addonProbeWatch?.IsDisposed == true)
    {
      this.addonProbeWatch = null;
    }

    if (!this.configuration.Translate)
    {
      return;
    }

    switch (this.configuration.UseImGuiForTalk ||
            this.configuration.UseImGuiForBattleTalk ||
            this.configuration.OverlayOnlyLanguage ||
            this.configuration.UseImGuiForMiniTalk ||
            this.configuration.UseImGuiForCutSceneSelectString ||
            this.configuration.UseImGuiForWideTextToast ||
            this.configuration.UseImGuiForErrorToast ||
            this.configuration.UseImGuiForAreaToast ||
            this.configuration.UseImGuiForClassChangeToast ||
            this.configuration.UseImGuiForQuestToast)
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
    if (!this.configuration.PluginAssetsDownloaded)
    {
      return;
    }

    if (this.config)
    {
      this.EchoglossianConfigUi();
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
    if (this.configuration.TranslateTooltips)
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
