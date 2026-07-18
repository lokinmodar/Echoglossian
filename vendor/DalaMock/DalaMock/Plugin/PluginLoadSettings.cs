namespace DalaMock.Core.Plugin;

/// <summary>
/// The settings to use when loading the plugin.
/// </summary>
public class PluginLoadSettings(DirectoryInfo configDir, FileInfo configFile)
{
    /// <summary>
    /// Gets the configuration directory to provide to the plugin.
    /// </summary>
    public DirectoryInfo ConfigDir { get; private set; } = configDir;

    /// <summary>
    /// Gets the configuration file to provide to the plugin.
    /// </summary>
    public FileInfo ConfigFile { get; private set; } = configFile;

    /// <summary>
    /// Gets or sets the location of the assembly that contains the plugin.
    /// If not provided, the location of the current executable will be used.
    /// </summary>
    public string? AssemblyLocation { get; set; }

    public PluginLoadReason PluginLoadReason { get; set; } = PluginLoadReason.Boot;
}
