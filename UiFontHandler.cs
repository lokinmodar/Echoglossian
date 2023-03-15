// <copyright file="UiFontHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Logging;
using ImGuiNET;

namespace Echoglossian;

public partial class Echoglossian
{
  public ImFontPtr ConfigUiFont;
  private readonly string fontFileName = "NotoSans-Medium.ttf";
  public bool FontLoaded;
  public bool FontLoadFailed;
  private GCHandle? glyphRangeConfigText;

  private GCHandle? glyphRangeMainText;

  public bool LanguageComboFontLoaded;
  public bool LanguageComboFontLoadFailed;
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

  private unsafe void LoadFont()
  {
    this.AdjustLanguageForFontBuild();

    var specialFontFilePath =
      $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}{this.specialFontFileName}";
    var fontFilePath =
      $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}{this.fontFileName}";
    var symbolsFontFilePath =
      $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}symbols.ttf";
    var dummyFontFilePath =
      $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-Regular.ttf";
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
        var io = ImGui.GetIO();
        List<ushort> chars = new();

        var builder = new ImFontGlyphRangesBuilderPtr(ImGuiNative
          .ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder());
        builder.AddText(this.CharsToAddToAll);
        builder.AddText(this.scriptCharList);

        foreach (var c in this.CharsToAddToAll) builder.AddChar(c);

        foreach (var c in this.scriptCharList) builder.AddChar(c);

        foreach (var c in this.PuaCharCodes) builder.AddChar(c);

        foreach (var c in this.PuaChars) builder.AddChar(c);
        builder.BuildRanges(out var ranges);

        this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesDefault());
        this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesVietnamese());
        this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesCyrillic());
        if (this.configuration.Lang is 16 or 22)
        {
          this.AddCharsFromIntPtr(chars,
            (ushort*)io.Fonts.GetGlyphRangesChineseSimplifiedCommon());
          this.AddCharsFromIntPtr(chars,
            (ushort*)io.Fonts.GetGlyphRangesChineseFull());
        }

        if (this.configuration.Lang is 21)
        {
          this.AddCharsFromIntPtr(chars,
            (ushort*)io.Fonts.GetGlyphRangesChineseSimplifiedCommon());
          this.AddCharsFromIntPtr(chars,
            (ushort*)io.Fonts.GetGlyphRangesChineseFull());
        }

        if (this.configuration.Lang is 56)
          this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesKorean());

        if (this.configuration.Lang is 50)
          this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesJapanese());

        if (this.configuration.Lang is 103)
          this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesThai());

        this.AddCharsFromIntPtr(chars, (ushort*)ranges.Data);

        var addChars = string
          .Join(string.Empty, chars.Select(c => new string((char)c, 2)))
          .Select(c => (ushort)c).ToArray();
        chars.AddRange(addChars);

        chars.Add(0);

        fontConfig = ImGuiNative.ImFontConfig_ImFontConfig();
        fontConfig.OversampleH = 1;
        fontConfig.OversampleV = 1;
        this.glyphRangeMainText?.Free();
        this.glyphRangeMainText =
          GCHandle.Alloc(chars.ToArray(), GCHandleType.Pinned);
        fontConfig.GlyphRanges = this.glyphRangeMainText.Value.AddrOfPinnedObject();

        this.UiFont = ImGui.GetIO().Fonts.AddFontFromFileTTF(dummyFontFilePath,
          this.configuration.FontSize, fontConfig);

        fontConfig.MergeMode = true;
        ImGui.GetIO().Fonts.AddFontFromFileTTF(symbolsFontFilePath,
          this.configuration.FontSize, fontConfig);
        ImGui.GetIO().Fonts.AddFontFromFileTTF(fontFilePath,
          this.configuration.FontSize, fontConfig);
        if (specialFontFilePath != string.Empty)
          ImGui.GetIO().Fonts.AddFontFromFileTTF(specialFontFilePath,
            this.configuration.FontSize, fontConfig);

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
        if (fontConfig.NativePtr != null) fontConfig.Destroy();
      }
    }
    else
    {
      PluginLog.Log($"Special Font doesn't exist. {specialFontFilePath}");
      this.FontLoadFailed = true;
    }
  }

  private unsafe void LoadLanguageComboFont()
  {
    var fontDir =
      $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}";
#if DEBUG
    PluginLog.LogVerbose("Inside LoadLanguageComboFont method");

    var fontFile = $@"{fontDir}NotoSans-Medium-Custom2.otf";
    var dummyFontFilePath = $@"{fontDir}NotoSans-SemiBold.ttf";
    var symbolsFontFilePath = $@"{fontDir}symbols.ttf";

    PluginLog.LogVerbose($"Font file in DEBUG Mode: {fontFile}");
#else
      // PluginLog.LogVerbose("Inside LoadLanguageComboFont method");
      var fontFile =
 $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-Medium-Custom2.otf";
      var dummyFontFilePath =
 $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-SemiBold.ttf";
      string symbolsFontFilePath =
        $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}symbols.ttf";

      // PluginLog.LogVerbose($"Font file in PROD Mode: {fontFile}");
#endif
    this.LanguageComboFontLoaded = false;
    if (File.Exists(fontFile))
    {
      ImFontConfigPtr fontConfig = null;
      try
      {
        var io = ImGui.GetIO();
        List<ushort> chars = new();

        var builder = new ImFontGlyphRangesBuilderPtr(ImGuiNative
          .ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder());
        builder.AddText(this.CharsToAddToAll);
        builder.AddText(this.LangComboItems);
        builder.AddText(this.scriptCharList);

        foreach (var c in this.PuaChars) builder.AddChar(c);

        foreach (var c in this.PuaCharCodes) builder.AddChar(c);

        builder.BuildRanges(out var ranges);

        this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesDefault());
        this.AddCharsFromIntPtr(chars, (ushort*)ranges.Data);

        var addChars = string
          .Join(string.Empty, chars.Select(c => new string((char)c, 2)))
          .Select(c => (ushort)c).ToArray();
        chars.AddRange(addChars);
        chars.Add(0);

        fontConfig = ImGuiNative.ImFontConfig_ImFontConfig();
        fontConfig.OversampleH = 1;
        fontConfig.OversampleV = 1;
        this.glyphRangeConfigText?.Free();
        this.glyphRangeConfigText =
          GCHandle.Alloc(chars.ToArray(), GCHandleType.Pinned);
        fontConfig.GlyphRanges =
          this.glyphRangeConfigText.Value.AddrOfPinnedObject();

        this.ConfigUiFont = ImGui.GetIO().Fonts.AddFontFromFileTTF(dummyFontFilePath,
          this.configuration.FontSize, fontConfig);

        fontConfig.MergeMode = true;
        ImGui.GetIO().Fonts.AddFontFromFileTTF(symbolsFontFilePath,
          this.configuration.FontSize, fontConfig);
        ImGui.GetIO().Fonts
          .AddFontFromFileTTF(fontFile, this.configuration.FontSize, fontConfig);

        foreach (var fileName in this.LanguagesDictionary.Values
                   .Select(x => x.FontName).ToHashSet())
          ImGui.GetIO().Fonts.AddFontFromFileTTF(fontDir + fileName,
            this.configuration.FontSize, fontConfig);

#if DEBUG
        PluginLog.Debug(
          $"ConfigUiFont data size: {ImGui.GetIO().Fonts.Fonts.Size}");
#endif
        this.LanguageComboFontLoaded = true;
#if DEBUG
        PluginLog.Debug(
          $"Language Combo Font loaded? {this.LanguageComboFontLoaded}");
#endif
      }
      catch (Exception ex)
      {
        PluginLog.Log($"Language Combo Font failed to load. {fontFile}");
        PluginLog.Log(ex.ToString());
        this.LanguageComboFontLoadFailed = true;
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
      PluginLog.Log($"Language Combo Font doesn't exist. {fontFile}");
      this.LanguageComboFontLoadFailed = true;
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