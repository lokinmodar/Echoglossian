// Copyright (c) Critical Impact. All rights reserved.

namespace DalaMock.Core.Fonts.GameFonts;

using System;
using System.Collections.Generic;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.GameFonts;

using Lumina;
using Lumina.Data.Files;

/// <summary>
/// Loads a game font (AXIS and the other game families) and registers its glyphs as custom rects on
/// an ImGui font atlas.
/// </summary>
internal static class GameFontBuildHelper
{
    private const string TexPathFormat = "common/font/font{0}.tex";

    // FDT path + horizontal offset per game-font-and-size, mirroring Dalamud's
    // GameFontFamilyAndSizeAttribute (which is internal and so can't be read directly).
    private static readonly Dictionary<GameFontFamilyAndSize, (string Fdt, float HorizontalOffset)> FontFiles = new()
    {
        [GameFontFamilyAndSize.Axis96] = ("common/font/AXIS_96.fdt", -1),
        [GameFontFamilyAndSize.Axis12] = ("common/font/AXIS_12.fdt", -1),
        [GameFontFamilyAndSize.Axis14] = ("common/font/AXIS_14.fdt", -1),
        [GameFontFamilyAndSize.Axis18] = ("common/font/AXIS_18.fdt", -1),
        [GameFontFamilyAndSize.Axis36] = ("common/font/AXIS_36.fdt", -4),
        [GameFontFamilyAndSize.Jupiter16] = ("common/font/Jupiter_16.fdt", -1),
        [GameFontFamilyAndSize.Jupiter20] = ("common/font/Jupiter_20.fdt", -1),
        [GameFontFamilyAndSize.Jupiter23] = ("common/font/Jupiter_23.fdt", -1),
        [GameFontFamilyAndSize.Jupiter45] = ("common/font/Jupiter_45.fdt", -2),
        [GameFontFamilyAndSize.Jupiter46] = ("common/font/Jupiter_46.fdt", -2),
        [GameFontFamilyAndSize.Jupiter90] = ("common/font/Jupiter_90.fdt", -4),
        [GameFontFamilyAndSize.Meidinger16] = ("common/font/Meidinger_16.fdt", -1),
        [GameFontFamilyAndSize.Meidinger20] = ("common/font/Meidinger_20.fdt", -1),
        [GameFontFamilyAndSize.Meidinger40] = ("common/font/Meidinger_40.fdt", -4),
        [GameFontFamilyAndSize.MiedingerMid10] = ("common/font/MiedingerMid_10.fdt", -1),
        [GameFontFamilyAndSize.MiedingerMid12] = ("common/font/MiedingerMid_12.fdt", -1),
        [GameFontFamilyAndSize.MiedingerMid14] = ("common/font/MiedingerMid_14.fdt", -1),
        [GameFontFamilyAndSize.MiedingerMid18] = ("common/font/MiedingerMid_18.fdt", -1),
        [GameFontFamilyAndSize.MiedingerMid36] = ("common/font/MiedingerMid_36.fdt", -2),
        [GameFontFamilyAndSize.TrumpGothic184] = ("common/font/TrumpGothic_184.fdt", -1),
        [GameFontFamilyAndSize.TrumpGothic23] = ("common/font/TrumpGothic_23.fdt", -1),
        [GameFontFamilyAndSize.TrumpGothic34] = ("common/font/TrumpGothic_34.fdt", -1),
        [GameFontFamilyAndSize.TrumpGothic68] = ("common/font/TrumpGothic_68.fdt", -3),
    };

    /// <summary>
    /// Registers the glyphs of the given game font family (at the FDT size closest to
    /// <paramref name="sizePx"/>) as custom rects on <paramref name="atlas"/> (pre-Build step) and
    /// returns a bake job for the post-build / upload steps, or null on failure.
    /// <para>
    /// <paramref name="gameFont"/> is set as soon as the font slot is registered with the atlas —
    /// even if setup later fails and null is returned — so the caller can tell a fallback font is
    /// not needed (which would otherwise shift every subsequent font index).
    /// </para>
    /// </summary>
    public static unsafe GameFontBakeJob? TryPrebuild(
        GameData gameData,
        ImFontAtlasPtr atlas,
        byte[] templateFontData,
        GameFontFamily family,
        float sizePx,
        out ImFontPtr gameFont)
    {
        gameFont = default;
        ushort* spaceRange = null;
        try
        {
            var familyAndSize = GameFontStyle.GetRecommendedFamilyAndSize(family, sizePx * 3f / 4f);
            if (familyAndSize == GameFontFamilyAndSize.Undefined
                || !FontFiles.TryGetValue(familyAndSize, out var info))
            {
                return null;
            }

            var fdtFile = gameData.GetFile(info.Fdt);
            if (fdtFile is null)
            {
                return null;
            }

            var fdt = new FdtReader(fdtFile.Data);

            spaceRange = (ushort*)ImGui.MemAlloc(sizeof(ushort) * 3);
            spaceRange[0] = 0x0020;
            spaceRange[1] = 0x0020;
            spaceRange[2] = 0;

            ImFontPtr font;
            {
                var cfg = ImGui.ImFontConfig();
                cfg.MergeMode = false;
                cfg.GlyphRanges = spaceRange;
                cfg.FontDataOwnedByAtlas = true;

                // Shows the font correctly in the font selector imgui provides.
                var nameDst = cfg.Name;
                nameDst.Clear();
                var nameBytes = System.Text.Encoding.UTF8.GetBytes($"{family} (game), {sizePx:0}px");
                var nameCount = Math.Min(nameBytes.Length, nameDst.Length - 1);
                nameBytes.AsSpan(0, nameCount).CopyTo(nameDst);
                nameDst[nameCount] = 0;

                var fontNative = (byte*)ImGui.MemAlloc((nuint)templateFontData.Length);
                templateFontData.AsSpan().CopyTo(new Span<byte>(fontNative, templateFontData.Length));
                font = atlas.AddFontFromMemoryTTF(
                    new ReadOnlySpan<byte>(fontNative, templateFontData.Length),
                    sizePx,
                    cfg,
                    spaceRange);
                cfg.Destroy();
            }

            if (font.IsNull)
            {
                ImGui.MemFree(spaceRange);
                return null;
            }

            gameFont = font;

            var rects = new List<(int RectId, int GlyphIdx)>(fdt.Glyphs.Count);
            var neededTexFiles = new HashSet<int>();
            for (var i = 0; i < fdt.Glyphs.Count; i++)
            {
                var g = fdt.Glyphs[i];
                var cp = g.CharInt;

                if (cp < 0x0020 || cp > 0xFFFE) continue;
                if (cp == 0x0020) continue;
                if (g.BoundingWidth == 0 || g.BoundingHeight == 0) continue;

                var rectId = atlas.AddCustomRectFontGlyph(
                    font,
                    (ushort)cp,
                    g.BoundingWidth,
                    g.BoundingHeight,
                    (float)g.AdvanceWidth,
                    new Vector2(info.HorizontalOffset, g.CurrentOffsetY));

                rects.Add((rectId, i));
                neededTexFiles.Add(g.TextureFileIndex);
            }

            var texFiles = new Dictionary<int, TexFile>();
            foreach (var fileIndex in neededTexFiles)
            {
                var texPath = string.Format(TexPathFormat, fileIndex + 1);
                var texFile = gameData.GetFile<TexFile>(texPath);
                if (texFile is not null)
                {
                    texFiles[fileIndex] = texFile;
                }
            }

            rects.RemoveAll(r => !texFiles.ContainsKey(fdt.Glyphs[r.GlyphIdx].TextureFileIndex));

            var job = new GameFontBakeJob(atlas, fdt, texFiles, rects, font, sizePx, spaceRange);
            spaceRange = null;
            return job;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[GameFontBuildHelper] TryPrebuild threw: " + ex);
            if (spaceRange != null)
            {
                ImGui.MemFree(spaceRange);
            }

            return null;
        }
    }
}
