namespace DalaMock.Core.Fonts.SystemFonts;

using SixLabors.Fonts;

/// <summary>
/// Enumerates the fonts installed on the host system in a cross-platform way and exposes them as
/// <see cref="IFontFamilyId"/>s usable by the mock font chooser. Replaces Dalamud's DirectWrite-only
/// <c>IFontFamilyId.ListSystemFonts</c>. Font metadata is parsed with SixLabors.Fonts; the file
/// paths come from scanning the platform's font directories so the faces can be loaded directly.
/// </summary>
public sealed class MockSystemFontProvider
{
    private static readonly string[] FontExtensions = [".ttf", ".otf", ".ttc", ".otc"];
    
    private readonly object syncRoot = new();
    private List<IFontFamilyId>? cache;
    
    /// <summary>
    /// Gets the list of system font families, sorted by name.
    /// </summary>
    /// <param name="refresh">If <c>true</c>, re-scans the system rather than returning the cache.</param>
    /// <returns>The system font families.</returns>
    public List<IFontFamilyId> GetFamilies(bool refresh = false)
    {
        lock (this.syncRoot)
        {
            if (!refresh && this.cache is not null)
            {
                return this.cache;
            }
            
            this.cache = Scan();
            return this.cache;
        }
    }
    
    private static List<IFontFamilyId> Scan()
    {
        var families = new Dictionary<string, MockSystemFontFamilyId>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var file in EnumerateFontFiles())
        {
            FontDescription[] descriptions;
            try
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                descriptions = ext is ".ttc" or ".otc"
                                   ? FontDescription.LoadFontCollectionDescriptions(file)
                                   : [FontDescription.LoadDescription(file)];
            }
            catch (Exception ex)
            {
                Log.Verbose(ex, "[MockSystemFontProvider] failed to read font {File}", file);
                continue;
            }
            
            for (var faceIndex = 0; faceIndex < descriptions.Length; faceIndex++)
            {
                var description = descriptions[faceIndex];
                var familyName = description.FontFamilyInvariantCulture;
                if (string.IsNullOrWhiteSpace(familyName))
                {
                    continue;
                }
                
                if (!families.TryGetValue(familyName, out var family))
                {
                    family = new MockSystemFontFamilyId(familyName);
                    families[familyName] = family;
                }
                
                var isBold = description.Style.HasFlag(FontStyle.Bold);
                var isItalic = description.Style.HasFlag(FontStyle.Italic);
                var weight = isBold
                                 ? (int)DWRITE_FONT_WEIGHT.DWRITE_FONT_WEIGHT_BOLD
                                 : (int)DWRITE_FONT_WEIGHT.DWRITE_FONT_WEIGHT_NORMAL;
                var style = isItalic
                                ? (int)DWRITE_FONT_STYLE.DWRITE_FONT_STYLE_ITALIC
                                : (int)DWRITE_FONT_STYLE.DWRITE_FONT_STYLE_NORMAL;
                var stretch = (int)DWRITE_FONT_STRETCH.DWRITE_FONT_STRETCH_NORMAL;
                
                // Skip a face whose weight/stretch/style is already represented in the family —
                // mirrors DirectWrite, which exposes one face per distinct combination.
                if (family.Fonts.Any(f => f.Weight == weight && f.Stretch == stretch && f.Style == style))
                {
                    continue;
                }
                
                var faceName = string.IsNullOrWhiteSpace(description.FontSubFamilyNameInvariantCulture)
                                   ? "Regular"
                                   : description.FontSubFamilyNameInvariantCulture;
                
                family.AddFont(new MockSystemFontId(family, faceName, file, faceIndex, weight, stretch, style));
            }
        }
        
        var result = families.Values
                             .Where(f => f.Fonts.Count > 0)
                             .Cast<IFontFamilyId>()
                             .ToList();
        result.Sort((a, b) => string.Compare(a.EnglishName, b.EnglishName, StringComparison.CurrentCultureIgnoreCase));
        return result;
    }
    
    private static IEnumerable<string> EnumerateFontFiles()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in FontDirectories())
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                continue;
            }
            
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories);
            }
            catch (Exception ex)
            {
                Log.Verbose(ex, "[MockSystemFontProvider] failed to enumerate {Dir}", dir);
                continue;
            }
            
            foreach (var file in files)
            {
                if (FontExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()) && seen.Add(file))
                {
                    yield return file;
                }
            }
        }
    }
    
    private static IEnumerable<string> FontDirectories()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                yield return Path.Combine(localAppData, "Microsoft", "Windows", "Fonts");
            }
            
            yield break;
        }
        
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS())
        {
            yield return "/System/Library/Fonts";
            yield return "/Library/Fonts";
            if (!string.IsNullOrEmpty(home))
            {
                yield return Path.Combine(home, "Library", "Fonts");
            }
            
            yield break;
        }
        
        // Linux / other unix.
        yield return "/usr/share/fonts";
        yield return "/usr/local/share/fonts";
        yield return "/run/host/usr/share/fonts";
        if (!string.IsNullOrEmpty(home))
        {
            yield return Path.Combine(home, ".fonts");
            yield return Path.Combine(home, ".local", "share", "fonts");
        }
    }
}
