namespace DalaMock.Core.Fonts.Atlas;

/// <summary>
/// Maps <see cref="DalamudAsset"/> values to bundled embedded resources shipped with DalaMock.
/// </summary>
internal static class MockFontAtlasResources
{
    /// <summary>
    /// Reads the embedded resource bytes for the given <see cref="DalamudAsset"/> font.
    /// Falls back to <c>gf.ttf</c> for unrecognized values so plugin builds never throw.
    /// </summary>
    /// <returns>The asset as a byte array.</returns>
    public static byte[] LoadAssetBytes(DalamudAsset asset)
    {
        var resourceName = asset switch
        {
            DalamudAsset.NotoSansCjkMedium => "NotoSansCJK-Medium.ttc",
            DalamudAsset.NotoSansCjkRegular => "NotoSansCJK-Regular.ttc",
            DalamudAsset.InconsolataRegular => "Inconsolata-Regular.ttf",
            DalamudAsset.FontAwesomeFreeSolid => "FontAwesome710FreeSolid.otf",
            DalamudAsset.LodestoneGameSymbol => "gf.ttf",
            _ => "gf.ttf",
        };

        return LoadEmbeddedResource(resourceName);
    }

    /// <summary>
    /// Reads an embedded font resource from the DalaMock assembly by its logical name.
    /// </summary>
    /// <returns>The embedded resource as a byte array.</returns>
    public static byte[] LoadEmbeddedResource(string resourceName)
    {
        var assembly = typeof(MockFontAtlasResources).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new FileNotFoundException(
                $"Embedded resource '{resourceName}' not found in {assembly.GetName().Name}. "
                + $"Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}");
        }

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var data = ms.ToArray();
        if (data.Length == 0)
        {
            throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' in {assembly.GetName().Name} has 0 bytes — the file may not have been included in the NuGet package.");
        }

        return data;
    }


    internal static void VerifyEmbeddedResources()
    {
        string[] required =
        [
            "gf.ttf",
            "NotoSansCJKjp-Medium.otf",
            "NotoSansCJK-Medium.ttc",
            "NotoSansCJK-Regular.ttc",
            "Inconsolata-Regular.ttf",
            "FontAwesome710FreeSolid.otf",
            "FontAwesomeFreeSolid.otf",
            "NotoSansKR-Regular.otf",
        ];

        var assembly = typeof(MockFontAtlasResources).Assembly;
        var available = assembly.GetManifestResourceNames();

        var missing = new List<string>();
        var empty = new List<string>();

        foreach (var name in required)
        {
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream == null)
            {
                missing.Add(name);
            }
            else if (stream.Length == 0)
            {
                empty.Add(name);
            }
        }

        if (missing.Count == 0 && empty.Count == 0)
        {
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[DalaMock] Embedded font resource validation failed in assembly '{assembly.GetName().Name}'.");

        if (missing.Count > 0)
        {
            sb.AppendLine($"  Missing ({missing.Count}): {string.Join(", ", missing)}");
        }

        if (empty.Count > 0)
        {
            sb.AppendLine($"  Empty/zero-byte ({empty.Count}): {string.Join(", ", empty)}");
        }

        sb.AppendLine($"  Available resources in assembly: {string.Join(", ", available)}");
        sb.AppendLine("  If you are referencing DalaMock via NuGet, the package may have been built without the font files.");

        throw new InvalidOperationException(sb.ToString());
    }
}
