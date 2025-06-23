// <copyright file="UINewFontHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Text.Unicode;

using Dalamud.Interface.ManagedFontAtlas;

namespace Echoglossian.PluginUI.Helpers
{
  public class UINewFontHandler : IDisposable
  {
    private bool disposedValue;
    private Config? configuration;
    private SafeFontConfig sfc;
    public IFontHandle GeneralFontHandle;
    public IFontHandle LanguageFontHandle;

    public UINewFontHandler(Config? configuration = default)
    {
      this.configuration = configuration;

      var allUnicodeRanges = UnicodeRanges.All;

      Echoglossian.PluginLog.Debug($"SymbolsFontPath: {SymbolsFontFilePath}");
      Echoglossian.PluginLog.Debug($"FontFilePath: {FontFilePath}");
      Echoglossian.PluginLog.Debug($"ComplementaryFont3FilePath: {ComplementaryFont3FilePath}");
      Echoglossian.PluginLog.Debug($"ComplementaryFont4FilePath: {ComplementaryFont4FilePath}");
      Echoglossian.PluginLog.Debug($"ComplementaryFont5FilePath: {ComplementaryFont5FilePath}");
      Echoglossian.PluginLog.Debug($"ComplementaryFont6FilePath: {ComplementaryFont6FilePath}");
      Echoglossian.PluginLog.Debug($"ComplementaryFont7FilePath: {ComplementaryFont7FilePath}");
      Echoglossian.PluginLog.Debug($"SpecialFontFilePath: {SpecialFontFilePath}");
      Echoglossian.PluginLog.Debug($"LangComboFontFilePath: {Echoglossian.LangComboFontFilePath}");
      Echoglossian.PluginLog.Debug($"DummyFontFilePath: {Echoglossian.DummyFontFilePath}");
      Echoglossian.PluginLog.Debug($"UndicodeRanges.All Length: {UnicodeRanges.All.Length}");

      this.GeneralFontHandle = Echoglossian.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(
        e => e.OnPreBuild(tk =>
        {
          PluginLog.Debug("Building font atlas for general use...");
          PluginLog.Debug($"Font size: {this.configuration?.FontSize} px");
          PluginLog.Debug($"Glyph ranges: {Echoglossian.LangComboItems.Length} items, {Echoglossian.CharsToAddToAll.Length} chars, {Echoglossian.ScriptCharList.Length} script chars, {Echoglossian.PuaCharCodes.Length} PUA codes, {Echoglossian.PuaChars.Length} PUA chars");

          var rangeBuilder = default(FluentGlyphRangeBuilder)
            .With(Echoglossian.LangComboItems.AsSpan())
            .With(Echoglossian.CharsToAddToAll.AsSpan())
            .With(Echoglossian.ScriptCharList.AsSpan())
            .With(Echoglossian.PuaCharCodes.AsSpan())
            .With(Echoglossian.PuaChars.AsSpan())
            .With(allUnicodeRanges.FirstCodePoint, allUnicodeRanges.FirstCodePoint + allUnicodeRanges.Length - 1);

          // more ranges here
          this.sfc = new SafeFontConfig
          {
            SizePx = (float)this.configuration?.FontSize,
            GlyphRanges = rangeBuilder.Build(),
          };
          this.sfc.MergeFont = tk.Font = tk.AddFontFromFile(Echoglossian.LangComboFontFilePath, this.sfc);
          tk.AddFontFromFile(Echoglossian.SymbolsFontFilePath, this.sfc);
          tk.AddFontFromFile(Echoglossian.FontFilePath, this.sfc);
          tk.AddFontFromFile(Echoglossian.ComplementaryFont3FilePath, this.sfc);
          tk.AddFontFromFile(Echoglossian.ComplementaryFont4FilePath, this.sfc);
          tk.AddFontFromFile(Echoglossian.ComplementaryFont5FilePath, this.sfc);
          tk.AddFontFromFile(Echoglossian.ComplementaryFont6FilePath, this.sfc);
          tk.AddFontFromFile(Echoglossian.ComplementaryFont7FilePath, this.sfc);
          if (!string.IsNullOrWhiteSpace(Echoglossian.SpecialFontFilePath))
          {
            tk.AddFontFromFile(Echoglossian.SpecialFontFilePath, this.sfc);
          }
        }));

      this.LanguageFontHandle = Echoglossian.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(
        e => e.OnPreBuild(tk =>
        {
          PluginLog.Debug("Building font atlas for language-specific use...");
          PluginLog.Debug($"Font size: {this.configuration?.FontSize} px");
          PluginLog.Debug($"Glyph ranges: {Echoglossian.CharsToAddToAll.Length} chars, {Echoglossian.ScriptCharList.Length} script chars, {Echoglossian.PuaCharCodes.Length} PUA codes, {Echoglossian.PuaChars.Length} PUA chars");
          PluginLog.Debug($"UndicodeRanges.All Length: {UnicodeRanges.All.Length}");
          PluginLog.Debug($"Selected language: {Echoglossian.SelectedLanguage.LanguageName}");

          var rangeBuilder = default(FluentGlyphRangeBuilder)
            .With(Echoglossian.CharsToAddToAll.AsSpan())
            .With(Echoglossian.ScriptCharList.AsSpan())
            .With(Echoglossian.PuaCharCodes.AsSpan())
            .With(Echoglossian.PuaChars.AsSpan())
            .With(allUnicodeRanges.FirstCodePoint, allUnicodeRanges.FirstCodePoint + allUnicodeRanges.Length - 1)
            .With(Echoglossian.SelectedLanguage.ExclusiveCharsToAdd.AsSpan());

          // more ranges here
          this.sfc = new SafeFontConfig
          {
            SizePx = this.configuration.FontSize,
            GlyphRanges = rangeBuilder.Build(),
          };
          this.sfc.MergeFont = tk.Font = tk.AddFontFromFile(Echoglossian.DummyFontFilePath, this.sfc);
          tk.AddFontFromFile(Echoglossian.SymbolsFontFilePath, this.sfc);
          tk.AddFontFromFile(Echoglossian.FontFilePath, this.sfc);
          if (!string.IsNullOrWhiteSpace(Echoglossian.SpecialFontFilePath))
          {
            tk.AddFontFromFile(Echoglossian.SpecialFontFilePath, this.sfc);
          }
        }));
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

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~UINewFontHandler()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }
    public void Dispose()
    {
      // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
      this.Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }
  }
}
