// <copyright file="PreviewConfigLoaderTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.Configuration;

using Xunit;

namespace Echoglossian.Previewer.Tests.Configuration;

/// <summary>
/// Covers read-only loading of the previewer's source plugin configuration.
/// </summary>
public sealed class PreviewConfigLoaderTests : IDisposable
{
    private readonly string temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        "Echoglossian.Previewer.Tests",
        Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewConfigLoaderTests" /> class.
    /// </summary>
    public PreviewConfigLoaderTests()
    {
        Directory.CreateDirectory(this.temporaryDirectory);
    }

    /// <summary>
    /// Ensures the default source path follows the XIVLauncher configuration convention.
    /// </summary>
    [Fact]
    public void DefaultConfigPath_UsesXivLauncherPluginConfigDirectory()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XIVLauncher",
            "pluginConfigs",
            "Echoglossian.json");

        Assert.Equal(expected, PreviewConfigLoader.GetDefaultConfigPath());
    }

    /// <summary>
    /// Ensures explicit absolute and relative paths resolve to the same source file.
    /// </summary>
    [Fact]
    public void Load_ResolvesExplicitAbsoluteAndRelativePaths()
    {
        var sourcePath = this.CreateConfigFile("config.json", "{ \"FontSize\": 31 }");
        var relativePath = Path.GetRelativePath(Environment.CurrentDirectory, sourcePath);

        var absolute = PreviewConfigLoader.Load(sourcePath);
        var relative = PreviewConfigLoader.Load(relativePath);

        Assert.True(absolute.Loaded, string.Join(Environment.NewLine, absolute.Diagnostics));
        Assert.True(relative.Loaded, string.Join(Environment.NewLine, relative.Diagnostics));
        Assert.Equal(sourcePath, absolute.SourcePath);
        Assert.Equal(sourcePath, relative.SourcePath);
        Assert.Equal(31, relative.Config.FontSize);
    }

    /// <summary>
    /// Ensures a missing source file produces a new default configuration without writing it.
    /// </summary>
    [Fact]
    public void Load_MissingFile_ReturnsNewConfigWithoutCreatingSource()
    {
        var sourcePath = Path.Combine(this.temporaryDirectory, "missing.json");

        var result = PreviewConfigLoader.Load(sourcePath);

        Assert.False(result.Loaded);
        Assert.Equal(sourcePath, result.SourcePath);
        Assert.False(File.Exists(sourcePath));
        Assert.Equal(24, result.Config.FontSize);
    }

    /// <summary>
    /// Ensures malformed source JSON does not expose secrets or change the source file.
    /// </summary>
    [Fact]
    public void Load_MalformedJson_ReturnsRedactedDiagnosticWithoutOverwritingSource()
    {
        const string apiKey = "preview-test-api-key";
        const string secret = "preview-test-aws-secret";
        var source = $"{{ \"ChatGptApiKey\": \"{apiKey}\", \"AwsSecretKey\": \"{secret}\"";
        var sourcePath = this.CreateConfigFile("malformed.json", source);

        var result = PreviewConfigLoader.Load(sourcePath);

        Assert.False(result.Loaded);
        Assert.Equal(source, File.ReadAllText(sourcePath));
        Assert.NotEmpty(result.Diagnostics);
        var diagnostics = string.Join(Environment.NewLine, result.Diagnostics);
        Assert.DoesNotContain(apiKey, diagnostics, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, diagnostics, StringComparison.Ordinal);
        Assert.DoesNotContain(source, diagnostics, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures preview edits are isolated from the loaded configuration snapshot.
    /// </summary>
    [Fact]
    public void CreateEditableCopy_DeepClonesLoadedConfiguration()
    {
        var sourcePath = this.CreateConfigFile(
            "clone.json",
            "{ \"FontSize\": 29, \"ChatGptApiKey\": \"preview-test-api-key\" }");
        var result = PreviewConfigLoader.Load(sourcePath);

        var editable = result.CreateEditableCopy();
        editable.FontSize = 42;
        editable.ChatGptApiKey = "changed-in-preview";

        Assert.Equal(29, result.Config.FontSize);
        Assert.Equal("preview-test-api-key", result.Config.ChatGptApiKey);
        Assert.Contains("\"FontSize\": 29", File.ReadAllText(sourcePath), StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(this.temporaryDirectory))
        {
            Directory.Delete(this.temporaryDirectory, recursive: true);
        }
    }

    /// <summary>
    /// Creates a source configuration file for one test.
    /// </summary>
    /// <param name="fileName">The source file name.</param>
    /// <param name="contents">The JSON contents.</param>
    /// <returns>The absolute source file path.</returns>
    private string CreateConfigFile(string fileName, string contents)
    {
        var sourcePath = Path.Combine(this.temporaryDirectory, fileName);
        File.WriteAllText(sourcePath, contents);
        return sourcePath;
    }
}
