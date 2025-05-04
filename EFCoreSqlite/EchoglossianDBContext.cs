// <copyright file="EchoglossianDBContext.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.EFCoreSqlite.Models.Journal;
using Microsoft.EntityFrameworkCore;

namespace Echoglossian.EFCoreSqlite
{
  public class EchoglossianDbContext : DbContext
  {
    public DbSet<ActionTooltip> ActionTooltip { get; set; }

    public DbSet<ItemTooltip> ItemTooltip { get; set; }

    public DbSet<SelectString> SelectString { get; set; }

    public DbSet<GameWindow> GameWindow { get; set; }

    public DbSet<TalkSubtitleMessage> TalkSubtitleMessage { get; set; }

    public DbSet<ToastMessage> ToastMessage { get; set; }

    public DbSet<TalkMessage> TalkMessage { get; set; }

    public DbSet<BattleTalkMessage> BattleTalkMessage { get; set; }

    public DbSet<QuestPlate> QuestPlate { get; set; }

    public DbSet<NpcNames> NpcName { get; set; }

    public DbSet<LocationName> LocationNames { get; set; }

    public string DbPath { get; }

#if DEBUG
    private StreamWriter? LogStream { get; set; }

#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="EchoglossianDbContext"/> class.
    /// </summary>
    /// <param name="configDir">The directory where the configuration files are located.</param>
    public EchoglossianDbContext(string configDir)
    {
      this.DbPath = $"{configDir}Echoglossian.db";

      Echoglossian.PluginLog.Debug($"DBPath: {this.DbPath}");

#if DEBUG
      // this.LogStream = new StreamWriter($"{configDir}DBContextLog.txt", append: true);
      // Echoglossian.PluginLog.Debug($"DBPath {this.DbPath}");
#endif
    }

    // The following configures EF to create a Sqlite database file in the
    // special "local" folder for your platform.
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
      optionsBuilder.UseSqlite($"Data Source={this.DbPath}");
      /*#if DEBUG
            optionsBuilder.LogTo(this.LogStream.WriteLine, LogLevel.Trace).EnableSensitiveDataLogging().EnableDetailedErrors();
      #endif*/
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      modelBuilder.Entity<ActionTooltip>().ToTable("actiontooltips");
      modelBuilder.Entity<ItemTooltip>().ToTable("itemtooltips");
      modelBuilder.Entity<SelectString>().ToTable("selectstrings");
      modelBuilder.Entity<GameWindow>().ToTable("gamewindows");
      modelBuilder.Entity<TalkSubtitleMessage>().ToTable("talksubtitlemessages");
      modelBuilder.Entity<ToastMessage>().ToTable("toastmessages");
      modelBuilder.Entity<TalkMessage>().ToTable("talkmessages");
      modelBuilder.Entity<BattleTalkMessage>().ToTable("battletalkmessages");
      modelBuilder.Entity<QuestPlate>().ToTable("questplates");
      modelBuilder.Entity<NpcNames>().ToTable("npcnames");
      modelBuilder.Entity<LocationName>().ToTable("locationnames");
    }

    public override void Dispose()
    {
      base.Dispose();
      /*#if DEBUG
            this.LogStream.Dispose();
      #endif*/
    }

    public override async ValueTask DisposeAsync()
    {
      await base.DisposeAsync();
      /*#if DEBUG
            await this.LogStream.DisposeAsync();
      #endif*/
    }
  }
}