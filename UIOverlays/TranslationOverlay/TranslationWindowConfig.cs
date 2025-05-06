// <copyright file="TranslationWindowConfig.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.UIOverlays.TranslationOverlay;

internal record TranslationWindowConfig(
    string DefaultTitle,
    float WidthMultiplier,
    float HeightMultiplier,
    Vector4 TextColor,
    Vector2 PosCorrection,
    bool ForceShowTitle = false,
    bool NoBackground = false
)
{
  public static TranslationWindowConfig FromConfigForTalk(Config config)
  {
    return new TranslationWindowConfig(
        DefaultTitle: "Talk translation",
        WidthMultiplier: config.ImGuiTalkWindowWidthMult,
        HeightMultiplier: config.ImGuiTalkWindowHeightMult,
        TextColor: new Vector4(config.OverlayTalkTextColor.X, config.OverlayTalkTextColor.Y, config.OverlayTalkTextColor.Z, 1.0f),
        PosCorrection: config.ImGuiWindowPosCorrection,
        ForceShowTitle: config.ForceShowTitle
    );
  }

  public static TranslationWindowConfig FromConfigForBattleTalk(Config config)
  {
    return new TranslationWindowConfig(
        DefaultTitle: "BattleTalk translation",
        WidthMultiplier: config.ImGuiBattleTalkWindowWidthMult,
        HeightMultiplier: config.ImGuiBattleTalkWindowHeightMult,
        TextColor: new Vector4(config.OverlayBattleTalkTextColor.X, config.OverlayBattleTalkTextColor.Y, config.OverlayBattleTalkTextColor.Z, 1.0f),
        PosCorrection: config.ImGuiBattleTalkWindowPosCorrection,
        ForceShowTitle: config.ForceShowTitle
    );
  }

  public static TranslationWindowConfig FromConfigForToast(Config config)
  {
    return new TranslationWindowConfig(
        DefaultTitle: "Toast translation",
        WidthMultiplier: config.ImGuiToastWindowWidthMult,
        HeightMultiplier: 2.0f,
        TextColor: new Vector4(config.OverlayToastTextColor.X, config.OverlayToastTextColor.Y, config.OverlayToastTextColor.Z, 1.0f),
        PosCorrection: config.ImGuiToastWindowPosCorrection,
        ForceShowTitle: config.ForceShowTitle
    );
  }

  public static TranslationWindowConfig FromConfigForErrorToast(Config config)
  {
    return new TranslationWindowConfig(
        DefaultTitle: "Error Toast translation",
        WidthMultiplier: config.ImGuiToastWindowWidthMult,
        HeightMultiplier: 2.0f,
        TextColor: new Vector4(config.OverlayToastTextColor.X, config.OverlayToastTextColor.Y, config.OverlayToastTextColor.Z, 1.0f),
        PosCorrection: config.ImGuiToastWindowPosCorrection,
        ForceShowTitle: config.ForceShowTitle,
        NoBackground: true
    );
  }

  public static TranslationWindowConfig FromConfigForChatBubble(Config config)
  {
    return new TranslationWindowConfig(
        DefaultTitle: "ChatBubble translation",
        WidthMultiplier: 1.0f,
        HeightMultiplier: 1.5f,
        TextColor: new Vector4(1f, 1f, 1f, 1f),
        PosCorrection: Vector2.Zero,
        ForceShowTitle: config.ForceShowTitle
    );
  }
}
