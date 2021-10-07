// <copyright file="UiFontHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.IO;

using Dalamud.Logging;
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
#if DEBUG
      var pathCorrection = MovePathUp(this.ConfigDir, 2);
      var fontFile2 = $@"{pathCorrection}{Path.DirectorySeparatorChar}installedPlugins{Path.DirectorySeparatorChar}Echoglossian{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-Regular.ttf";
      PluginLog.LogVerbose($"Font file in DEBUG Mode using path correction: {fontFile2}");


      var fontFile = $@"{Path.GetFullPath(Path.GetDirectoryName(this.AssemblyLocation)!)}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-Regular.ttf";
      PluginLog.LogVerbose($"Font file in Debug Mode: {fontFile}");
#else
      var pathCorrection = MovePathUp(this.ConfigDir, 1);
      var fontFile = $@"{pathCorrection}{Path.DirectorySeparatorChar}installedPlugins{Path.DirectorySeparatorChar}Echoglossian{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-Regular.ttf";
      PluginLog.LogVerbose($"Font file in Prod Mode: {fontFile}");
#endif
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