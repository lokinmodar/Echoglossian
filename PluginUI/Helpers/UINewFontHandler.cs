// <copyright file="UINewFontHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Text.Unicode;
using Dalamud.Interface.ManagedFontAtlas;

namespace Echoglossian.PluginUI.Helpers;

public class UINewFontHandler : IDisposable
{
    private const int DefaultFontSize = 24;
    private readonly Config? configuration;
    private bool disposedValue;
    public IFontHandle GeneralFontHandle;
    public IFontHandle LanguageFontHandle;
    private SafeFontConfig sfc;

    public UINewFontHandler(Config? configuration = default)
    {
        this.configuration = configuration;

        var allUnicodeRanges = UnicodeRanges.All;

        PluginRuntimeLog.Debug($"SymbolsFontPath: {SymbolsFontFilePath}");
        PluginRuntimeLog.Debug($"FontFilePath: {FontFilePath}");
        PluginRuntimeLog.Debug(
            $"ComplementaryFont3FilePath: {ComplementaryFont3FilePath}");
        PluginRuntimeLog.Debug(
            $"ComplementaryFont4FilePath: {ComplementaryFont4FilePath}");
        PluginRuntimeLog.Debug(
            $"ComplementaryFont5FilePath: {ComplementaryFont5FilePath}");
        PluginRuntimeLog.Debug(
            $"ComplementaryFont6FilePath: {ComplementaryFont6FilePath}");
        PluginRuntimeLog.Debug(
            $"ComplementaryFont7FilePath: {ComplementaryFont7FilePath}");
        PluginRuntimeLog.Debug($"SpecialFontFilePath: {SpecialFontFilePath}");
        PluginRuntimeLog.Debug($"LangComboFontFilePath: {LangComboFontFilePath}");
        PluginRuntimeLog.Debug($"DummyFontFilePath: {DummyFontFilePath}");
        PluginRuntimeLog.Debug(
            $"UndicodeRanges.All Length: {UnicodeRanges.All.Length}");

        this.GeneralFontHandle =
            PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e =>
                e.OnPreBuild(tk =>
                {
                    PluginRuntimeLog.Debug("Building font atlas for general use...");
                    PluginRuntimeLog.Debug(
                        $"Font size: {this.configuration?.FontSize} px");
                    PluginRuntimeLog.Debug(
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
                        SizePx = this.configuration?.FontSize ?? DefaultFontSize,
                        GlyphRanges = rangeBuilder.Build(),
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
                    PluginRuntimeLog.Debug(
                        "Building font atlas for language-specific use...");
                    PluginRuntimeLog.Debug(
                        $"Font size: {this.configuration?.FontSize} px");
                    PluginRuntimeLog.Debug(
                        $"Glyph ranges: {CharsToAddToAll.Length} chars, {ScriptCharList.Length} script chars, {PuaCharCodes.Length} PUA codes, {PuaChars.Length} PUA chars");
                    PluginRuntimeLog.Debug(
                        $"UndicodeRanges.All Length: {UnicodeRanges.All.Length}");
                    PluginRuntimeLog.Debug(
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
                        SizePx = this.configuration?.FontSize ?? DefaultFontSize,
                        GlyphRanges = rangeBuilder.Build(),
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


