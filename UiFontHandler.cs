// <copyright file="UiFontHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Dalamud.Logging;
using ImGuiNET;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    public bool FontLoaded;
    public bool FontLoadFailed;
    public ImFontPtr UiFont;

    public bool ConfigFontLoaded;
    public bool ConfigFontLoadFailed;
    public ImFontPtr ConfigUiFont;

    private string specialFontFileName = string.Empty;
    private string fontFileName = "NotoSans-Medium.ttf";
    private string scriptCharList = string.Empty;

    private ushort[] glyphRangeMainText = null;
    private ushort[] glyphRangeConfigText = null;

    private void AdjustLanguageForFontBuild()
    {
#if DEBUG
      PluginLog.Debug("Inside AdjustLanguageForFontBuild method");
#endif

      LanguageInfo lang = this.LanguagesDictionary[this.configuration.Lang];
      this.specialFontFileName = lang.FontName;
      this.scriptCharList = lang.ExclusiveCharsToAdd;
    }

    private unsafe void LoadFont()
    {
      this.AdjustLanguageForFontBuild();

      string specialFontFilePath = $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}{this.specialFontFileName}";
      string fontFilePath = $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}{this.fontFileName}";
      string symbolsFontFilePath =
        $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}symbols.ttf";
      string dummyFontFilePath = $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-Regular.ttf";
#if DEBUG
      PluginLog.LogWarning("Inside LoadFont method");
      PluginLog.LogWarning($"Font file in DEBUG Mode: {specialFontFilePath}");
#endif
      this.FontLoaded = false;
      if (File.Exists(specialFontFilePath) || File.Exists(fontFilePath))
      {
        ImFontConfigPtr fontConfig = null;
        try
        {
          ImGuiIOPtr io = ImGui.GetIO();
          List<ushort> chars = new();

          ImFontGlyphRangesBuilderPtr builder = new ImFontGlyphRangesBuilderPtr(ImGuiNative.ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder());
          builder.AddText(this.CharsToAddToAll);
          builder.AddText(this.scriptCharList);

          foreach (char c in this.CharsToAddToAll)
          {
            builder.AddChar(c);
          }

          foreach (char c in this.scriptCharList)
          {
            builder.AddChar(c);
          }

          foreach (char c in this.PuaCharCodes)
          {
            builder.AddChar(c);
          }

          foreach (char c in this.PuaChars)
          {
            builder.AddChar(c);
          }

          /*var fontPathGame = Path.Combine(dalamud.AssetDirectory.FullName, "UIRes", "gamesym.ttf");

          if (!File.Exists(fontPathGame))
            ShowFontError(fontPathGame);

          var gameRangeHandle = GCHandle.Alloc(
            new ushort[]
            {
              0xE020,
              0xE0DB,
              0,
            },
            GCHandleType.Pinned);

          ImGui.GetIO().Fonts.AddFontFromFileTTF(fontPathGame, 17.0f, fontConfig, gameRangeHandle.AddrOfPinnedObject());*/

          builder.BuildRanges(out ImVector ranges);

          this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesDefault());
          this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesVietnamese());
          this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesCyrillic());
          if (this.configuration.Lang is 16)
          {
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesChineseSimplifiedCommon());
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesChineseFull());
          }

          if (this.configuration.Lang is 22 or 21)
          {
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesChineseSimplifiedCommon());
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesChineseFull());
          }

          if (this.configuration.Lang is 56)
          {
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesKorean());
          }

          if (this.configuration.Lang is 50)
          {
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesJapanese());
          }

          if (this.configuration.Lang is 103)
          {
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesThai());
          }

          this.AddCharsFromIntPtr(chars, (ushort*)ranges.Data);

          ushort[] addChars = string.Join(string.Empty, chars.Select(c => new string((char)c, 2))).Select(c => (ushort)c).ToArray();
          chars.AddRange(addChars);

          chars.Add(0);

          fontConfig = ImGuiNative.ImFontConfig_ImFontConfig();
          fontConfig.OversampleH = 1;
          fontConfig.OversampleV = 1;
          this.glyphRangeMainText = chars.ToArray();
          fixed (ushort* ptr = &this.glyphRangeMainText[0])
          {
            fontConfig.GlyphRanges = (IntPtr)ptr;
          }

          this.UiFont = ImGui.GetIO().Fonts.AddFontFromFileTTF(dummyFontFilePath, this.configuration.FontSize, fontConfig);

          fontConfig.MergeMode = true;
          ImGui.GetIO().Fonts.AddFontFromFileTTF(symbolsFontFilePath, this.configuration.FontSize, fontConfig);
          ImGui.GetIO().Fonts.AddFontFromFileTTF(fontFilePath, this.configuration.FontSize, fontConfig);
          if (specialFontFilePath != string.Empty)
          {
            ImGui.GetIO().Fonts.AddFontFromFileTTF(specialFontFilePath, this.configuration.FontSize, fontConfig);
          }

#if DEBUG
          PluginLog.Debug($"UiFont Data size: {ImGui.GetIO().Fonts.Fonts.Size}");
#endif
          this.FontLoaded = true;
#if DEBUG
          PluginLog.Debug($"Font loaded? {this.FontLoaded}");
#endif
        }
        catch (Exception ex)
        {
          PluginLog.Log($"Special Font failed to load. {specialFontFilePath}");
          PluginLog.Log(ex.ToString());
          this.FontLoadFailed = true;
        }
        finally
        {
          if (fontConfig.NativePtr != null)
          {
            fontConfig.Destroy();
          }
        }
      }
      else
      {
        PluginLog.Log($"Special Font doesn't exist. {specialFontFilePath}");
        this.FontLoadFailed = true;
      }
    }

    private unsafe void LoadConfigFont()
    {
#if DEBUG
      PluginLog.LogVerbose("Inside LoadConfigFont method");
      string fontFile = $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-Medium-Custom2.otf";
      string dummyFontFilePath = $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-SemiBold.ttf";
      string symbolsFontFilePath =
        $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}symbols.ttf";

      PluginLog.LogVerbose($"Font file in DEBUG Mode: {fontFile}");
#else
      // PluginLog.LogVerbose("Inside LoadConfigFont method");
      var fontFile = $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-Medium-Custom2.otf";
      var dummyFontFilePath = $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-SemiBold.ttf";
      string symbolsFontFilePath =
        $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}symbols.ttf";

      // PluginLog.LogVerbose($"Font file in PROD Mode: {fontFile}");
#endif
      this.ConfigFontLoaded = false;
      if (File.Exists(fontFile))
      {
        ImFontConfigPtr fontConfig = null;
        try
        {
          ImGuiIOPtr io = ImGui.GetIO();
          List<ushort> chars = new();

          ImFontGlyphRangesBuilderPtr builder = new ImFontGlyphRangesBuilderPtr(ImGuiNative.ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder());
          builder.AddText(this.CharsToAddToAll);
          builder.AddText(this.LangComboItems);

          foreach (char c in this.LangComboItems)
          {
            builder.AddChar(c);
          }

          foreach (char c in this.CharsToAddToAll)
          {
            builder.AddChar(c);
          }

          foreach (char c in this.scriptCharList)
          {
            builder.AddChar(c);
          }

          foreach (char c in this.PuaChars)
          {
            builder.AddChar(c);
          }

          foreach (char c in this.PuaCharCodes)
          {
            builder.AddChar(c);
          }

          builder.BuildRanges(out ImVector ranges);

          this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesDefault());
          this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesVietnamese());
          this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesCyrillic());
          this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesThai());
          this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesJapanese());
          this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesKorean());
          this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesChineseFull());
          this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesChineseSimplifiedCommon());
          this.AddCharsFromIntPtr(chars, (ushort*)ranges.Data);

          ushort[] addChars = string.Join(string.Empty, chars.Select(c => new string((char)c, 2))).Select(c => (ushort)c).ToArray();
          chars.AddRange(addChars);

          chars.Add(0);

          fontConfig = ImGuiNative.ImFontConfig_ImFontConfig();
          fontConfig.OversampleH = 1;
          fontConfig.OversampleV = 1;
          this.glyphRangeConfigText = chars.ToArray();
          fixed (ushort* ptr = &this.glyphRangeConfigText[0])
          {
            fontConfig.GlyphRanges = (IntPtr)ptr;
          }

          this.ConfigUiFont = ImGui.GetIO().Fonts.AddFontFromFileTTF(dummyFontFilePath, this.configuration.FontSize, fontConfig);

          fontConfig.MergeMode = true;
          ImGui.GetIO().Fonts.AddFontFromFileTTF(symbolsFontFilePath, this.configuration.FontSize, fontConfig);
          ImGui.GetIO().Fonts.AddFontFromFileTTF(fontFile, this.configuration.FontSize, fontConfig);

#if DEBUG
          PluginLog.Debug($"ConfigUiFont data size: {ImGui.GetIO().Fonts.Fonts.Size}");
#endif
          this.ConfigFontLoaded = true;
#if DEBUG
          PluginLog.Debug($"Config Font loaded? {this.ConfigFontLoaded}");
#endif
        }
        catch (Exception ex)
        {
          PluginLog.Log($"Config Font failed to load. {fontFile}");
          PluginLog.Log(ex.ToString());
          this.ConfigFontLoadFailed = true;
        }
        finally
        {
          if (fontConfig.NativePtr != null)
          {
            fontConfig.Destroy();
          }
        }
      }
      else
      {
        PluginLog.Log($"Config Font doesn't exist. {fontFile}");
        this.ConfigFontLoadFailed = true;
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