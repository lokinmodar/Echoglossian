// <copyright file="PreviewerIsolationTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Xml.Linq;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the project-boundary contract for the standalone ImGui previewer.
/// </summary>
public class PreviewerIsolationTests
{
    /// <summary>
    ///     Ensures the plugin project excludes both previewer directories from
    ///     every SDK-default item type that could enter plugin packaging.
    /// </summary>
    [Theory]
    [InlineData("Compile")]
    [InlineData("EmbeddedResource")]
    [InlineData("None")]
    public void MainProject_ExcludesPreviewerDirectories(string itemType)
    {
        var project = this.LoadProject("Echoglossian.csproj");

        var exclusions = project
            .Descendants(itemType)
            .Attributes("Remove")
            .SelectMany(attribute => attribute.Value.Split(';'))
            .Select(value => value.Replace('/', '\\'))
            .ToArray();

        Assert.Contains("Echoglossian.Previewer\\**", exclusions);
        Assert.Contains("Echoglossian.Previewer.Tests\\**", exclusions);
    }

    /// <summary>
    ///     Ensures the plugin project remains free of previewer dependencies
    ///     and grants the standalone executable access to shared internals.
    /// </summary>
    [Fact]
    public void MainProject_PreservesPreviewerDependencyBoundary()
    {
        var project = this.LoadProject("Echoglossian.csproj");

        Assert.DoesNotContain(
            project.Descendants("PackageReference"),
            reference => reference.Attribute("Include")?.Value.StartsWith(
                "Veldrid",
                StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(
            project.Descendants("InternalsVisibleTo"),
            friend => friend.Attribute("Include")?.Value ==
                "Echoglossian.Previewer");
    }

    /// <summary>
    ///     Ensures the standalone previewer remains outside the plugin
    ///     solution and cannot be packed as a production artifact.
    /// </summary>
    [Fact]
    public void PreviewerProject_RemainsStandaloneAndUnpackable()
    {
        var solution = File.ReadAllText(
            Path.Combine(this.RepositoryRoot, "Echoglossian.sln"));
        var previewerProjectPath = Path.Combine(
            this.RepositoryRoot,
            "Echoglossian.Previewer",
            "Echoglossian.Previewer.csproj");

        Assert.DoesNotContain("Echoglossian.Previewer", solution);
        Assert.True(
            File.Exists(previewerProjectPath),
            "The standalone previewer project must exist.");

        var previewerProject = XDocument.Load(previewerProjectPath);

        Assert.Contains(
            previewerProject.Descendants("IsPackable"),
            property => property.Value.Equals("false", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Gets the repository root discovered from the test output directory.
    /// </summary>
    private string RepositoryRoot => FindRepositoryRoot();

    /// <summary>
    ///     Loads an SDK-style project document from the repository root.
    /// </summary>
    /// <param name="projectFileName">The project file name.</param>
    /// <returns>The parsed project document.</returns>
    private XDocument LoadProject(string projectFileName)
    {
        return XDocument.Load(Path.Combine(this.RepositoryRoot, projectFileName));
    }

    /// <summary>
    ///     Finds the repository root by walking upward from the test output
    ///     directory until the solution file is found.
    /// </summary>
    /// <returns>The absolute repository-root path.</returns>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Echoglossian.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the Echoglossian repository root.");
    }
}
