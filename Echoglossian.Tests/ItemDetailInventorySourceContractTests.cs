// <copyright file="ItemDetailInventorySourceContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Verifies that ItemDetail prefetch observes loaded inventory containers
///     dynamically instead of relying on a fixed hand-maintained container list.
/// </summary>
public class ItemDetailInventorySourceContractTests
{
    /// <summary>
    ///     Ensures item prefetch enumerates all loaded <c>InventoryType</c>
    ///     containers through <c>InventoryManager</c> and keeps hotbars as a
    ///     supplemental source.
    /// </summary>
    [Fact]
    public void ItemDetailPrefetch_uses_dynamic_inventory_type_enumeration()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "Helpers",
            "ItemDetailPrefetchRuntime.cs"));

        Assert.Contains(
            "Enum.GetValues<InventoryType>()",
            source);
        Assert.DoesNotContain(
            "PrefetchInventoryTypes",
            source);
        Assert.Contains(
            "container->IsLoaded",
            source);
        Assert.Contains(
            "container->Size == 0",
            source);
        Assert.Contains(
            "RaptureHotbarModule.Instance()",
            source);
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
