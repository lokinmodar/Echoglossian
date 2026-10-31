// <copyright file="GameWindowPersistenceBatchTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>Checks version fallback across operations sharing one batch context.</summary>
public sealed class GameWindowPersistenceBatchTests
{
    /// <summary>Pending inserts obey the same directional version fallback as committed rows.</summary>
    /// <param name="firstVersion">The version inserted first.</param>
    /// <param name="secondVersion">The version requested before committing.</param>
    /// <param name="expectedRows">The number of distinct persisted identities.</param>
    [Theory]
    [InlineData(null, "7.3", 1)]
    [InlineData(null, "", 1)]
    [InlineData("", null, 1)]
    [InlineData("", "7.3", 1)]
    [InlineData("7.3", null, 2)]
    public async Task Upsert_ReusesPendingVersionAgnosticInsert(
        string? firstVersion, string? secondVersion, int expectedRows)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var first = await GameWindowPersistenceHelper.UpsertAsync(
            context, CreateRow(firstVersion, "first"), CancellationToken.None);
        var secondRequest = CreateRow(secondVersion, "latest");
        secondRequest.OriginalWindowStringsLang = "English";
        var second = await GameWindowPersistenceHelper.UpsertAsync(context, secondRequest, CancellationToken.None);

        if (expectedRows == 1)
        {
            Assert.Same(first.Row, second.Row);
            Assert.Equal(firstVersion, second.Row.GameVersion);
        }
        else
        {
            Assert.NotSame(first.Row, second.Row);
        }

        Assert.Equal("latest", second.Row.TranslatedWindowStrings);
        await context.SaveChangesAsync();
        Assert.Equal(expectedRows, await context.GameWindow.CountAsync());
        Assert.Equal("latest", (await context.GameWindow.SingleAsync(row => row.Id == second.Row.Id)).TranslatedWindowStrings);
    }

    /// <summary>Previously persisted candidates retain precedence over a pending fallback row.</summary>
    [Fact]
    public async Task Upsert_PrefersCommittedCandidateBeforePendingFallback()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var stored = CreateRow("7.3", "stored");
        context.GameWindow.Add(stored);
        await context.SaveChangesAsync();

        var pending = await GameWindowPersistenceHelper.UpsertAsync(
            context, CreateRow(null, "pending"), CancellationToken.None);
        var resolved = await GameWindowPersistenceHelper.UpsertAsync(
            context, CreateRow("7.3", "latest"), CancellationToken.None);

        Assert.Same(stored, resolved.Row);
        Assert.NotSame(pending.Row, resolved.Row);
        Assert.Equal("pending", pending.Row.TranslatedWindowStrings);
        await context.SaveChangesAsync();
        Assert.Equal(2, await context.GameWindow.CountAsync());
        Assert.Equal("latest", (await context.GameWindow.SingleAsync(row => row.Id == stored.Id)).TranslatedWindowStrings);
    }

    /// <summary>Creates a SQLite context sharing the test's open connection.</summary>
    /// <param name="connection">The connection owned by the test.</param>
    /// <returns>The batch context.</returns>
    private static EchoglossianDbContext CreateContext(SqliteConnection connection)
    {
        return new EchoglossianDbContext(new DbContextOptionsBuilder<EchoglossianDbContext>()
            .UseSqlite(connection).Options);
    }

    /// <summary>Creates one canonical row for testing version reuse.</summary>
    /// <param name="version">The stored version.</param>
    /// <param name="translation">The translated payload.</param>
    /// <returns>The requested row.</returns>
    private static GameWindow CreateRow(string? version, string translation)
    {
        return new GameWindow("ActionMenu", "original", "en", translation, "pt", 0, version, null, null);
    }
}
