// <copyright file="PreviewSessionLoaderTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.Session;

using Xunit;

namespace Echoglossian.Previewer.Tests.Session;

/// <summary>
/// Covers isolated preview session snapshots.
/// </summary>
public sealed class PreviewSessionLoaderTests
{
    /// <summary>
    /// Ensures config and database sources are cloned into preview-owned session storage.
    /// </summary>
    [Fact]
    public void Load_ClonesConfigAndDatabaseIntoSessionWorkspace()
    {
        var tempRoot = Directory.CreateTempSubdirectory();
        var configPath = Path.Combine(tempRoot.FullName, "Echoglossian.json");
        var databasePath = Path.Combine(tempRoot.FullName, "Echoglossian.db");

        File.WriteAllText(configPath, "{\"Lang\":28,\"FontSize\":24}");
        File.WriteAllText(databasePath, "preview-db");

        using var session = PreviewSessionLoader.Load(
            new PreviewSessionSourceOptions(configPath, databasePath, null));

        Assert.NotEqual(configPath, session.ClonedConfigPath);
        Assert.NotEqual(databasePath, session.ClonedDatabasePath);
        Assert.True(File.Exists(session.ClonedConfigPath));
        Assert.True(File.Exists(session.ClonedDatabasePath));
        Assert.Equal(28, session.EditableConfiguration.Lang);
    }

    /// <summary>
    /// Ensures a missing database remains unavailable without modifying live sources.
    /// </summary>
    [Fact]
    public void Load_AllowsMissingDatabaseWithoutTouchingLiveSources()
    {
        var tempRoot = Directory.CreateTempSubdirectory();
        var configPath = Path.Combine(tempRoot.FullName, "Echoglossian.json");
        var databasePath = Path.Combine(tempRoot.FullName, "missing.db");
        File.WriteAllText(configPath, "{\"Lang\":28}");

        using var session = PreviewSessionLoader.Load(
            new PreviewSessionSourceOptions(configPath, databasePath, null));

        Assert.Null(session.ClonedDatabasePath);
        Assert.Contains(
            "database",
            string.Join(" ", session.Diagnostics),
            StringComparison.OrdinalIgnoreCase);
    }
}
