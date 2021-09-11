// <copyright file="Config.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Generic;
using System.Numerics;

using Dalamud.Configuration;

namespace Echoglossian
{
  public class Config : IPluginConfiguration
  {
    public Vector2 ImGuiWindowPosCorrection = Vector2.Zero;
    public float ImGuiWindowWidthMult = 0.85f;
    public bool TranslateBattleTalk = true;
    public bool TranslateTalk = true;

    public bool UseImGui = false;
    public bool ShowInCutscenes = true;

    public int Lang { get; set; } = 16;

    public int FontSize = 20;

    public List<int> ChosenLanguages { get; set; } = new();

    public int Version { get; set; } = 0;
  }
}