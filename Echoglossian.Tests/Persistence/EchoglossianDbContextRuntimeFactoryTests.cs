// <copyright file="EchoglossianDbContextRuntimeFactoryTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;

using Echoglossian.EFCoreSqlite;
using Echoglossian.Persistence;

using FluentAssertions;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Echoglossian.Tests.Persistence;

/// <summary>Verifies the public database-context API alongside the pooled runtime factory.</summary>
public sealed class EchoglossianDbContextRuntimeFactoryTests
{
    /// <summary>Ensures consumers can still construct a context from the plugin config directory.</summary>
    [Fact]
    public void EchoglossianDbContext_ConfigDirectoryConstructor_RemainsPublic()
    {
        var constructor = typeof(EchoglossianDbContext).GetConstructor(new[] { typeof(string) });

        constructor.Should().NotBeNull();
        constructor!.IsPublic.Should().BeTrue();
    }

    /// <summary>Ensures the runtime factory pools a derived context while returning the public base type.</summary>
    [Fact]
    public async Task RuntimeFactory_MigratedSqliteDatabase_ReturnsDistinctAssignablePooledContexts()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var factory = new EchoglossianDbContextRuntimeFactory(directory);

        try
        {
            await using (var initialContext = await factory.CreateDbContextAsync())
            {
                initialContext.Should().BeAssignableTo<EchoglossianDbContext>();
                await initialContext.Database.MigrateAsync();
                (await initialContext.Database.CanConnectAsync()).Should().BeTrue();
                (await initialContext.LlmModelCapabilityObservations.CountAsync()).Should().Be(0);
            }

            await using var firstContext = factory.CreateDbContext();
            await using var secondContext = await factory.CreateDbContextAsync();

            firstContext.Should().BeAssignableTo<EchoglossianDbContext>();
            secondContext.Should().BeAssignableTo<EchoglossianDbContext>();
            firstContext.Should().NotBeSameAs(secondContext);
            firstContext.GetType().GetConstructors(BindingFlags.Instance | BindingFlags.Public)
                .Should().ContainSingle(constructor => constructor.GetParameters()
                    .Select(parameter => parameter.ParameterType)
                    .SequenceEqual(new[] { typeof(DbContextOptions<>).MakeGenericType(firstContext.GetType()) }));
            (await firstContext.Database.CanConnectAsync()).Should().BeTrue();
            (await secondContext.Database.CanConnectAsync()).Should().BeTrue();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, true);
        }
    }
}
