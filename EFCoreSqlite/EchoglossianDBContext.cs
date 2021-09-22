// <copyright file="EchoglossianDBContext.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;
using System.Reflection;
using System.Threading.Tasks;

using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.EFCoreSqlite.Models.Journal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Echoglossian.EFCoreSqlite
{
  public class EchoglossianDbContext : DbContext
  {
    public DbSet<ToastMessage> ToastMessage { get; set; }

    public DbSet<TalkMessage> TalkMessage { get; set; }

    public DbSet<BattleTalkMessage> BattleTalkMessage { get; set; }

    public DbSet<QuestPlate> QuestPlate { get; set; }

    public string DbPath { get; }

    private StreamWriter LogStream { get; set; }

    public EchoglossianDbContext()
    {
      var dbPath = Directory.GetParent(Assembly.GetExecutingAssembly().Location)?.ToString();
      this.DbPath = $"{dbPath}{Path.DirectorySeparatorChar}Echoglossian.db";
      this.LogStream = new StreamWriter($"{dbPath}{Path.DirectorySeparatorChar}dantecontextlog.txt", append: true);
    }

    // The following configures EF to create a Sqlite database file in the
    // special "local" folder for your platform.
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
      optionsBuilder.UseSqlite($"Data Source={this.DbPath}");
      optionsBuilder.LogTo(this.LogStream.WriteLine).EnableSensitiveDataLogging().EnableDetailedErrors();
    }

    public override void Dispose()
    {
      base.Dispose();
      this.LogStream.Dispose();
    }

    public override async ValueTask DisposeAsync()
    {
      await base.DisposeAsync();
      await this.LogStream.DisposeAsync();
    }

    /*public override int SaveChanges()
    {
      var entries = this.ChangeTracker
        .Entries()
        .Where(e => e.Entity is BaseEntity && (
          e.State == EntityState.Added
          || e.State == EntityState.Modified));
      var enumerable = entries as EntityEntry[] ?? entries.ToArray();
      var entityEntries = entries as EntityEntry[] ?? enumerable.ToArray();
      File.AppendAllLines($"{Directory.GetParent(Assembly.GetExecutingAssembly().Location)}{Path.DirectorySeparatorChar}dantelog.txt", new[] { "entries antes do foreach", entityEntries.ToString() });
      foreach (var entityEntry in entityEntries)
      {
        ((BaseEntity)entityEntry.Entity).UpdatedDate = DateTime.Now;

        if (entityEntry.State == EntityState.Added)
        {
          ((BaseEntity)entityEntry.Entity).CreatedDate = DateTime.Now;
        }
      }

      var entityEntries2 = entries as EntityEntry[] ?? enumerable.ToArray();
      File.AppendAllLines($"{Directory.GetParent(Assembly.GetExecutingAssembly().Location)}{Path.DirectorySeparatorChar}dantelog.txt", new[] { "entries depois do foreach", entityEntries2.ToString() });
      return base.SaveChanges();
    }*/
  }
}