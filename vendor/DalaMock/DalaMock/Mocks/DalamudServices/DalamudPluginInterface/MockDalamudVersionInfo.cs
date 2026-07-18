namespace DalaMock.Core.Mocks.DalamudServices.DalamudPluginInterface;

public class MockDalamudVersionInfo : IDalamudVersionInfo
{
    public MockDalamudVersionInfo(Version version)
    {
        this.Version = version;
    }

    public Version Version { get; set; }

    public string? BetaTrack { get; set; }

    public string? GitHash { get; set; }

    public string? GitHashClientStructs { get; set; }

    public string? ScmVersion { get; set; }
}
