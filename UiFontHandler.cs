// <copyright file="UiFontHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.IO;
using System.Numerics;

using Dalamud.Interface;
using Dalamud.Logging;
using Dalamud.Utility;
using Echoglossian.Properties;
using ImGuiNET;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    public bool FontLoaded;
    public bool FontLoadFailed;
    public ImFontPtr UiFont;

    private void LoadFont(/*string fontFileName,int imguiFontSize */)
    {
      // TODO: Get font by languageint
      PluginLog.LogVerbose("Inside LoadFont method");
      var fontFile = $@"{Path.GetFullPath(Path.GetDirectoryName(this.AssemblyLocation)!)}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-Regular.ttf";
      this.FontLoaded = false;
      if (File.Exists(fontFile))
      {
        try
        {
          var neededGlyphs = ImGui.GetIO().Fonts.GetGlyphRangesVietnamese();
#if DEBUG
          PluginLog.Debug($"Glyphs pointer: {neededGlyphs}");
#endif
          this.UiFont = ImGui.GetIO().Fonts.AddFontFromFileTTF(fontFile, this.configuration.FontSize /*imguiFontSize*/, null, neededGlyphs);
#if DEBUG
          PluginLog.Debug($"UiFont pointer: {this.UiFont}");
#endif
          this.FontLoaded = true;
#if DEBUG
          PluginLog.Debug($"Font loaded? {this.FontLoaded}");
#endif
        }
        catch (Exception ex)
        {
          PluginLog.Log($"Font failed to load. {fontFile}");
          PluginLog.Log(ex.ToString());
          this.FontLoadFailed = true;
        }
      }
      else
      {
        PluginLog.Log($"Font doesn't exist. {fontFile}");
        this.FontLoadFailed = true;
      }
    }
  }
}