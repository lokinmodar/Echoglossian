// <copyright file="PersistenceCoordinatorTestContextFactory.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Concurrent;

using Echoglossian.EFCoreSqlite;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

using Xunit;

namespace Echoglossian.Tests.Persistence;

/// <summary>
///     Creates distinct short-lived contexts and records their concurrent leases.
/// </summary>
public sealed class PersistenceCoordinatorTestContextFactory
    : IDbContextFactory<EchoglossianDbContext>
{
  private readonly ConcurrentQueue<int> contextIds = new();
  private readonly DbContextOptions<EchoglossianDbContext> options =
      new DbContextOptionsBuilder<EchoglossianDbContext>().Options;
  private int activeLeases;
  private int nextContextId;
  private int maximumConcurrentLeases;

  /// <summary>Gets the identifiers of every context leased by this factory.</summary>
  public IReadOnlyCollection<int> ContextIds => this.contextIds.ToArray();

  /// <summary>Gets the maximum simultaneously leased context count.</summary>
  public int MaximumConcurrentLeases => Volatile.Read(ref this.maximumConcurrentLeases);

  /// <inheritdoc />
  public EchoglossianDbContext CreateDbContext()
  {
    return this.CreateContext();
  }

  /// <inheritdoc />
  public Task<EchoglossianDbContext> CreateDbContextAsync(
      CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    return Task.FromResult<EchoglossianDbContext>(this.CreateContext());
  }

  /// <summary>Gets the stable test identifier for one leased context.</summary>
  /// <param name="context">The leased context.</param>
  /// <returns>The context identifier.</returns>
  public int GetContextId(EchoglossianDbContext context)
  {
    return AssertContext(context).TestContextId;
  }

  private static TestDbContext AssertContext(EchoglossianDbContext context)
  {
    return Assert.IsType<TestDbContext>(context);
  }

  private EchoglossianDbContext CreateContext()
  {
    var contextId = Interlocked.Increment(ref this.nextContextId);
    this.contextIds.Enqueue(contextId);
    var activeLeases = Interlocked.Increment(ref this.activeLeases);
    UpdateMaximum(ref this.maximumConcurrentLeases, activeLeases);
    return new TestDbContext(this.options, contextId, this.ReleaseLease);
  }

  private static void UpdateMaximum(ref int maximum, int candidate)
  {
    while (candidate > Volatile.Read(ref maximum))
    {
      var observed = Volatile.Read(ref maximum);
      if (Interlocked.CompareExchange(ref maximum, candidate, observed) == observed)
      {
        return;
      }
    }
  }

  private void ReleaseLease()
  {
    _ = Interlocked.Decrement(ref this.activeLeases);
  }

  private sealed class TestDbContext : EchoglossianDbContext
  {
    private readonly Action releaseLease;
    private int disposed;

    internal TestDbContext(
        DbContextOptions<EchoglossianDbContext> options,
        int contextId,
        Action releaseLease)
        : base(options)
    {
      this.TestContextId = contextId;
      this.releaseLease = releaseLease;
    }

    internal int TestContextId { get; }

    public override async ValueTask DisposeAsync()
    {
      await base.DisposeAsync();
      if (Interlocked.Exchange(ref this.disposed, 1) == 0)
      {
        this.releaseLease();
      }
    }
  }
}
