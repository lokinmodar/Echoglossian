// <copyright file="UiFontHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Unicode;
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
#if DEBUG
      PluginLog.LogVerbose("Inside LoadFont method");

      var fontFile = $@"{this.pluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-Regular.ttf";
      PluginLog.LogVerbose($"Font file in DEBUG Mode: {fontFile}");

#else

      var fontFile = $@"{this.pluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-Regular.ttf";
      PluginLog.LogVerbose($"Font file in Prod Mode: {fontFile}");
#endif
      this.FontLoaded = false;
      if (File.Exists(fontFile))
      {
        //GCHandle rangeHandle = GCHandle.Alloc(new ushort[] { Convert.ToUInt16("0x0000", 16), Convert.ToUInt16("0xFFFF", 16), 0 }, GCHandleType.Pinned);
        try
        {
          unsafe
          {
            var io = ImGui.GetIO();
            /*ImVector<ImWchar>*/
            List<ushort> chars = new List<ushort>();

            // if (this.configuration.Lang == )
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesVietnamese());
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesCyrillic());
            // this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts. );

            var addChars = string.Join(string.Empty, chars.Select(c => new string((char)c, 2))).Select(c => (ushort)c).ToArray();
            chars.AddRange(addChars);


            chars.Add(0);

            var arr = chars.ToArray();

            fixed (ushort* ptr = &arr[0])
            {
              this.UiFont = ImGui.GetIO().Fonts.AddFontFromFileTTF(fontFile, this.configuration.FontSize /*imguiFontSize*/, null, new IntPtr((void*)ptr));
            }




            // var neededGlyphs = ImGui.GetIO().Fonts.GetGlyphRangesVietnamese();
            // var neededGlyps2 = ImGui.GetIO().Fonts.GetGlyphRangesCyrillic();
            // var neededGlyphs3 = ImGui.GetIO().Fonts.GetGlyphRangesDefault();

#if DEBUG
            // PluginLog.Debug($"Glyphs pointer: {neededGlyphs}");
#endif
            // this.UiFont = ImGui.GetIO().Fonts.AddFontFromFileTTF(fontFile, this.configuration.FontSize /*imguiFontSize*/, null, rangeHandle.AddrOfPinnedObject());
#if DEBUG
            PluginLog.Debug($"UiFont pointer: {this.UiFont}");
#endif
            this.FontLoaded = true;
#if DEBUG
            PluginLog.Debug($"Font loaded? {this.FontLoaded}");
#endif
          }
        }
        catch (Exception ex)
        {
          PluginLog.Log($"Font failed to load. {fontFile}");
          PluginLog.Log(ex.ToString());
          this.FontLoadFailed = true;
        }
        /*        finally
                {
                  if (rangeHandle.IsAllocated)
                  {
                    rangeHandle.Free();
                  }
                }*/
      }
      else
      {
        PluginLog.Log($"Font doesn't exist. {fontFile}");
        this.FontLoadFailed = true;
      }
    }


    private unsafe void AddCharsFromIntPtr(List<ushort> chars, ushort* ptr)
    {
      while (*ptr != 0)
      {
        chars.Add(*ptr);
        ptr++;
      }
    }


  }
}