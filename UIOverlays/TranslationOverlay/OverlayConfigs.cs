// <copyright file="OverlayConfigs.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian
{
  public partial class Echoglossian
  {
    // Overlays
    private readonly TranslationOverlay TalkOverlay = new();
    private readonly TranslationOverlay BattleTalkOverlay = new();
    private readonly TranslationOverlay TalkSubtitleOverlay = new();
    private readonly TranslationOverlay ToastOverlay = new();
    private readonly TranslationOverlay ErrorToastOverlay = new();
    private readonly TranslationOverlay ChatBubbleOverlay = new();

    // List of registered overlays
    private readonly List<OverlayRegistration> registeredOverlays = new();

    /// <summary>
    /// Registers the overlays with their respective configurations using current plugin config values.
    /// </summary>
    private void RegisterOverlays()
    {
      PluginLog.Debug("Registering overlays...");

      this.registeredOverlays.Add(new OverlayRegistration(
          this.TalkOverlay,
          TranslationWindowConfig.FromConfigForTalk(this.configuration)
      ));

      this.registeredOverlays.Add(new OverlayRegistration(
          this.BattleTalkOverlay,
          TranslationWindowConfig.FromConfigForBattleTalk(this.configuration)
      ));

      this.registeredOverlays.Add(new OverlayRegistration(this.TalkSubtitleOverlay, TranslationWindowConfig.FromConfigTalkSubtitle(this.configuration)));

      this.registeredOverlays.Add(new OverlayRegistration(
          this.ToastOverlay,
          TranslationWindowConfig.FromConfigForToast(this.configuration)
      ));

      this.registeredOverlays.Add(new OverlayRegistration(
          this.ErrorToastOverlay,
          TranslationWindowConfig.FromConfigForErrorToast(this.configuration)
      ));

      this.registeredOverlays.Add(new OverlayRegistration(
          this.ChatBubbleOverlay,
          TranslationWindowConfig.FromConfigForChatBubble(this.configuration)
      ));

      PluginLog.Debug($"Overlays registered: {this.registeredOverlays.Count} ");
    }
  }
}
