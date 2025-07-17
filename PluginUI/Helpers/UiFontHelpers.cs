// <copyright file="UiFontHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

public partial class Echoglossian
{
    public static readonly string FontFileName = "NotoSans-Medium.ttf";

    public static string SpecialFontFileName = string.Empty;
    public ImFontPtr ConfigUiFont;
    public bool FontLoaded;
    public bool FontLoadFailed;
    public GCHandle? GlyphRangeConfigText;

    public GCHandle? GlyphRangeMainText;

    public bool LanguageComboFontLoaded;
    public bool LanguageComboFontLoadFailed;
    public ImFontPtr UiFont;

    private static void AdjustLanguageForFontBuild()
    {
#if DEBUG
        PluginLog.Debug("Inside AdjustLanguageForFontBuild method");
#endif

        var lang = SelectedLanguage;
        SpecialFontFileName = lang.FontName;
        ScriptCharList = lang.ExclusiveCharsToAdd;

        PluginLog.Debug(
            "Lang:\n " + lang + "\nSpecialFontFileName:\n " +
            SpecialFontFileName + "\nScriptCharList:\n " + ScriptCharList);
    }

    public static void MountFontPaths()
    {
        AdjustLanguageForFontBuild();

        SpecialFontFilePath =
            $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}{SpecialFontFileName}";
        FontFilePath =
            $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}{FontFileName}";
        SymbolsFontFilePath =
            $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}symbols.ttf";
        DummyFontFilePath =
            $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-Regular.ttf";
        LangComboFontFilePath =
            $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-Medium-Custom2.otf";
        PluginLog.Debug(
            "Fonts paths:\n " + SpecialFontFilePath + "\n " + FontFilePath +
            "\n " + SymbolsFontFilePath + "\n " + DummyFontFilePath);
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