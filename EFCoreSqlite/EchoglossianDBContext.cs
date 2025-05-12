// <copyright file="EchoglossianDBContext.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

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

    private readonly string? dbPath;

    public EchoglossianDbContext(DbContextOptions<EchoglossianDbContext> options)
        : base(options) { }

    public EchoglossianDbContext(string configDir)
    {
      this.dbPath = Path.Combine(configDir, "Echoglossian.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
      if (!optionsBuilder.IsConfigured && this.dbPath != null)
      {
        optionsBuilder.UseSqlite($"Data Source={this.dbPath}");
      }
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
    }

    public override async ValueTask DisposeAsync()
    {
      await base.DisposeAsync();
    }
  }
}