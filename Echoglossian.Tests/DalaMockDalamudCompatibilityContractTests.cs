// <copyright file="DalaMockDalamudCompatibilityContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Verifies the vendored DalaMock services remain compatible with the current
/// Dalamud members required to build the Mock validation harness.
/// </summary>
public sealed class DalaMockDalamudCompatibilityContractTests
{
    /// <summary>
    /// Ensures MockFramework implements the current debouncer factory member.
    /// </summary>
    [Fact]
    public void MockFramework_implements_current_debouncer_factory_contract()
    {
        var source = this.ReadVendorSource("Mocks", "DalamudServices", "MockFramework.cs");

        Assert.Contains("public IDebouncer CreateDebouncer(TimeSpan", source);
        Assert.Contains("System.Action action", source);
    }

    /// <summary>
    /// Ensures MockCharacter implements the current distance members as bytes.
    /// </summary>
    [Fact]
    public void MockCharacter_implements_current_byte_distance_contract()
    {
        var source = this.ReadVendorSource("Mocks", "Objects", "MockCharacter.cs");

        Assert.Contains("public byte CurrentDistance", source);
        Assert.Contains("public byte NextDistance", source);
    }

    /// <summary>
    /// Reads a source file from the vendored DalaMock project.
    /// </summary>
    /// <param name="pathSegments">The source file path relative to the DalaMock project.</param>
    /// <returns>The source file contents.</returns>
    private string ReadVendorSource(params string[] pathSegments)
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(new[] { root.FullName, "vendor", "DalaMock", "DalaMock" }.Concat(pathSegments).ToArray());
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Finds the repository root from the test output directory.
    /// </summary>
    /// <returns>The repository root directory.</returns>
    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Echoglossian.sln")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root.");
    }
}
