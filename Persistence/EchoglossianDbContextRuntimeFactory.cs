// <copyright file="EchoglossianDbContextRuntimeFactory.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Echoglossian.Persistence;

/// <summary>Creates pooled, short-lived runtime database contexts.</summary>
internal sealed class EchoglossianDbContextRuntimeFactory : IDbContextFactory<EchoglossianDbContext>
{
    private readonly PooledDbContextFactory<EchoglossianDbContext> factory;

    /// <summary>Initializes a new instance of the factory.</summary>
    /// <param name="configDirectory">The plugin configuration directory.</param>
    internal EchoglossianDbContextRuntimeFactory(string configDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configDirectory);
        var connection = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(configDirectory, "Echoglossian.db"),
            DefaultTimeout = PersistenceCoordinatorOptions.Default.SqliteDefaultTimeoutSeconds,
        }.ToString();
        var options = new DbContextOptionsBuilder<EchoglossianDbContext>()
            .UseSqlite(connection)
            .Options;
        this.factory = new PooledDbContextFactory<EchoglossianDbContext>(
            options,
            PersistenceCoordinatorOptions.Default.ContextPoolSize);
    }

    /// <inheritdoc />
    public EchoglossianDbContext CreateDbContext() => this.factory.CreateDbContext();

    /// <inheritdoc />
    public Task<EchoglossianDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        this.factory.CreateDbContextAsync(cancellationToken);
}
