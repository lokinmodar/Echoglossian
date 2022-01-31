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
    public bool ConfigFontLoaded;
    public bool ConfigFontLoadFailed;
    public ImFontPtr ConfigUiFont;
    private readonly string fontFileName = "NotoSans-Medium.ttf";
    public bool FontLoaded;
    public bool FontLoadFailed;
    private string scriptCharList = string.Empty;

    private string specialFontFileName = string.Empty;
    public ImFontPtr UiFont;

    private void AdjustLanguageForFontBuild()
    {
#if DEBUG
      PluginLog.Debug("Inside AdjustLanguageForFontBuild method");
#endif

      var lang = this.LanguagesDictionary[this.configuration.Lang];
      this.specialFontFileName = lang.FontName;
      this.scriptCharList = lang.ExclusiveCharsToAdd;
    }

    private void LoadFont()
    {
      this.AdjustLanguageForFontBuild();

      var specialFontFilePath = $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}{this.specialFontFileName}";
      var fontFilePath = $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}{this.fontFileName}";
      var symbolsFontFilePath =
        $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}symbols.ttf";
      var dummyFontFilePath = $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-Regular.ttf";
#if DEBUG
      PluginLog.LogWarning("Inside LoadFont method");
      PluginLog.LogWarning($"Font file in DEBUG Mode: {specialFontFilePath}");
#endif
      this.FontLoaded = false;
      if (File.Exists(specialFontFilePath) || File.Exists(fontFilePath))
      {
        try
        {
          unsafe
          {
            var io = ImGui.GetIO();
            List<ushort> chars = new();

            var builder = new ImFontGlyphRangesBuilderPtr(ImGuiNative.ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder());
            builder.AddText(this.CharsToAddToAll);
            builder.AddText(this.scriptCharList);

            foreach (var c in this.PuaCharCodes)
            {
              builder.AddChar(c);
            }

            foreach (var c in this.PuaChars)
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

            builder.BuildRanges(out var ranges);

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

            var addChars = string.Join(string.Empty, chars.Select(c => new string((char)c, 2))).Select(c => (ushort)c).ToArray();
            chars.AddRange(addChars);

            chars.Add(0);

            var arr = chars.ToArray();

            var nativeConfig = ImGuiNative.ImFontConfig_ImFontConfig();
            var fontConfig = new ImFontConfigPtr(nativeConfig)
            {
              OversampleH = 2,
              OversampleV = 2,
              MergeMode = true
            };

            fixed (ushort* ptr = &arr[0])
            {
              if (specialFontFilePath != string.Empty)
              {
                ImGui.GetIO().Fonts.AddFontFromFileTTF(dummyFontFilePath, this.configuration.FontSize,
                  null);
                ImGui.GetIO().Fonts.AddFontFromFileTTF(symbolsFontFilePath, this.configuration.FontSize,
                  fontConfig);
                ImGui.GetIO().Fonts.AddFontFromFileTTF(fontFilePath, this.configuration.FontSize,
                  fontConfig,
                  new IntPtr(ptr));
                this.UiFont = ImGui.GetIO().Fonts.AddFontFromFileTTF(specialFontFilePath, this.configuration.FontSize,
                  fontConfig,
                  new IntPtr(ptr));
              }
              else
              {
                ImGui.GetIO().Fonts.AddFontFromFileTTF(dummyFontFilePath, this.configuration.FontSize,
                  fontConfig,
                  new IntPtr(ptr));
                ImGui.GetIO().Fonts.AddFontFromFileTTF(symbolsFontFilePath, this.configuration.FontSize,
                  fontConfig);
                this.UiFont = ImGui.GetIO().Fonts.AddFontFromFileTTF(fontFilePath, this.configuration.FontSize,
                  fontConfig,
                  new IntPtr(ptr));
              }
            }

#if DEBUG
            PluginLog.Debug($"UiFont Data size: {ImGui.GetIO().Fonts.Fonts.Size}");
#endif
            this.FontLoaded = true;
#if DEBUG
            PluginLog.Debug($"Font loaded? {this.FontLoaded}");
#endif
          }
        }
        catch (Exception ex)
        {
          PluginLog.Log($"Special Font failed to load. {specialFontFilePath}");
          PluginLog.Log(ex.ToString());
          this.FontLoadFailed = true;
        }
      }
      else
      {
        PluginLog.Log($"Special Font doesn't exist. {specialFontFilePath}");
        this.FontLoadFailed = true;
      }
    }

    private void LoadConfigFont()
    {
#if DEBUG
      PluginLog.LogVerbose("Inside LoadConfigFont method");
      var fontFile = $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-Medium-Custom2.otf";
      var dummyFontFilePath = $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-SemiBold.ttf";
      var symbolsFontFilePath =
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
        try
        {
          unsafe
          {
            var io = ImGui.GetIO();
            List<ushort> chars = new();

            var builder = new ImFontGlyphRangesBuilderPtr(ImGuiNative.ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder());
            builder.AddText(this.CharsToAddToAll);
            builder.AddText(this.LangComboItems);
            foreach (var c in this.PuaCharCodes)
            {
              builder.AddChar(c);
            }

            builder.BuildRanges(out var ranges);

            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesDefault());
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesVietnamese());
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesCyrillic());
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesThai());
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesJapanese());
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesKorean());
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesChineseFull());
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesChineseSimplifiedCommon());
            this.AddCharsFromIntPtr(chars, (ushort*)ranges.Data);

            var addChars = string.Join(string.Empty, chars.Select(c => new string((char)c, 2))).Select(c => (ushort)c).ToArray();
            chars.AddRange(addChars);

            chars.Add(0);

            var arr = chars.ToArray();

            var nativeConfig = ImGuiNative.ImFontConfig_ImFontConfig();
            var fontConfig = new ImFontConfigPtr(nativeConfig)
            {
              OversampleH = 2,
              OversampleV = 2,
              MergeMode = true
            };

            fixed (ushort* ptr = &arr[0])
            {
              ImGui.GetIO().Fonts.AddFontFromFileTTF(dummyFontFilePath, 17.0f, null, new IntPtr(ptr));
              ImGui.GetIO().Fonts.AddFontFromFileTTF(symbolsFontFilePath, this.configuration.FontSize,
                fontConfig);
              this.ConfigUiFont = ImGui.GetIO().Fonts.AddFontFromFileTTF(fontFile, 17.0f,
                fontConfig, new IntPtr(ptr));
            }

#if DEBUG
            PluginLog.Debug($"ConfigUiFont data size: {ImGui.GetIO().Fonts.Fonts.Size}");
#endif
            this.ConfigFontLoaded = true;
#if DEBUG
            PluginLog.Debug($"Config Font loaded? {this.ConfigFontLoaded}");
#endif
            fontConfig.Destroy();
          }
        }
        catch (Exception ex)
        {
          PluginLog.Log($"Config Font failed to load. {fontFile}");
          PluginLog.Log(ex.ToString());
          this.ConfigFontLoadFailed = true;
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