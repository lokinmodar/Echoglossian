// <copyright file="EchoglossianDBContext.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;
using System.Threading.Tasks;

using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.EFCoreSqlite.Models.Journal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Echoglossian.EFCoreSqlite
{
  public class EchoglossianDbContext : DbContext
  {
    public DbSet<TalkSubtitleMessage> TalkSubtitleMessage { get; set; }

    public DbSet<ToastMessage> ToastMessage { get; set; }

    public DbSet<TalkMessage> TalkMessage { get; set; }

    public DbSet<BattleTalkMessage> BattleTalkMessage { get; set; }

    public DbSet<QuestPlate> QuestPlate { get; set; }

    public DbSet<NpcNames> NpcName { get; set; }

    public DbSet<LocationName> LocationNames { get; set; }

    public string DbPath { get; }

#if DEBUG
    private StreamWriter LogStream { get; set; }

#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="EchoglossianDbContext"/> class.
    /// </summary>
    /// <param name="configDir"></param>
    public EchoglossianDbContext(string configDir)
    {
      this.DbPath = $"{configDir}Echoglossian.db";
#if DEBUG
      this.LogStream = new StreamWriter($"{configDir}DBContextLog.txt", append: true);
#endif
    }

    // The following configures EF to create a Sqlite database file in the
    // special "local" folder for your platform.
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
      optionsBuilder.UseSqlite($"Data Source={this.DbPath}");
#if DEBUG
      optionsBuilder.LogTo(this.LogStream.WriteLine, LogLevel.Trace).EnableSensitiveDataLogging().EnableDetailedErrors();
#endif
    }

    public override void Dispose()
    {
      base.Dispose();
#if DEBUG
      this.LogStream.Dispose();
#endif
    }

    public override async ValueTask DisposeAsync()
    {
      await base.DisposeAsync();
#if DEBUG
      await this.LogStream.DisposeAsync();
#endif
    }
  }
}