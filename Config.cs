// <copyright file="Config.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
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

    public int Lang { get; set; } = 16;

    public List<int> ChosenLanguages { get; set; } = new();

    /// <inheritdoc/>
    public int Version { get; set; } = 0;
  }
}