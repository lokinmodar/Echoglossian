// Copyright (c) Critical Impact. All rights reserved.

namespace DalaMock.Core.Fonts.GameFonts;

using System.Collections.Generic;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.Utility;

using Lumina.Data.Files;

/// <summary>
/// Bakes game-font glyphs (AXIS, Jupiter, Meidinger, MiedingerMid, TrumpGothic) into an ImGui font
/// atlas. Glyphs are registered as custom rects before <c>atlas.Build()</c>; their bitmap pixels are
/// copied straight into the RGBA32 buffer that the renderer uploads to the GPU (see <see cref="FillRgba"/>).
/// Each glyph can live in a different <c>font{N}.tex</c> page, addressed by its texture file index.
/// </summary>
internal sealed unsafe class GameFontBakeJob
{
    private readonly ImFontAtlasPtr atlas;
    private readonly FdtReader fdt;
    private readonly IReadOnlyDictionary<int, TexFile> texFiles;
    private readonly List<(int RectId, int GlyphIdx)> rects;
    private readonly ImFontPtr font;
    private readonly float targetSizePx;
    private ushort* spaceRange;

    internal GameFontBakeJob(
        ImFontAtlasPtr atlas,
        FdtReader fdt,
        IReadOnlyDictionary<int, TexFile> texFiles,
        List<(int RectId, int GlyphIdx)> rects,
        ImFontPtr font,
        float targetSizePx,
        ushort* spaceRange)
    {
        this.atlas = atlas;
        this.fdt = fdt;
        this.texFiles = texFiles;
        this.rects = rects;
        this.font = font;
        this.targetSizePx = targetSizePx;
        this.spaceRange = spaceRange;
    }

    /// <summary>
    /// Patches the font's ascent/descent from the FDT header and rebuilds its lookup table.
    /// Call after <c>atlas.Build()</c> (glyph metrics aren't valid until then).
    /// </summary>
    public void PatchMetrics()
    {
        var h = this.fdt.FontHeader;

        // The FDT glyph metrics are in the font's native pixel space (its point Size). When the atlas
        // is built at a different size, scale the custom-rect glyph quads and the ascent/descent to the
        // requested size. quadScale == 1 in the native case. Mirrors Dalamud's PostProcessFullRangeFont.
        var targetSizePt = this.targetSizePx * 3f / 4f;
        var quadScale = h.Size > 0 ? targetSizePt / h.Size : 1f;

        this.font.Handle->Ascent = h.Ascent * quadScale;
        this.font.Handle->Descent = -h.Descent * quadScale;

        if (quadScale is > 1.0001f or < 0.9999f)
        {
            var glyphs = (ImGuiHelpers.ImFontGlyphReal*)this.font.Glyphs.Data;
            var count = this.font.Glyphs.Size;
            for (var i = 0; i < count; i++)
            {
                ref var g = ref glyphs[i];
                if (g.Codepoint == 0x20)
                {
                    continue;
                }

                g.X0 *= quadScale;
                g.Y0 *= quadScale;
                g.X1 *= quadScale;
                g.Y1 *= quadScale;
                g.AdvanceX *= quadScale;
            }
        }

        this.font.BuildLookupTable();

        if (this.spaceRange != null)
        {
            ImGui.MemFree(this.spaceRange);
            this.spaceRange = null;
        }
    }

    /// <summary>
    /// Copies game-font glyph bitmaps into the provided RGBA32 atlas buffer. Must be called on the exact
    /// buffer that gets uploaded to the GPU (i.e. right after the renderer's GetTexDataAsRGBA32 and
    /// before UpdateTexture) — ImGui's lazy alpha-8↔RGBA32 conversions otherwise discard our writes.
    /// </summary>
    /// <param name="pixels">Pointer to the RGBA32 atlas pixel buffer.</param>
    /// <param name="width">Atlas width in pixels.</param>
    /// <param name="height">Atlas height in pixels.</param>
    /// <param name="page">The atlas page whose buffer this is.</param>
    public void FillRgba(byte* pixels, int width, int height, int page)
    {
        if (pixels == null)
        {
            return;
        }

        foreach (var (rectId, glyphIdx) in this.rects)
        {
            var pRect = (ImGuiHelpers.ImFontAtlasCustomRectReal*)this.atlas.GetCustomRectByIndex(rectId);
            if (pRect == null || pRect->X == 0xFFFF)
            {
                continue;
            }

            if (pRect->TextureIndex != page)
            {
                continue;
            }

            var g = this.fdt.Glyphs[glyphIdx];
            if (!this.texFiles.TryGetValue(g.TextureFileIndex, out var texFile))
            {
                continue;
            }

            var srcData = texFile.ImageData;
            var texW = texFile.Header.Width;
            var texH = texFile.Header.Height;
            int rectX = pRect->X;
            int rectY = pRect->Y;
            var chanByte = g.TextureChannelByteIndex;

            for (var row = 0; row < g.BoundingHeight; row++)
            {
                int srcY = g.TextureOffsetY + row;
                if (srcY >= texH)
                {
                    break;
                }

                for (var col = 0; col < g.BoundingWidth; col++)
                {
                    int srcX = g.TextureOffsetX + col;
                    if (srcX >= texW)
                    {
                        break;
                    }

                    var a = srcData[(srcY * texW + srcX) * 4 + chanByte];

                    var dst = ((rectY + row) * width + (rectX + col)) * 4;
                    pixels[dst + 0] = 255;
                    pixels[dst + 1] = 255;
                    pixels[dst + 2] = 255;
                    pixels[dst + 3] = a;
                }
            }
        }
    }
}
