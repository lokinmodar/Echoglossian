namespace DalaMock.Core.Fonts.SystemFonts;

/// <summary>
/// Represents a single face of a system-installed font, resolved to a concrete file path and face
/// index. Cross-platform replacement for Dalamud's DirectWrite-backed <c>SystemFontId</c>; the font
/// is loaded into the build toolkit directly from the file instead of via DirectWrite.
/// </summary>
internal sealed class MockSystemFontId : IFontId
{
    public MockSystemFontId(
        IFontFamilyId family,
        string faceName,
        string path,
        int faceIndex,
        int weight,
        int stretch,
        int style)
    {
        this.Family = family;
        this.EnglishName = faceName;
        this.Path = path;
        this.FaceIndex = faceIndex;
        this.Weight = weight;
        this.Stretch = stretch;
        this.Style = style;
    }

    public string EnglishName { get; }

    public IReadOnlyDictionary<string, string>? LocaleNames => null;

    public IFontFamilyId Family { get; }

    public int Weight { get; }

    public int Stretch { get; }

    public int Style { get; }

    /// <summary>Gets the absolute path of the font file backing this face.</summary>
    public string Path { get; }

    /// <summary>Gets the face index within the file (0 for single-face files, &gt;0 for collections).</summary>
    public int FaceIndex { get; }

    public override bool Equals(object? obj) => ReferenceEquals(this, obj) || (obj is MockSystemFontId other && this.Equals(other));

    public override int GetHashCode() => HashCode.Combine(this.Family, this.Weight, this.Stretch, this.Style);

    public override string ToString() => $"{nameof(MockSystemFontId)}:{this.Weight}:{this.Stretch}:{this.Style}:{this.Family}";

    public ImFontPtr AddToBuildToolkit(IFontAtlasBuildToolkitPreBuild tk, in SafeFontConfig config) => tk.AddFontFromFile(this.Path, config with { FontNo = this.FaceIndex });

    private bool Equals(MockSystemFontId other) =>
        this.Family.Equals(other.Family) && this.Weight == other.Weight &&
        this.Stretch == other.Stretch && this.Style == other.Style;
}
