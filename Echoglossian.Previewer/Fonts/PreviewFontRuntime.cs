// <copyright file="PreviewFontRuntime.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Bindings.ImGui;

using Echoglossian.PluginUI.Runtime;

using System.Runtime.InteropServices;

namespace Echoglossian.Previewer.Fonts;

/// <summary>
/// Builds and exposes the previewer's standalone ImGui font stack.
/// </summary>
internal sealed unsafe class PreviewFontRuntime : IUiFontRuntime
{
    private const ushort BasicLatinStart = 0x0020;
    private const ushort Latin1SupplementEnd = 0x00FF;
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

        using (var fontConfigs = new FontConfigScope())
        {
            fixed (ushort* ranges = glyphRanges)
            {
                this.generalFont = AddFontStack(
                    io.Fonts,
                    selection.GeneralFontPaths,
                    selection.FontSize,
                    ranges,
                    fontConfigs);
                this.languageFont = AddFontStack(
                    io.Fonts,
                    selection.LanguageFontPaths,
                    selection.FontSize,
                    ranges,
                    fontConfigs);
                io.Fonts.Build();
            }

            recreateFontTexture();
        }
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
    /// <param name="fontConfigs">The native font configurations kept alive through the atlas build.</param>
    /// <returns>The primary ImGui font for the stack.</returns>
    private static ImFontPtr AddFontStack(
        ImFontAtlasPtr atlas,
        IEnumerable<string> fontPaths,
        int fontSize,
        ushort* glyphRanges,
        FontConfigScope fontConfigs)
    {
        ImFontPtr primaryFont = default;
        var hasPrimaryFont = false;

        foreach (var fontPath in fontPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(fontPath))
            {
                continue;
            }

            var fontConfig = hasPrimaryFont
                ? fontConfigs.CreateMergeConfig(primaryFont)
                : fontConfigs.CreatePrimaryConfig();

            var font = atlas.AddFontFromFileTTF(
                fontPath,
                fontSize,
                fontConfig,
                glyphRanges);
            if (!hasPrimaryFont)
            {
                primaryFont = font;
                hasPrimaryFont = true;
            }
        }

        return primaryFont;
    }

    /// <summary>
    /// Owns the native ImGui font configurations until the current atlas rebuild completes.
    /// </summary>
    private sealed class FontConfigScope : IDisposable
    {
        private readonly List<ImFontConfigPtr> configs = [];
        private bool disposed;

        /// <summary>
        /// Creates a configuration for the first font in a stack.
        /// </summary>
        /// <returns>The initialized primary font configuration.</returns>
        public ImFontConfigPtr CreatePrimaryConfig()
        {
            return ImFontConfigPtr.Null;
        }

        /// <summary>
        /// Creates a configuration that merges into the stack's primary font.
        /// </summary>
        /// <param name="primaryFont">The destination font for merged glyphs.</param>
        /// <returns>The initialized merge font configuration.</returns>
        public ImFontConfigPtr CreateMergeConfig(ImFontPtr primaryFont)
        {
            var config = this.CreateConfig();
            config.MergeMode = true;
            config.DstFont = primaryFont;
            return config;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            foreach (var config in this.configs)
            {
                NativeMemory.Free(config.Handle);
            }

            this.disposed = true;
        }

        /// <summary>
        /// Allocates one initialized native ImGui font configuration.
        /// </summary>
        /// <returns>The allocated configuration.</returns>
        private ImFontConfigPtr CreateConfig()
        {
            var handle = (ImFontConfig*)NativeMemory.Alloc(
                (nuint)sizeof(ImFontConfig));
            *handle = default;
            handle->FontDataOwnedByAtlas = 1;
            handle->OversampleH = 2;
            handle->OversampleV = 1;
            handle->GlyphMaxAdvanceX = float.MaxValue;
            handle->RasterizerMultiply = 1.0f;
            var config = new ImFontConfigPtr(handle);
            this.configs.Add(config);
            return config;
        }
    }

    /// <summary>
    /// Builds sorted single-character glyph ranges from preview content and the selected script.
    /// </summary>
    /// <param name="title">The scenario title.</param>
    /// <param name="text">The scenario text.</param>
    /// <param name="exclusiveCharacters">The selected language character set.</param>
    /// <returns>Null-terminated ImGui glyph ranges.</returns>
    internal static ushort[] BuildGlyphRanges(
        string? title,
        string? text,
        string? exclusiveCharacters)
    {
        var characters = new SortedSet<ushort>();
        AddRange(characters, BasicLatinStart, Latin1SupplementEnd);
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
    /// Adds an inclusive UTF-16 code-unit range.
    /// </summary>
    /// <param name="characters">The target character set.</param>
    /// <param name="start">The first character to include.</param>
    /// <param name="end">The last character to include.</param>
    private static void AddRange(ISet<ushort> characters, ushort start, ushort end)
    {
        for (var character = start; character <= end; character++)
        {
            characters.Add(character);
        }
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
