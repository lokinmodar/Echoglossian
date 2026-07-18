namespace DalaMock.Core.Plugin;

public class MockPluginManifest : IPluginManifest
{
    public string InternalName { get; set; }

    public string Name { get; set; }

    public string? Punchline { get; set; }

    public string Author { get; set; }

    public bool CanUnloadAsync { get; set; }

    public Version AssemblyVersion { get; set; }

    public Version? TestingAssemblyVersion { get; set; }

    public Version? MinimumDalamudVersion { get; set; }

    public string? Dip17Channel { get; set; }

    public long LastUpdate { get; set; }

    public string? Changelog { get; set; }

    public List<string>? Tags { get; set; }

    public int DalamudApiLevel { get; set; }

    public int? TestingDalamudApiLevel { get; set; }

    public long DownloadCount { get; set; }

    public bool SupportsProfiles { get; set; }

    public string? RepoUrl { get; set; }

    public string? Description { get; set; }

    public string? FeedbackMessage { get; set; }

    public bool IsTestingExclusive { get; set; }

    public List<string>? ImageUrls { get; set; }

    public string? IconUrl { get; set; }

    public bool IsAvailableForTesting { get; set; }
}
