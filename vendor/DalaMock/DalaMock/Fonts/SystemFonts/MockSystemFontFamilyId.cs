namespace DalaMock.Core.Fonts.SystemFonts;

/// <summary>
/// Represents a system-installed font family. Cross-platform replacement for Dalamud's
/// DirectWrite-backed <c>SystemFontFamilyId</c>; faces are discovered by
/// <see cref="MockSystemFontProvider"/> and resolved to concrete file paths.
/// </summary>
internal sealed class MockSystemFontFamilyId : IFontFamilyId
{
    private readonly List<IFontId> fonts = new();

    public MockSystemFontFamilyId(string englishName)
    {
        this.EnglishName = englishName;
    }

    public string EnglishName { get; }

    public IReadOnlyDictionary<string, string>? LocaleNames => null;

    public IReadOnlyList<IFontId> Fonts => this.fonts;

    public static bool operator ==(MockSystemFontFamilyId? left, MockSystemFontFamilyId? right) => Equals(left, right);

    public static bool operator !=(MockSystemFontFamilyId? left, MockSystemFontFamilyId? right) => !Equals(left, right);

    public void AddFont(IFontId font) => this.fonts.Add(font);

    /// <summary>
    /// Finds the index of the face that best matches the requested weight/stretch/style, mirroring
    /// Dalamud's <c>SystemFontFamilyId.FindBestMatch</c>.
    /// </summary>
    public int FindBestMatch(int weight, int stretch, int style)
    {
        var candidates = this.fonts.ToList();
        if (candidates.Count == 0)
        {
            return 0;
        }

        var minGap = candidates.Min(c => Math.Abs(c.Weight - weight));
        candidates.RemoveAll(c => Math.Abs(c.Weight - weight) != minGap);

        minGap = candidates.Min(c => Math.Abs(c.Stretch - stretch));
        candidates.RemoveAll(c => Math.Abs(c.Stretch - stretch) != minGap);

        if (candidates.Any(x => x.Style == style))
        {
            candidates.RemoveAll(x => x.Style != style);
        }
        else if (candidates.Any(x => x.Style == (int)DWRITE_FONT_STYLE.DWRITE_FONT_STYLE_NORMAL))
        {
            candidates.RemoveAll(x => x.Style != (int)DWRITE_FONT_STYLE.DWRITE_FONT_STYLE_NORMAL);
        }

        if (candidates.Count == 0)
        {
            return 0;
        }

        for (var i = 0; i < this.fonts.Count; i++)
        {
            if (Equals(this.fonts[i], candidates[0]))
            {
                return i;
            }
        }

        return 0;
    }

    public override bool Equals(object? obj) =>
        ReferenceEquals(this, obj) || (obj is MockSystemFontFamilyId other && this.EnglishName == other.EnglishName);

    public override int GetHashCode() => this.EnglishName.GetHashCode();

    public override string ToString() => $"{nameof(MockSystemFontFamilyId)}:{this.EnglishName}";
}
