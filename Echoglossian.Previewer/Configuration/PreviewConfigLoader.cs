// <copyright file="PreviewConfigLoader.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Newtonsoft.Json;

namespace Echoglossian.Previewer.Configuration;

/// <summary>
/// Loads plugin configuration files into an isolated, preview-only snapshot.
/// </summary>
public static class PreviewConfigLoader
{
    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        MissingMemberHandling = MissingMemberHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    };

    /// <summary>
    /// Gets the default XIVLauncher plugin configuration path.
    /// </summary>
    /// <returns>The default absolute configuration path.</returns>
    public static string GetDefaultConfigPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XIVLauncher",
            "pluginConfigs",
            "Echoglossian.json");
    }

    /// <summary>
    /// Loads a configuration file without modifying, saving, or logging its contents.
    /// </summary>
    /// <param name="path">An optional absolute or relative source path.</param>
    /// <returns>An isolated configuration snapshot and non-secret diagnostics.</returns>
    public static PreviewConfiguration Load(string? path)
    {
        var sourcePath = ResolvePath(path);

        if (!File.Exists(sourcePath))
        {
            return new PreviewConfiguration(
                new Config(),
                sourcePath,
                loaded: false,
                ["Preview configuration file was not found; using defaults."]);
        }

        try
        {
            using var stream = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            using var jsonReader = new JsonTextReader(reader);
            var loadedConfig = JsonSerializer.Create(SerializerSettings)
                .Deserialize<Config>(jsonReader) ?? new Config();
            var isolatedConfig = DeepClone(loadedConfig);

            return new PreviewConfiguration(
                isolatedConfig,
                sourcePath,
                loaded: true,
                []);
        }
        catch (Exception exception) when (
            exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return new PreviewConfiguration(
                new Config(),
                sourcePath,
                loaded: false,
                [$"Preview configuration could not be loaded: {GetSafeExceptionName(exception)}."]);
        }
    }

    /// <summary>
    /// Resolves an optional source path without touching the source file.
    /// </summary>
    /// <param name="path">The optional absolute or relative source path.</param>
    /// <returns>The absolute source path.</returns>
    private static string ResolvePath(string? path)
    {
        return Path.GetFullPath(
            string.IsNullOrWhiteSpace(path) ? GetDefaultConfigPath() : path);
    }

    /// <summary>
    /// Creates a serialization-compatible deep clone without exposing it to diagnostics.
    /// </summary>
    /// <param name="config">The deserialized source configuration.</param>
    /// <returns>An independent preview configuration snapshot.</returns>
    private static Config DeepClone(Config config)
    {
        var serialized = JsonConvert.SerializeObject(config, SerializerSettings);
        return JsonConvert.DeserializeObject<Config>(serialized, SerializerSettings) ?? new Config();
    }

    /// <summary>
    /// Returns only an exception type name so source JSON and secrets cannot enter diagnostics.
    /// </summary>
    /// <param name="exception">The load exception.</param>
    /// <returns>A non-secret diagnostic exception name.</returns>
    private static string GetSafeExceptionName(Exception exception)
    {
        return exception.GetType().Name;
    }
}
