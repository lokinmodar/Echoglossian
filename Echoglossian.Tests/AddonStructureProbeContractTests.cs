// <copyright file="AddonStructureProbeContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Verifies addon-probe contracts used to diagnose live game UI surfaces.
/// </summary>
public sealed class AddonStructureProbeContractTests
{
    /// <summary>
    ///     Ensures StringArrayData subscriptions include bounded value samples
    ///     so surfaces without visible text nodes can still be diagnosed.
    /// </summary>
    [Fact]
    public void AddonProbe_logs_string_array_value_samples()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "Helpers",
            "AddonStructureProbe.cs"));

        Assert.Contains("StringArraySampleMaxEntries", source);
        Assert.Contains("BuildStringArrayValueSample", source);
        Assert.Contains("sampleValues=", source);
    }

    /// <summary>
    ///     Finds the repository root from the test output directory.
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
