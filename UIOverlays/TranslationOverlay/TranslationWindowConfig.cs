// <copyright file="TranslationWindowConfig.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.UIOverlays.TranslationOverlay;

/// <summary>
///     Identifies one overlay surface using a stable internal key that does not
///     depend on localized titles.
/// </summary>
internal enum TranslationOverlaySurfaceId
{
    /// <summary>Talk overlay.</summary>
    Talk,

    /// <summary>BattleTalk overlay.</summary>
    BattleTalk,

    /// <summary>TalkSubtitle overlay.</summary>
    TalkSubtitle,

    /// <summary>MiniTalk overlay.</summary>
    MiniTalk,

    /// <summary>CutSceneSelectString overlay.</summary>
    CutSceneSelectString,

    /// <summary>Text Gimmick Hint overlay.</summary>
    TextGimmickHint,

    /// <summary>Screen Info (_WideText) toast overlay.</summary>
    WideTextToast,

    /// <summary>Error toast overlay.</summary>
    ErrorToast,

    /// <summary>Area toast overlay.</summary>
    AreaToast,

    /// <summary>Class / Job change toast overlay.</summary>
    ClassChangeToast,

    /// <summary>Quest toast overlay.</summary>
    QuestToast,

    /// <summary>Chat bubble overlay.</summary>
    ChatBubble,

    /// <summary>ActionDetail overlay.</summary>
    ActionDetail,

    /// <summary>ItemDetail overlay.</summary>
    ItemDetail,
}

internal record TranslationWindowConfig(
    TranslationOverlaySurfaceId SurfaceId,
    string DefaultTitle,
    float FontScale,
    float WidthMultiplier,
    float HeightMultiplier,
    Vector4 TextColor,
    Vector2 PosCorrection,
    bool ForceShowTitle = false,
    float BackgroundOpacity = 1.0f,
    bool NoBackground = false,
    bool UseFixedWindowSize = false,
    bool CenterOnAddon = false,
    bool AutoSizeToTextWithMaxWidth = false,
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
        SurfaceId: TranslationOverlaySurfaceId.Talk,
        DefaultTitle: Resources.OverlayWindowTitleTalkTranslation,
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
        SurfaceId: TranslationOverlaySurfaceId.BattleTalk,
        DefaultTitle: Resources.OverlayWindowTitleBattleTalkTranslation,
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
        SurfaceId: TranslationOverlaySurfaceId.TalkSubtitle,
        DefaultTitle: Resources.OverlayWindowTitleTalkSubtitleTranslation,
        FontScale: config.TalkSubtitleFontScale,
        WidthMultiplier: config.ImGuiTalkSubtitleWindowWidthMult,
        HeightMultiplier: config.ImGuiTalkSubtitleWindowHeightMult,
        TextColor: new Vector4(config.OverlayTalkSubtitleTextColor.X, config.OverlayTalkSubtitleTextColor.Y, config.OverlayTalkSubtitleTextColor.Z, 1.0f),
        PosCorrection: config.ImGuiTalkSubtitleWindowPosCorrection,
        ForceShowTitle: false);
  }

  /// <summary>
  /// Creates a <see cref="TranslationWindowConfig"/> instance based on the provided <see cref="Config"/> for MiniTalk translations.
  /// </summary>
  /// <param name="config"></param>
  /// <returns></returns>
  public static TranslationWindowConfig FromConfigForMiniTalk(Config config)
  {
    return new TranslationWindowConfig(
        SurfaceId: TranslationOverlaySurfaceId.MiniTalk,
        DefaultTitle: Resources.OverlayWindowTitleMiniTalkTranslation,
        FontScale: config.MiniTalkFontScale,
        WidthMultiplier: config.ImGuiMiniTalkWindowWidthMult,
        HeightMultiplier: 1.0f,
        TextColor: new Vector4(config.OverlayMiniTalkTextColor.X, config.OverlayMiniTalkTextColor.Y, config.OverlayMiniTalkTextColor.Z, 1.0f),
        PosCorrection: config.ImGuiMiniTalkWindowPosCorrection,
        ForceShowTitle: false,
        BackgroundOpacity: config.MiniTalkBackgroundOpacity,
        NoBackground: config.MiniTalkBackgroundOpacity <= 0f,
        CenterOnAddon: true,
        AutoSizeToTextWithMaxWidth: true);
  }

  /// <summary>
  /// Creates a <see cref="TranslationWindowConfig"/> instance based on the provided <see cref="Config"/> for CutSceneSelectString overlays.
  /// </summary>
  /// <param name="config"></param>
  /// <returns></returns>
  public static TranslationWindowConfig FromConfigForCutSceneSelectString(Config config)
  {
    return new TranslationWindowConfig(
        SurfaceId: TranslationOverlaySurfaceId.CutSceneSelectString,
        DefaultTitle: Resources.OverlayWindowTitleCutSceneSelectStringTranslation,
        FontScale: config.CutSceneSelectStringFontScale,
        WidthMultiplier: config.ImGuiCutSceneSelectStringWindowWidthMult,
        HeightMultiplier: 1.5f,
        TextColor: new Vector4(config.OverlayCutSceneSelectStringTextColor.X, config.OverlayCutSceneSelectStringTextColor.Y, config.OverlayCutSceneSelectStringTextColor.Z, 1.0f),
        PosCorrection: config.ImGuiCutSceneSelectStringWindowPosCorrection,
        ForceShowTitle: true,
        BackgroundOpacity: config.CutSceneSelectStringBackgroundOpacity,
        NoBackground: config.CutSceneSelectStringBackgroundOpacity <= 0f,
        CenterOnAddon: true,
        AutoSizeToTextWithMaxWidth: true);
  }

  /// <summary>
  /// Creates a <see cref="TranslationWindowConfig"/> instance based on the provided <see cref="Config"/> for text gimmick hint translations.
  /// </summary>
  /// <param name="config"></param>
  /// <returns></returns>
  public static TranslationWindowConfig FromConfigForTextGimmickHint(Config config)
  {
    return new TranslationWindowConfig(
        SurfaceId: TranslationOverlaySurfaceId.TextGimmickHint,
        DefaultTitle: Resources.OverlayWindowTitleTextGimmickHintTranslation,
        FontScale: config.TextGimmickHintFontScale,
        WidthMultiplier: config.ImGuiTextGimmickHintWindowWidthMult,
        HeightMultiplier: config.ImGuiTextGimmickHintWindowHeightMult,
        TextColor: new Vector4(config.OverlayTextGimmickHintTextColor.X, config.OverlayTextGimmickHintTextColor.Y, config.OverlayTextGimmickHintTextColor.Z, 1.0f),
        PosCorrection: config.ImGuiTextGimmickHintWindowPosCorrection,
        ForceShowTitle: false,
        BackgroundOpacity: config.TextGimmickHintBackgroundOpacity,
        NoBackground: config.TextGimmickHintBackgroundOpacity <= 0f,
        CenterOnAddon: true,
        AutoSizeToTextWithMaxWidth: true);
  }

  /// <summary>
  /// Creates a <see cref="TranslationWindowConfig"/> instance based on the provided <see cref="Config"/> for toast translations.
  /// </summary>
  /// <param name="config"></param>
  /// <returns></returns>
  public static TranslationWindowConfig FromConfigForToast(Config config)
  {
    return FromConfigForWideTextToast(config);
  }

  /// <summary>
  /// Creates a <see cref="TranslationWindowConfig"/> instance based on the
  /// provided <see cref="Config"/> for Screen Info (_WideText) toast
  /// translations.
  /// </summary>
  /// <param name="config"></param>
  /// <returns></returns>
  public static TranslationWindowConfig FromConfigForWideTextToast(Config config)
  {
    return new TranslationWindowConfig(
        SurfaceId: TranslationOverlaySurfaceId.WideTextToast,
        DefaultTitle: Resources.OverlayWindowTitleWideTextToastTranslation,
        FontScale: config.WideTextToastFontScale,
        WidthMultiplier: config.ImGuiWideTextToastWindowWidthMult,
        HeightMultiplier: 2.0f,
        TextColor: new Vector4(config.OverlayWideTextToastTextColor.X, config.OverlayWideTextToastTextColor.Y, config.OverlayWideTextToastTextColor.Z, 1.0f),
        PosCorrection: config.ImGuiWideTextToastWindowPosCorrection,
        ForceShowTitle: false,
        BackgroundOpacity: config.WideTextToastBackgroundOpacity,
        CenterOnAddon: true,
        AutoSizeToTextWithMaxWidth: true);
  }

  /// <summary>
  /// Creates a <see cref="TranslationWindowConfig"/> instance based on the provided <see cref="Config"/> for error toast translations.
  /// </summary>
  /// <param name="config"></param>
  /// <returns></returns>
  public static TranslationWindowConfig FromConfigForErrorToast(Config config)
  {
    return new TranslationWindowConfig(
        SurfaceId: TranslationOverlaySurfaceId.ErrorToast,
        DefaultTitle: Resources.OverlayWindowTitleErrorToastTranslation,
        FontScale: config.ErrorToastFontScale,
        WidthMultiplier: config.ImGuiErrorToastWindowWidthMult,
        HeightMultiplier: 2.0f,
        TextColor: new Vector4(config.OverlayErrorToastTextColor.X, config.OverlayErrorToastTextColor.Y, config.OverlayErrorToastTextColor.Z, 1.0f),
        PosCorrection: config.ImGuiErrorToastWindowPosCorrection,
        ForceShowTitle: false,
        BackgroundOpacity: config.ErrorToastBackgroundOpacity,
        NoBackground: config.ErrorToastBackgroundOpacity <= 0f,
        CenterOnAddon: true,
        AutoSizeToTextWithMaxWidth: true);
  }

  /// <summary>
  /// Creates a <see cref="TranslationWindowConfig"/> instance based on the
  /// provided <see cref="Config"/> for area toast translations.
  /// </summary>
  /// <param name="config"></param>
  /// <returns></returns>
  public static TranslationWindowConfig FromConfigForAreaToast(Config config)
  {
    return new TranslationWindowConfig(
        SurfaceId: TranslationOverlaySurfaceId.AreaToast,
        DefaultTitle: Resources.OverlayWindowTitleAreaToastTranslation,
        FontScale: config.AreaToastFontScale,
        WidthMultiplier: config.ImGuiAreaToastWindowWidthMult,
        HeightMultiplier: 2.0f,
        TextColor: new Vector4(config.OverlayAreaToastTextColor.X, config.OverlayAreaToastTextColor.Y, config.OverlayAreaToastTextColor.Z, 1.0f),
        PosCorrection: config.ImGuiAreaToastWindowPosCorrection,
        ForceShowTitle: false,
        BackgroundOpacity: config.AreaToastBackgroundOpacity,
        NoBackground: config.AreaToastBackgroundOpacity <= 0f,
        CenterOnAddon: true,
        AutoSizeToTextWithMaxWidth: true);
  }

  /// <summary>
  /// Creates a <see cref="TranslationWindowConfig"/> instance based on the
  /// provided <see cref="Config"/> for class/job change toast translations.
  /// </summary>
  /// <param name="config"></param>
  /// <returns></returns>
  public static TranslationWindowConfig FromConfigForClassChangeToast(
      Config config)
  {
    return new TranslationWindowConfig(
        SurfaceId: TranslationOverlaySurfaceId.ClassChangeToast,
        DefaultTitle: Resources.OverlayWindowTitleClassJobToastTranslation,
        FontScale: config.ClassChangeToastFontScale,
        WidthMultiplier: config.ImGuiClassChangeToastWindowWidthMult,
        HeightMultiplier: 2.0f,
        TextColor: new Vector4(config.OverlayClassChangeToastTextColor.X, config.OverlayClassChangeToastTextColor.Y, config.OverlayClassChangeToastTextColor.Z, 1.0f),
        PosCorrection: config.ImGuiClassChangeToastWindowPosCorrection,
        ForceShowTitle: false,
        BackgroundOpacity: config.ClassChangeToastBackgroundOpacity,
        NoBackground: config.ClassChangeToastBackgroundOpacity <= 0f,
        CenterOnAddon: true,
        AutoSizeToTextWithMaxWidth: true);
  }

  /// <summary>
  /// Creates a <see cref="TranslationWindowConfig"/> instance based on the
  /// provided <see cref="Config"/> for quest toast translations.
  /// </summary>
  /// <param name="config"></param>
  /// <returns></returns>
  public static TranslationWindowConfig FromConfigForQuestToast(Config config)
  {
    return new TranslationWindowConfig(
        SurfaceId: TranslationOverlaySurfaceId.QuestToast,
        DefaultTitle: Resources.OverlayWindowTitleQuestToastTranslation,
        FontScale: config.QuestToastFontScale,
        WidthMultiplier: config.ImGuiQuestToastWindowWidthMult,
        HeightMultiplier: 2.0f,
        TextColor: new Vector4(config.OverlayQuestToastTextColor.X, config.OverlayQuestToastTextColor.Y, config.OverlayQuestToastTextColor.Z, 1.0f),
        PosCorrection: config.ImGuiQuestToastWindowPosCorrection,
        ForceShowTitle: false,
        BackgroundOpacity: config.QuestToastBackgroundOpacity,
        NoBackground: config.QuestToastBackgroundOpacity <= 0f,
        AutoSizeToTextWithMaxWidth: true);
  }

  /// <summary>
  /// Creates a <see cref="TranslationWindowConfig"/> instance based on the provided <see cref="Config"/> for chat bubble translations.
  /// </summary>
  /// <param name="config"></param>
  /// <returns></returns>
  public static TranslationWindowConfig FromConfigForChatBubble(Config config)
  {
    return new TranslationWindowConfig(
        SurfaceId: TranslationOverlaySurfaceId.ChatBubble,
        DefaultTitle: Resources.OverlayWindowTitleChatBubbleTranslation,
        FontScale: config.TalkFontScale,
        WidthMultiplier: 1.0f,
        HeightMultiplier: 1.5f,
        TextColor: new Vector4(1f, 1f, 1f, 1f),
        PosCorrection: Vector2.Zero,
        ForceShowTitle: config.TalkForceShowTitle);
  }
}
