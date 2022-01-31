// <copyright file="Config.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Numerics;
using Dalamud.Configuration;

namespace Echoglossian
{
  public class Config : IPluginConfiguration
  {
    public float BattleTalkFontScale = 1;

    public int ChosenTransEngine = 0;
    public string DefaultPluginCulture = "en";

    [NonSerialized] public long FontChangeTime = DateTime.Now.Ticks;

    public float FontScale = 1;

    public int FontSize = 24;

    public float ImGuiBattleTalkWindowHeightMult = 1f;

    public Vector2 ImGuiBattleTalkWindowPosCorrection = new(0.0f, 0.0f);

    public float ImGuiBattleTalkWindowWidthMult = 1f;

    public float ImGuiTalkWindowHeightMult = 1f;

    public float ImGuiTalkWindowWidthMult = 1f;

    public Vector2 ImGuiToastWindowPosCorrection = new(0.0f, 0.0f);

    public float ImGuiToastWindowWidthMult = 1f;

    public Vector2 ImGuiWindowPosCorrection = new(0.0f, 0.0f);

    public int Lang = 28;

    public Vector3 OverlayBattleTalkTextColor = new(1.0f, 1.0f, 1.0f);

    public Vector3 OverlayTextColor = new(1.0f, 1.0f, 1.0f);

    public bool PluginAssetsDownloaded = false;

    public int PluginCultureInt;

    public bool ShowInCutscenes = true;

    public bool SwapTextsUsingImGui = false;

    public bool Translate = false;

    public bool TranslateAreaToast = false;

    public bool TranslateBattleTalk = false;

    public bool TranslateClassChangeToast = false;

    public bool TranslateCutSceneSelectString = false;

    public bool TranslateErrorToast = false;

    public bool TranslateJournal = false;

    public bool TranslateNpcNames = false;

    public bool TranslateQuestToast = false;

    public bool TranslateScenarioTree = false;

    public bool TranslateSelectOk = false;

    public bool TranslateSelectString = false;

    public bool TranslateTalk = false;

    public bool TranslateTalkSubtitle = false;

    public bool TranslateToast = false;

    public bool TranslateToDoList = false;

    public bool TranslateTooltips = false;

    public bool TranslateWideTextToast = false;

    public bool TranslateYesNoScreen = false;

    public bool UseImGuiForBattleTalk = false;

    // public bool UseImGui = false; //deprecated
    public bool UseImGuiForTalk = false;

    public bool UseImGuiForToasts = false;

    public int Version { get; set; } = 4;
  }
}