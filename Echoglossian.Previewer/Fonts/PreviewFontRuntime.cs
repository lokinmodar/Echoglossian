// <copyright file="PreviewFontRuntime.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Bindings.ImGui;

using Echoglossian.PluginUI.Runtime;

namespace Echoglossian.Previewer.Fonts;

/// <summary>
/// Builds and exposes the previewer's standalone ImGui font stack.
/// </summary>
internal sealed unsafe class PreviewFontRuntime : IUiFontRuntime
{
    private readonly ImFontPtr generalFont;
    private readonly ImFontPtr languageFont;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewFontRuntime" /> class.
    /// </summary>
    /// <param name="selection">The resolved plugin font selection.</param>
    /// <param name="title">The current preview scenario title.</param>
    /// <param name="text">The current preview scenario text.</param>
    /// <param name="recreateFontTexture">Rebuilds the active backend font texture.</param>
    internal PreviewFontRuntime(
        PreviewFontSelection selection,
        string? title,
        string? text,
        Action recreateFontTexture)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(recreateFontTexture);

        var glyphRanges = BuildGlyphRanges(
            title,
            text,
            selection.SelectedLanguage.ExclusiveCharsToAdd);
        ImGuiIOPtr io = ImGui.GetIO();

        fixed (ushort* ranges = glyphRanges)
        {
            this.generalFont = AddFontStack(
                io.Fonts,
                selection.GeneralFontPaths,
                selection.FontSize,
                ranges);
            this.languageFont = AddFontStack(
                io.Fonts,
                selection.LanguageFontPaths,
                selection.FontSize,
                ranges);
            io.Fonts.Build();
        }

        recreateFontTexture();
    }

    /// <inheritdoc />
    public IDisposable Push(UiFontKind fontKind)
    {
        ImGui.PushFont(fontKind == UiFontKind.General ? this.generalFont : this.languageFont);
        return new FontPopScope();
    }

    /// <summary>
    /// Adds a font stack directly to the standalone ImGui atlas.
    /// </summary>
    /// <param name="atlas">The active ImGui font atlas.</param>
    /// <param name="fontPaths">The ordered font file paths.</param>
    /// <param name="fontSize">The configured font size.</param>
    /// <param name="glyphRanges">The current glyph ranges.</param>
    /// <returns>The primary ImGui font for the stack.</returns>
    private static ImFontPtr AddFontStack(
        ImFontAtlasPtr atlas,
        IEnumerable<string> fontPaths,
        int fontSize,
        ushort* glyphRanges)
    {
        ImFontPtr primaryFont = default;
        ImFontConfigPtr mergeConfig = default;
        var hasPrimaryFont = false;

        foreach (var fontPath in fontPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(fontPath))
            {
                continue;
            }

            if (hasPrimaryFont)
            {
                mergeConfig.MergeMode = true;
            }

            var font = atlas.AddFontFromFileTTF(
                fontPath,
                fontSize,
                hasPrimaryFont ? mergeConfig : default,
                glyphRanges);
            if (!hasPrimaryFont)
            {
                primaryFont = font;
                mergeConfig = font.ConfigData;
                hasPrimaryFont = true;
            }
        }

        return primaryFont;
    }

    /// <summary>
    /// Builds sorted single-character glyph ranges from preview content and the selected script.
    /// </summary>
    /// <param name="title">The scenario title.</param>
    /// <param name="text">The scenario text.</param>
    /// <param name="exclusiveCharacters">The selected language character set.</param>
    /// <returns>Null-terminated ImGui glyph ranges.</returns>
    private static ushort[] BuildGlyphRanges(
        string? title,
        string? text,
        string? exclusiveCharacters)
    {
        var characters = new SortedSet<ushort>();
        AddCharacters(characters, title);
        AddCharacters(characters, text);
        AddCharacters(characters, exclusiveCharacters);

        var ranges = new ushort[(characters.Count * 2) + 1];
        var index = 0;
        foreach (var character in characters)
        {
            ranges[index++] = character;
            ranges[index++] = character;
        }

        return ranges;
    }

    /// <summary>
    /// Adds UTF-16 code units supported by ImGui's current glyph range API.
    /// </summary>
    /// <param name="characters">The target character set.</param>
    /// <param name="value">The source text.</param>
    private static void AddCharacters(ISet<ushort> characters, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        foreach (var character in value)
        {
            characters.Add(character);
        }
    }

    /// <summary>
    /// Restores the previous ImGui font stack entry.
    /// </summary>
    private sealed class FontPopScope : IDisposable
    {
        private int disposed;

        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) == 0)
            {
                ImGui.PopFont();
            }
        }
    }
}
