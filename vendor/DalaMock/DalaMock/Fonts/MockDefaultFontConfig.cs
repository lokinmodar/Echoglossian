namespace DalaMock.Core.Fonts;

/// <summary>
/// A serializable representation of a chosen default font. Stored in
/// <see cref="MockDalamudConfiguration"/> so the choice survives restarts. A DTO is used instead of
/// serializing <see cref="SingleFontSpec"/> directly because its <c>IFontId</c> is polymorphic and the
/// concrete Dalamud font-id types don't round-trip cleanly through JSON.
/// </summary>
public class MockDefaultFontConfig
{
    public enum FontKind
    {
        DalamudDefault,
        Game,
        DalamudAsset,
        System,
    }

    public FontKind Kind { get; set; }

    public float SizePt { get; set; } = 12f;

    public float LineHeight { get; set; } = 1f;

    public Vector2 GlyphOffset { get; set; }

    public float LetterSpacing { get; set; }

    public int FontNo { get; set; }

    public GameFontFamily GameFamily { get; set; }

    public DalamudAsset Asset { get; set; }

    public string? SystemFamilyName { get; set; }

    /// <summary>
    /// Builds a config DTO from a chosen spec, or returns <c>null</c> if the font-id type can't be
    /// persisted.
    /// </summary>
    /// <param name="spec">The chosen font spec.</param>
    /// <returns>The DTO, or <c>null</c>.</returns>
    public static MockDefaultFontConfig? FromSpec(SingleFontSpec spec)
    {
        var config = new MockDefaultFontConfig
        {
            SizePt = spec.SizePt,
            LineHeight = spec.LineHeight,
            GlyphOffset = spec.GlyphOffset,
            LetterSpacing = spec.LetterSpacing,
            FontNo = spec.FontNo,
        };

        switch (spec.FontId)
        {
            case GameFontAndFamilyId game:
                config.Kind = FontKind.Game;
                config.GameFamily = game.GameFontFamily;
                break;
            case DalamudAssetFontAndFamilyId asset:
                config.Kind = FontKind.DalamudAsset;
                config.Asset = asset.Asset;
                break;
            case DalamudDefaultFontAndFamilyId:
                config.Kind = FontKind.DalamudDefault;
                break;
            case MockSystemFontId system:
                config.Kind = FontKind.System;
                config.SystemFamilyName = system.Family.EnglishName;
                break;
            default:
                return null;
        }

        return config;
    }

    /// <summary>
    /// Resolves this config back into a <see cref="SingleFontSpec"/>, using
    /// <paramref name="systemFontProvider"/> to look up system fonts by family name. Returns
    /// <c>null</c> if the font can no longer be resolved (e.g. a system font that is no longer installed).
    /// </summary>
    /// <param name="systemFontProvider">The system font provider.</param>
    /// <returns>The resolved spec, or <c>null</c>.</returns>
    public SingleFontSpec? ToSpec(MockSystemFontProvider systemFontProvider)
    {
        IFontId? fontId = this.Kind switch
        {
            FontKind.DalamudDefault => DalamudDefaultFontAndFamilyId.Instance,
            FontKind.Game => new GameFontAndFamilyId(this.GameFamily),
            FontKind.DalamudAsset => new DalamudAssetFontAndFamilyId(this.Asset),
            FontKind.System => ResolveSystemFont(systemFontProvider, this.SystemFamilyName),
            _ => null,
        };

        if (fontId is null)
        {
            return null;
        }

        return new SingleFontSpec
        {
            FontId = fontId,
            SizePt = this.SizePt,
            LineHeight = this.LineHeight,
            GlyphOffset = this.GlyphOffset,
            LetterSpacing = this.LetterSpacing,
            FontNo = this.FontNo,
        };
    }

    private static IFontId? ResolveSystemFont(MockSystemFontProvider systemFontProvider, string? familyName)
    {
        if (string.IsNullOrEmpty(familyName))
        {
            return null;
        }

        var family = systemFontProvider.GetFamilies().FirstOrDefault(f => f.EnglishName == familyName);
        if (family is null || family.Fonts.Count == 0)
        {
            return null;
        }

        var index = family.FindBestMatch(
            (int)DWRITE_FONT_WEIGHT.DWRITE_FONT_WEIGHT_NORMAL,
            (int)DWRITE_FONT_STRETCH.DWRITE_FONT_STRETCH_NORMAL,
            (int)DWRITE_FONT_STYLE.DWRITE_FONT_STYLE_NORMAL);
        return family.Fonts[index];
    }
}
