// <copyright file="TooltipAddonCacheContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Guards the dedicated Tooltip addon cache wiring contract.
/// </summary>
public sealed class TooltipAddonCacheContractTests
{
    /// <summary>
    ///     Ensures plugin startup and shutdown preload and clear the dedicated
    ///     Tooltip cache alongside the other canonical caches.
    /// </summary>
    [Fact]
    public void Echoglossian_WiresTooltipTextCacheLifecycle()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root.FullName, "Echoglossian.cs"));

        Assert.Contains(
            "TooltipTextCacheManager.Preload(ConfigDirectory);",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "TooltipTextCacheManager.Clear();",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the dedicated Tooltip persistence path consults and updates
    ///     the in-memory cache instead of always hitting SQLite from the UI
    ///     thread.
    /// </summary>
    [Fact]
    public void DbOperations_TooltipPersistencePathUsesTooltipTextCache()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "DBHelpers",
            "DbOperations.cs"));

        Assert.Contains(
            "TooltipTextCacheManager.TryFindMatch(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "TooltipTextCacheManager.GetCandidates(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "TooltipTextCacheManager.Update(",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the dedicated Tooltip cache protects its shared in-memory
    ///     indexes from concurrent hot-path reads and asynchronous writes.
    /// </summary>
    [Fact]
    public void TooltipTextCacheManager_synchronizes_shared_indexes()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "Cache",
            "TooltipTextCacheManager.cs"));

        Assert.Contains(
            "ReaderWriterLockSlim",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "EnterReadLock()",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "ExitReadLock()",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "EnterWriteLock()",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "ExitWriteLock()",
            source,
            StringComparison.Ordinal);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null &&
               !File.Exists(Path.Combine(current.FullName, "Echoglossian.sln")))
        {
            current = current.Parent;
        }

        return current ?? throw new DirectoryNotFoundException(
            "Could not locate Echoglossian.sln from the test output directory.");
    }
}
