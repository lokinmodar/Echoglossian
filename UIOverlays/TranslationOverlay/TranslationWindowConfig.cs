// <copyright file="TranslationWindowConfig.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.UIOverlays.TranslationOverlay;

internal record TranslationWindowConfig(
    string DefaultTitle,
    float FontScale,
    float WidthMultiplier,
    float HeightMultiplier,
    Vector4 TextColor,
    Vector2 PosCorrection,
    bool ForceShowTitle = false,
    bool NoBackground = false,
    bool UseFixedWindowSize = false,
    bool ExpandWidthToFitText = false,
    float MaxAutoExpandedWidthMultiplier = 1.0f,
    float MinWidthViewportFraction = 0.0f,
    float MaxWidthViewportFraction = 0.0f)
{
  /// <summary>
  /// Creates a <see cref="TranslationWindowConfig"/> instance based on the provided <see cref="Config"/> for talk translations.
  /// </summary>
  /// <param name="config"></param>
  /// <returns></returns>
  public static TranslationWindowConfig FromConfigForTalk(Config config)
  {
    return new TranslationWindowConfig(
        DefaultTitle: "Talk translation",
        FontScale: config.TalkFontScale,
        WidthMultiplier: config.ImGuiTalkWindowWidthMult,
        HeightMultiplier: config.ImGuiTalkWindowHeightMult,
        TextColor: new Vector4(config.OverlayTalkTextColor.X, config.OverlayTalkTextColor.Y, config.OverlayTalkTextColor.Z, 1.0f),
        PosCorrection: config.ImGuiWindowPosCorrection,
        ForceShowTitle: config.TalkForceShowTitle,
        UseFixedWindowSize: true);
  }

  /// <summary>
  /// Creates a <see cref="TranslationWindowConfig"/> instance based on the provided <see cref="Config"/> for battle talk translations.
  /// </summary>
  /// <param name="config"></param>
  /// <returns></returns>
  public static TranslationWindowConfig FromConfigForBattleTalk(Config config)
  {
    return new TranslationWindowConfig(
        DefaultTitle: "BattleTalk translation",
        FontScale: config.BattleTalkFontScale,
        WidthMultiplier: config.ImGuiBattleTalkWindowWidthMult,
        HeightMultiplier: config.ImGuiBattleTalkWindowHeightMult,
        TextColor: new Vector4(config.OverlayBattleTalkTextColor.X, config.OverlayBattleTalkTextColor.Y, config.OverlayBattleTalkTextColor.Z, 1.0f),
        PosCorrection: config.ImGuiBattleTalkWindowPosCorrection,
        ForceShowTitle: config.BattleTalkForceShowTitle,
        UseFixedWindowSize: true,
        ExpandWidthToFitText: true,
        MaxAutoExpandedWidthMultiplier: 1.75f,
        MinWidthViewportFraction: 0.45f,
        MaxWidthViewportFraction: 0.80f);
  }

  /// <summary>
  /// Creates a <see cref="TranslationWindowConfig"/> instance based on the provided <see cref="Config"/> for talk subtitle translations.
  /// </summary>
  /// <param name="config"></param>
  /// <returns></returns>
  public static TranslationWindowConfig FromConfigTalkSubtitle(Config config)
  {
    return new TranslationWindowConfig(
        DefaultTitle: "Talk Subtitle translation",
        FontScale: config.TalkSubtitleFontScale,
        WidthMultiplier: config.ImGuiTalkSubtitleWindowWidthMult,
        HeightMultiplier: config.ImGuiTalkSubtitleWindowHeightMult,
        TextColor: new Vector4(config.OverlayTalkSubtitleTextColor.X, config.OverlayTalkSubtitleTextColor.Y, config.OverlayTalkSubtitleTextColor.Z, 1.0f),
        PosCorrection: config.ImGuiTalkSubtitleWindowPosCorrection,
        ForceShowTitle: config.TalkSubtitleForceShowTitle);
  }

  /// <summary>
  /// Creates a <see cref="TranslationWindowConfig"/> instance based on the provided <see cref="Config"/> for toast translations.
  /// </summary>
  /// <param name="config"></param>
  /// <returns></returns>
  public static TranslationWindowConfig FromConfigForToast(Config config)
  {
    return new TranslationWindowConfig(
        DefaultTitle: "Toast translation",
        FontScale: config.ToastFontScale,
        WidthMultiplier: config.ImGuiToastWindowWidthMult,
        HeightMultiplier: 2.0f,
        TextColor: new Vector4(config.OverlayToastTextColor.X, config.OverlayToastTextColor.Y, config.OverlayToastTextColor.Z, 1.0f),
        PosCorrection: config.ImGuiToastWindowPosCorrection,
        ForceShowTitle: config.ToastForceShowTitle);
  }

  /// <summary>
  /// Creates a <see cref="TranslationWindowConfig"/> instance based on the provided <see cref="Config"/> for error toast translations.
  /// </summary>
  /// <param name="config"></param>
  /// <returns></returns>
  public static TranslationWindowConfig FromConfigForErrorToast(Config config)
  {
    return new TranslationWindowConfig(
        DefaultTitle: "Error Toast translation",
        FontScale: config.ToastFontScale,
        WidthMultiplier: config.ImGuiToastWindowWidthMult,
        HeightMultiplier: 2.0f,
        TextColor: new Vector4(config.OverlayToastTextColor.X, config.OverlayToastTextColor.Y, config.OverlayToastTextColor.Z, 1.0f),
        PosCorrection: config.ImGuiToastWindowPosCorrection,
        ForceShowTitle: config.ToastForceShowTitle,
        NoBackground: true);
  }

  /// <summary>
  /// Creates a <see cref="TranslationWindowConfig"/> instance based on the provided <see cref="Config"/> for chat bubble translations.
  /// </summary>
  /// <param name="config"></param>
  /// <returns></returns>
  public static TranslationWindowConfig FromConfigForChatBubble(Config config)
  {
    return new TranslationWindowConfig(
        DefaultTitle: "ChatBubble translation",
        FontScale: config.TalkFontScale,
        WidthMultiplier: 1.0f,
        HeightMultiplier: 1.5f,
        TextColor: new Vector4(1f, 1f, 1f, 1f),
        PosCorrection: Vector2.Zero,
        ForceShowTitle: config.TalkForceShowTitle);
  }
}
