// <copyright file="UINewFontHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Text.Unicode;
using Dalamud.Interface.ManagedFontAtlas;

namespace Echoglossian.PluginUI.Helpers;

public class UINewFontHandler : IDisposable
{
    private readonly Config? configuration;
    private bool disposedValue;
    public IFontHandle GeneralFontHandle;
    public IFontHandle LanguageFontHandle;
    private SafeFontConfig sfc;

    public UINewFontHandler(Config? configuration = default)
    {
        this.configuration = configuration;

        var allUnicodeRanges = UnicodeRanges.All;

        PluginLog.Debug($"SymbolsFontPath: {SymbolsFontFilePath}");
        PluginLog.Debug($"FontFilePath: {FontFilePath}");
        PluginLog.Debug(
            $"ComplementaryFont3FilePath: {ComplementaryFont3FilePath}");
        PluginLog.Debug(
            $"ComplementaryFont4FilePath: {ComplementaryFont4FilePath}");
        PluginLog.Debug(
            $"ComplementaryFont5FilePath: {ComplementaryFont5FilePath}");
        PluginLog.Debug(
            $"ComplementaryFont6FilePath: {ComplementaryFont6FilePath}");
        PluginLog.Debug(
            $"ComplementaryFont7FilePath: {ComplementaryFont7FilePath}");
        PluginLog.Debug($"SpecialFontFilePath: {SpecialFontFilePath}");
        PluginLog.Debug($"LangComboFontFilePath: {LangComboFontFilePath}");
        PluginLog.Debug($"DummyFontFilePath: {DummyFontFilePath}");
        PluginLog.Debug(
            $"UndicodeRanges.All Length: {UnicodeRanges.All.Length}");

        this.GeneralFontHandle =
            PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e =>
                e.OnPreBuild(tk =>
                {
                    PluginLog.Debug("Building font atlas for general use...");
                    PluginLog.Debug(
                        $"Font size: {this.configuration?.FontSize} px");
                    PluginLog.Debug(
                        $"Glyph ranges: {LangComboItems.Length} items, {CharsToAddToAll.Length} chars, {ScriptCharList.Length} script chars, {PuaCharCodes.Length} PUA codes, {PuaChars.Length} PUA chars");

                    var rangeBuilder = default(FluentGlyphRangeBuilder)
                        .With(LangComboItems.AsSpan())
                        .With(CharsToAddToAll.AsSpan())
                        .With(ScriptCharList.AsSpan())
                        .With(PuaCharCodes.AsSpan()).With(PuaChars.AsSpan())
                        .With(
                            allUnicodeRanges.FirstCodePoint,
                            allUnicodeRanges.FirstCodePoint +
                            allUnicodeRanges.Length - 1);

                    // more ranges here
                    this.sfc = new SafeFontConfig
                    {
                        SizePx = (float)this.configuration?.FontSize,
                        GlyphRanges = rangeBuilder.Build()
                    };
                    this.sfc.MergeFont = tk.Font = tk.AddFontFromFile(
                        LangComboFontFilePath,
                        this.sfc);
                    tk.AddFontFromFile(SymbolsFontFilePath, this.sfc);
                    tk.AddFontFromFile(FontFilePath, this.sfc);
                    tk.AddFontFromFile(ComplementaryFont3FilePath, this.sfc);
                    tk.AddFontFromFile(ComplementaryFont4FilePath, this.sfc);
                    tk.AddFontFromFile(ComplementaryFont5FilePath, this.sfc);
                    tk.AddFontFromFile(ComplementaryFont6FilePath, this.sfc);
                    tk.AddFontFromFile(ComplementaryFont7FilePath, this.sfc);
                    if (!string.IsNullOrWhiteSpace(SpecialFontFilePath))
                    {
                        tk.AddFontFromFile(SpecialFontFilePath, this.sfc);
                    }
                }));

        this.LanguageFontHandle =
            PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e =>
                e.OnPreBuild(tk =>
                {
                    PluginLog.Debug(
                        "Building font atlas for language-specific use...");
                    PluginLog.Debug(
                        $"Font size: {this.configuration?.FontSize} px");
                    PluginLog.Debug(
                        $"Glyph ranges: {CharsToAddToAll.Length} chars, {ScriptCharList.Length} script chars, {PuaCharCodes.Length} PUA codes, {PuaChars.Length} PUA chars");
                    PluginLog.Debug(
                        $"UndicodeRanges.All Length: {UnicodeRanges.All.Length}");
                    PluginLog.Debug(
                        $"Selected language: {SelectedLanguage.LanguageName}");

                    var rangeBuilder = default(FluentGlyphRangeBuilder)
                        .With(CharsToAddToAll.AsSpan())
                        .With(ScriptCharList.AsSpan())
                        .With(PuaCharCodes.AsSpan()).With(PuaChars.AsSpan())
                        .With(
                            allUnicodeRanges.FirstCodePoint,
                            allUnicodeRanges.FirstCodePoint +
                            allUnicodeRanges.Length - 1).With(
                            SelectedLanguage.ExclusiveCharsToAdd.AsSpan());

                    // more ranges here
                    this.sfc = new SafeFontConfig
                    {
                        SizePx = (float)this.configuration?.FontSize,
                        GlyphRanges = rangeBuilder.Build()
                    };
                    this.sfc.MergeFont = tk.Font = tk.AddFontFromFile(
                        DummyFontFilePath,
                        this.sfc);
                    tk.AddFontFromFile(SymbolsFontFilePath, this.sfc);
                    tk.AddFontFromFile(FontFilePath, this.sfc);
                    if (!string.IsNullOrWhiteSpace(SpecialFontFilePath))
                    {
                        tk.AddFontFromFile(SpecialFontFilePath, this.sfc);
                    }
                }));
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~UINewFontHandler()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!this.disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            this.disposedValue = true;
        }
    }
}