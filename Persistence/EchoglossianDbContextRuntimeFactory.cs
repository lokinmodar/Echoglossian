// <copyright file="EchoglossianDbContextRuntimeFactory.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using System.Reflection;

namespace Echoglossian.Persistence;

/// <summary>Creates pooled, short-lived runtime database contexts.</summary>
internal sealed class EchoglossianDbContextRuntimeFactory : IDbContextFactory<EchoglossianDbContext>
{
    private readonly PooledDbContextFactory<PooledRuntimeDbContext> factory;

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
        var options = new DbContextOptionsBuilder<PooledRuntimeDbContext>()
            .UseSqlite(connection)
            .ReplaceService<IMigrationsAssembly, PooledRuntimeMigrationsAssembly>()
            .Options;
        this.factory = new PooledDbContextFactory<PooledRuntimeDbContext>(
            options,
            PersistenceCoordinatorOptions.Default.ContextPoolSize);
    }

    /// <inheritdoc />
    public EchoglossianDbContext CreateDbContext() => this.factory.CreateDbContext();

    /// <inheritdoc />
    public async Task<EchoglossianDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        await this.factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>Provides the single public options constructor required by the runtime context pool.</summary>
    private sealed class PooledRuntimeDbContext : EchoglossianDbContext
    {
        /// <summary>Initializes a new instance of the pooled runtime context.</summary>
        /// <param name="options">The pooled context options.</param>
        public PooledRuntimeDbContext(DbContextOptions<PooledRuntimeDbContext> options)
            : base(options)
        {
        }
    }

    /// <summary>Maps the pooled derived context back to the existing base-context migrations.</summary>
    private sealed class PooledRuntimeMigrationsAssembly : IMigrationsAssembly
    {
        private readonly IReadOnlyDictionary<string, TypeInfo> migrations;
        private readonly ModelSnapshot? modelSnapshot;
        private readonly IMigrationsIdGenerator idGenerator;

        /// <summary>Initializes a new instance of the migrations adapter.</summary>
        /// <param name="idGenerator">The migration identifier generator.</param>
        public PooledRuntimeMigrationsAssembly(IMigrationsIdGenerator idGenerator)
        {
            this.idGenerator = idGenerator;
            this.Assembly = typeof(EchoglossianDbContext).Assembly;
            this.migrations = this.Assembly.DefinedTypes
                .Where(static type => type.IsSubclassOf(typeof(Migration)))
                .Where(static type => type.GetCustomAttribute<DbContextAttribute>()?.ContextType == typeof(EchoglossianDbContext))
                .Select(static type => new
                {
                    Id = type.GetCustomAttribute<MigrationAttribute>()?.Id,
                    Type = type,
                })
                .Where(static migration => migration.Id is not null)
                .OrderBy(static migration => migration.Id, StringComparer.Ordinal)
                .ToDictionary(static migration => migration.Id!, static migration => migration.Type);
            this.modelSnapshot = this.Assembly.DefinedTypes
                .Where(static type => type.IsSubclassOf(typeof(ModelSnapshot)))
                .Where(static type => type.GetCustomAttribute<DbContextAttribute>()?.ContextType == typeof(EchoglossianDbContext))
                .Select(static type => (ModelSnapshot)Activator.CreateInstance(type.AsType())!)
                .SingleOrDefault();
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<string, TypeInfo> Migrations => this.migrations;

        /// <inheritdoc />
        public ModelSnapshot? ModelSnapshot => this.modelSnapshot;

        /// <summary>Gets the migrations assembly containing the base context migrations.</summary>
        public Assembly Assembly { get; }

        /// <summary>Finds a migration identifier by its identifier or name.</summary>
        /// <param name="nameOrId">The migration name or identifier.</param>
        /// <returns>The matching migration identifier, if found.</returns>
        public string? FindMigrationId(string nameOrId) => this.migrations.Keys.FirstOrDefault(
            this.idGenerator.IsValidId(nameOrId)
                ? id => string.Equals(id, nameOrId, StringComparison.OrdinalIgnoreCase)
                : id => string.Equals(this.idGenerator.GetName(id), nameOrId, StringComparison.OrdinalIgnoreCase));

        /// <summary>Creates a migration for the active provider.</summary>
        /// <param name="migrationClass">The migration type information.</param>
        /// <param name="activeProvider">The active provider name.</param>
        /// <returns>The initialized migration.</returns>
        public Migration CreateMigration(TypeInfo migrationClass, string activeProvider)
        {
            var migration = (Migration)Activator.CreateInstance(migrationClass.AsType())!;
            migration.ActiveProvider = activeProvider;
            return migration;
        }
    }
}
