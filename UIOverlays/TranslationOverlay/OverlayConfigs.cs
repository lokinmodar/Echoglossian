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
    private readonly TranslationOverlay ToastOverlay = new();
    private readonly TranslationOverlay ErrorToastOverlay = new();
    private readonly TranslationOverlay ChatBubbleOverlay = new();

    // Configs
    private static readonly TranslationWindowConfig TalkConfig = new(
        DefaultTitle: "Talk translation",
        WidthMultiplier: 1.0f,
        HeightMultiplier: 1.0f,
        TextColor: new Vector4(1f, 1f, 1f, 1f),
        PosCorrection: Vector2.Zero
    );

    private static readonly TranslationWindowConfig BattleTalkConfig = new(
        DefaultTitle: "BattleTalk translation",
        WidthMultiplier: 1.5f,
        HeightMultiplier: 2.5f,
        TextColor: new Vector4(1f, 1f, 1f, 1f),
        PosCorrection: Vector2.Zero,
        ForceShowTitle: true
    );

    private static readonly TranslationWindowConfig ToastConfig = new(
        DefaultTitle: "Toast Translation",
        WidthMultiplier: 1.0f,
        HeightMultiplier: 2.0f,
        TextColor: new Vector4(1f, 1f, 1f, 1f),
        PosCorrection: Vector2.Zero
    );

    private static readonly TranslationWindowConfig ErrorToastConfig = new(
        DefaultTitle: "Error Toast Translation",
        WidthMultiplier: 1.0f,
        HeightMultiplier: 2.0f,
        TextColor: new Vector4(1f, 1f, 1f, 1f),
        PosCorrection: Vector2.Zero,
        NoBackground: true
    );

    private static readonly TranslationWindowConfig ChatBubbleConfig = new(
        DefaultTitle: "ChatBubble translation",
        WidthMultiplier: 1.0f,
        HeightMultiplier: 1.5f,
        TextColor: new Vector4(1f, 1f, 1f, 1f),
        PosCorrection: Vector2.Zero
    );

    /// <summary>
    /// List of registered overlays.
    /// </summary>
    private readonly List<OverlayRegistration> registeredOverlays = new();

    /// <summary>
    /// Registers the overlays with their respective configurations.
    /// </summary>
    private void RegisterOverlays()
    {
      PluginLog.Debug("Registering overlays...");

      this.registeredOverlays.Add(new OverlayRegistration(
          this.TalkOverlay,
          TalkConfig
      ));

      this.registeredOverlays.Add(new OverlayRegistration(
          this.BattleTalkOverlay,
          BattleTalkConfig
      ));

      this.registeredOverlays.Add(new OverlayRegistration(
          this.ToastOverlay,
          ToastConfig
      ));

      this.registeredOverlays.Add(new OverlayRegistration(
          this.ErrorToastOverlay,
          ErrorToastConfig
      ));

      this.registeredOverlays.Add(new OverlayRegistration(
          this.ChatBubbleOverlay,
          ChatBubbleConfig
      ));

      PluginLog.Debug($"Overlays registered: {this.registeredOverlays.Count} ");
    }
  }
}
