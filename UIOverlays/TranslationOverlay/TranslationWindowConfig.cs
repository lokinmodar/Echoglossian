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
    bool NoBackground = false)
{
  public static TranslationWindowConfig FromConfigForTalk(Config config)
  {
    return new TranslationWindowConfig(
        "Talk",
        config.ImGuiTalkWindowWidthMult,
        config.ImGuiTalkWindowHeightMult,
        new Vector4(config.OverlayTalkTextColor.X, config.OverlayTalkTextColor.Y, config.OverlayTalkTextColor.Z, 1.0f),
        config.ImGuiWindowPosCorrection,
        config.ForceShowTitle,
        false);
  }

  public static TranslationWindowConfig FromConfigForBattleTalk(Config config)
  {
    return new TranslationWindowConfig(
        "BattleTalk",
        config.ImGuiBattleTalkWindowWidthMult,
        config.ImGuiBattleTalkWindowHeightMult,
        new Vector4(config.OverlayBattleTalkTextColor.X, config.OverlayBattleTalkTextColor.Y, config.OverlayBattleTalkTextColor.Z, 1.0f),
        config.ImGuiBattleTalkWindowPosCorrection,
        config.ForceShowTitle,
        false);
  }

  public static TranslationWindowConfig FromConfigForTalkSubtitle(Config config)
  {
    return new TranslationWindowConfig(
        "TalkSubtitle",
        config.ImGuiTalkSubtitleWindowWidthMult,
        config.ImGuiTalkSubtitleWindowHeightMult,
        new Vector4(config.OverlayTalkSubtitleTextColor.X, config.OverlayTalkSubtitleTextColor.Y, config.OverlayTalkSubtitleTextColor.Z, 1.0f),
        config.ImGuiTalkSubtitleWindowPosCorrection,
        config.ForceShowTitle,
        false);
  }

  public static TranslationWindowConfig FromConfigForToast(Config config)
  {
    return new TranslationWindowConfig(
        "Toast",
        config.ImGuiToastWindowWidthMult,
        1f,
        new Vector4(config.OverlayToastTextColor.X, config.OverlayToastTextColor.Y, config.OverlayToastTextColor.Z, 1.0f),
        config.ImGuiToastWindowPosCorrection,
        config.ForceShowTitle,
        false);
  }
}
