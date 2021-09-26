// <copyright file="Config.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Numerics;

using Dalamud.Configuration;

namespace Echoglossian
{
  public class Config : IPluginConfiguration
  {
    public string PluginCulture = "en";
    public int PluginCultureInt;

    public int Lang { get; set; } = 16;

    public int FontSize = 20;

    public bool ShowInCutscenes = true;

    public bool TranslateBattleTalk = true;
    public bool TranslateTalk = true;
    public bool TranslateToast = true;
    public bool TranslateNPCNames = true;
    public bool TranslateErrorToast = true;
    public bool TranslateQuestToast = true;
    public bool TranslateAreaToast = true;
    public bool TranslateClassChangeToast = true;
    public bool TranslateWideTextToast = true;
    public bool TranslateYesNoScreen = true;
    public bool TranslateCutSceneSelectString = true;
    public bool TranslateSelectString = true;
    public bool TranslateSelectOk = true;
    public bool TranslateToDoList = true;
    public bool TranslateScenarioTree = true;

    public bool UseImGui = false;
    public bool InvertTranslationAndOriginalTextsWhenUsingImGui = false;

    public int ChosenTransEngine = 0;

    public Vector2 ImGuiWindowPosCorrection = Vector2.Zero;
    public Vector2 ImGuiToastWindowPosCorrection = Vector2.Zero;
    public float ImGuiWindowWidthMult = 0.85f;
    public float ImGuiToastWindowWidthMult = 1.20f;
    public Vector3 OverlayTextColor = Vector3.Zero;

    public int Version { get; set; } = 0;
  }
}