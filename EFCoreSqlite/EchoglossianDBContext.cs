// <copyright file="EchoglossianDBContext.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite;

/// <summary>
///     Gets or sets the translated string array records.   Represents the database context for Echoglossian translations.
/// </summary>
public class EchoglossianDbContext : DbContext
{
  private readonly string? dbPath;

  /// <summary>
  ///     Gets or sets the translated string array records.   Initializes a new instance of the <see cref="EchoglossianDbContext" />
  ///     Gets or sets the translated string array records.   class.
  /// </summary>
  /// <param name="options">Configuration options.</param>
  public EchoglossianDbContext(
      DbContextOptions<EchoglossianDbContext> options)
    : base(options)
  {
  }

  /// <summary>
  ///     Gets or sets the translated string array records.   Initializes a new instance of the <see cref="EchoglossianDbContext" />
  ///     Gets or sets the translated string array records.   class.
  /// </summary>
  /// <param name="configDir">Plugin config directory.</param>
  public EchoglossianDbContext(string configDir)
  {
    this.dbPath = Path.Combine(configDir, "Echoglossian.db");
  }

  public DbSet<ActionTooltip> ActionTooltip { get; set; }

  public DbSet<Trait> Traits { get; set; }

  public DbSet<ItemTooltip> ItemTooltip { get; set; }

  public DbSet<SelectString> SelectString { get; set; }

  public DbSet<GameWindow> GameWindow { get; set; }

  public DbSet<TalkSubtitleMessage> TalkSubtitleMessage { get; set; }

  public DbSet<MiniTalkMessage> MiniTalkMessage { get; set; }

  public DbSet<TextGimmickHintMessage> TextGimmickHintMessage { get; set; }

  public DbSet<ToastMessage> ToastMessage { get; set; }

  public DbSet<TalkMessage> TalkMessage { get; set; }

  public DbSet<BattleTalkMessage> BattleTalkMessage { get; set; }

  public DbSet<QuestPlate> QuestPlate { get; set; }

  public DbSet<NpcNames> NpcName { get; set; }

  public DbSet<LocationName> LocationNames { get; set; }
  /// <summary>
  ///     Gets or sets the translated string array records.
  /// </summary>
  public DbSet<StringArrayDatas> StringArrayDatas { get; set; }

  /// <summary>
  ///     Gets or sets the canonical GeneralAction text rows.
  /// </summary>
  public DbSet<GeneralActionText> GeneralActionTexts { get; set; }

  /// <summary>
  ///     Gets or sets the canonical BuddyAction text rows.
  /// </summary>
  public DbSet<BuddyActionText> BuddyActionTexts { get; set; }

  /// <summary>
  ///     Gets or sets the canonical CompanyAction text rows.
  /// </summary>
  public DbSet<CompanyActionText> CompanyActionTexts { get; set; }

  /// <summary>
  ///     Gets or sets the canonical CraftAction text rows.
  /// </summary>
  public DbSet<CraftActionText> CraftActionTexts { get; set; }

  /// <summary>
  ///     Gets or sets the canonical PetAction text rows.
  /// </summary>
  public DbSet<PetActionText> PetActionTexts { get; set; }

  /// <summary>
  ///     Gets or sets the canonical EventAction text rows.
  /// </summary>
  public DbSet<EventActionText> EventActionTexts { get; set; }

  /// <summary>
  ///     Gets or sets the canonical EventItem text rows.
  /// </summary>
  public DbSet<EventItemText> EventItemTexts { get; set; }

  /// <summary>
  ///     Gets or sets the canonical BgcArmyAction text rows.
  /// </summary>
  public DbSet<BgcArmyActionText> BgcArmyActionTexts { get; set; }

  /// <summary>
  ///     Gets or sets the canonical AozAction text rows.
  /// </summary>
  public DbSet<AozActionText> AozActionTexts { get; set; }

  /// <summary>
  ///     Gets or sets the canonical PvPAction text rows.
  /// </summary>
  public DbSet<PvPActionText> PvPActionTexts { get; set; }

  /// <summary>
  ///     Gets or sets the canonical MountAction text rows.
  /// </summary>
  public DbSet<MountActionText> MountActionTexts { get; set; }

  /// <summary>
  ///     Gets or sets the canonical MainCommand sheet text rows.
  /// </summary>
  public DbSet<MainCommandText> MainCommandTexts { get; set; }

  /// <summary>
  ///     Gets or sets the canonical EurekaMagiaAction text rows.
  /// </summary>
  public DbSet<EurekaMagiaActionText> EurekaMagiaActionTexts { get; set; }

  /// <summary>
  ///     Gets or sets the canonical DeepDungeonItem text rows.
  /// </summary>
  public DbSet<DeepDungeonItemText> DeepDungeonItemTexts { get; set; }

  /// <summary>
  ///     Gets or sets exact translation failures that should not be retried for
  ///     the same source/target language pair and engine.
  /// </summary>
  public DbSet<TranslationFailure> TranslationFailures { get; set; }

  /// <summary>
  ///     Gets or sets the translated string array records.   Configures the database context options.
  /// </summary>
  /// <param name="optionsBuilder"></param>
  protected override void OnConfiguring(
        DbContextOptionsBuilder optionsBuilder)
  {
    if (!optionsBuilder.IsConfigured && this.dbPath != null)
    {
      optionsBuilder.UseSqlite($"Data Source={this.dbPath}");
    }
  }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<ActionTooltip>().ToTable("actiontooltips");
    modelBuilder.Entity<ActionTooltip>()
        .HasIndex(t => new
        {
          t.ActionId,
          t.TranslationLang,
          t.TranslationEngine,
          t.GameVersion,
          t.SourceContentHash
        })
        .HasDatabaseName("IX_actiontooltips_lookup");
    modelBuilder.Entity<Trait>().ToTable("Traits");
    modelBuilder.Entity<Trait>()
        .HasIndex(t => new
        {
          t.TraitId,
          t.TranslationLang,
          t.TranslationEngine,
          t.GameVersion,
          t.SourceContentHash
        })
        .HasDatabaseName("IX_traits_lookup");
    modelBuilder.Entity<ItemTooltip>().ToTable("itemtooltips");
    modelBuilder.Entity<ItemTooltip>()
        .HasIndex(t => new
        {
          t.ItemId,
          t.TranslationLang,
          t.TranslationEngine,
          t.GameVersion,
          t.SourceContentHash
        })
        .HasDatabaseName("IX_itemtooltips_lookup");
    modelBuilder.Entity<SelectString>().ToTable("selectstrings");
    modelBuilder.Entity<SelectString>()
        .HasIndex(s => new
        {
          s.OriginalSelectString,
          s.OriginalOptionsAsText,
          s.TranslationLang,
          s.TranslationEngine
        })
        .HasDatabaseName("IX_selectstrings_lookup");
    modelBuilder.Entity<GameWindow>().ToTable("gamewindows");
    modelBuilder.Entity<GameWindow>()
        .HasIndex(g => new
        {
          g.WindowAddonName,
          g.TranslationLang,
          g.TranslationEngine,
          g.GameVersion
        })
        .HasDatabaseName("IX_gamewindows_lookup");
    modelBuilder.Entity<GameWindow>()
        .HasIndex(g => new
        {
          g.WindowAddonName,
          g.ClassJobId,
          g.TranslationLang,
          g.TranslationEngine,
          g.GameVersion
        })
        .HasDatabaseName("IX_gamewindows_classjob_lookup");
    modelBuilder.Entity<TalkSubtitleMessage>()
        .ToTable("talksubtitlemessages");
    modelBuilder.Entity<TalkSubtitleMessage>()
        .HasIndex(t => new
        {
          t.TranslationLang,
          t.TranslationEngine
        })
        .HasDatabaseName("IX_talksubtitlemessages_lookup");
    modelBuilder.Entity<MiniTalkMessage>()
        .ToTable("minitalkmessages");
    modelBuilder.Entity<MiniTalkMessage>()
        .HasIndex(t => new
        {
          t.TranslationLang,
          t.TranslationEngine
        })
        .HasDatabaseName("IX_minitalkmessages_lookup");
    modelBuilder.Entity<TextGimmickHintMessage>()
        .ToTable("textgimmickhintmessages");
    modelBuilder.Entity<TextGimmickHintMessage>()
        .HasIndex(t => new
        {
          t.TranslationLang,
          t.TranslationEngine
        })
        .HasDatabaseName("IX_textgimmickhintmessages_lookup");
    modelBuilder.Entity<ToastMessage>().ToTable("toastmessages");
    modelBuilder.Entity<ToastMessage>()
        .HasIndex(t => new
        {
          t.ToastType,
          t.TranslationLang,
          t.TranslationEngine
        })
        .HasDatabaseName("IX_toastmessages_lookup");
    modelBuilder.Entity<TalkMessage>().ToTable("talkmessages");
    modelBuilder.Entity<TalkMessage>()
        .HasIndex(t => new
        {
          t.SenderName,
          t.TranslationLang,
          t.TranslationEngine
        })
        .HasDatabaseName("IX_talkmessages_lookup");
    modelBuilder.Entity<BattleTalkMessage>().ToTable("battletalkmessages");
    modelBuilder.Entity<BattleTalkMessage>()
        .HasIndex(t => new
        {
          t.SenderName,
          t.TranslationLang,
          t.TranslationEngine
        })
        .HasDatabaseName("IX_battletalkmessages_lookup");
    modelBuilder.Entity<QuestPlate>().ToTable("questplates");
    modelBuilder.Entity<QuestPlate>()
        .HasIndex(q => new
        {
          q.QuestName,
          q.TranslationLang,
          q.TranslationEngine,
          q.GameVersion
        })
        .HasDatabaseName("IX_questplates_lookup");
    modelBuilder.Entity<QuestPlate>()
        .HasIndex(q => new
        {
          q.QuestId,
          q.TranslationLang,
          q.TranslationEngine,
          q.GameVersion
        })
        .HasDatabaseName("IX_questplates_questid_lookup");
    modelBuilder.Entity<NpcNames>().ToTable("npcnames");
    modelBuilder.Entity<LocationName>().ToTable("locationnames");
    modelBuilder.Entity<TranslationFailure>().ToTable("translationfailures");
    modelBuilder.Entity<TranslationFailure>()
        .HasIndex(t => new
        {
          t.SourceTextHash,
          t.SourceLanguage,
          t.TargetLanguage,
          t.TranslationEngine,
          t.SourceText
        })
        .IsUnique()
        .HasDatabaseName("IX_translationfailures_lookup");
    modelBuilder.Entity<StringArrayDatas>()
        .HasIndex(s => new
        {
          s.Type,
          s.TranslationLang,
          s.TranslationEngine
        })
        .HasDatabaseName("IX_stringarraydatas_lookup");
    modelBuilder.Entity<StringArrayDatas>()
        .HasIndex(s => new
        {
          s.Type,
          s.ContextKey,
          s.TranslationLang,
          s.TranslationEngine,
          s.GameVersion,
          s.SourceContentHash
        })
        .HasDatabaseName("IX_stringarraydatas_context_lookup");
    ConfigureReferenceTextEntity<GeneralActionText>(
        modelBuilder,
        "generalactiontexts",
        "IX_generalactiontexts_lookup");
    ConfigureReferenceTextEntity<BuddyActionText>(
        modelBuilder,
        "buddyactiontexts",
        "IX_buddyactiontexts_lookup");
    ConfigureReferenceTextEntity<CompanyActionText>(
        modelBuilder,
        "companyactiontexts",
        "IX_companyactiontexts_lookup");
    ConfigureReferenceTextEntity<CraftActionText>(
        modelBuilder,
        "craftactiontexts",
        "IX_craftactiontexts_lookup");
    ConfigureReferenceTextEntity<PetActionText>(
        modelBuilder,
        "petactiontexts",
        "IX_petactiontexts_lookup");
    ConfigureReferenceTextEntity<EventActionText>(
        modelBuilder,
        "eventactiontexts",
        "IX_eventactiontexts_lookup");
    ConfigureReferenceTextEntity<EventItemText>(
        modelBuilder,
        "eventitemtexts",
        "IX_eventitemtexts_lookup");
    ConfigureReferenceTextEntity<BgcArmyActionText>(
        modelBuilder,
        "bgcarmyactiontexts",
        "IX_bgcarmyactiontexts_lookup");
    ConfigureReferenceTextEntity<AozActionText>(
        modelBuilder,
        "aozactiontexts",
        "IX_aozactiontexts_lookup");
    ConfigureReferenceTextEntity<PvPActionText>(
        modelBuilder,
        "pvpactiontexts",
        "IX_pvpactiontexts_lookup");
    ConfigureReferenceTextEntity<MountActionText>(
        modelBuilder,
        "mountactiontexts",
        "IX_mountactiontexts_lookup");
    ConfigureReferenceTextEntity<MainCommandText>(
        modelBuilder,
        "maincommandtexts",
        "IX_maincommandtexts_lookup");
    ConfigureReferenceTextEntity<EurekaMagiaActionText>(
        modelBuilder,
        "eurekamagiaactiontexts",
        "IX_eurekamagiaactiontexts_lookup");
    ConfigureReferenceTextEntity<DeepDungeonItemText>(
        modelBuilder,
        "deepdungeonitemtexts",
        "IX_deepdungeonitemtexts_lookup");
  }

  /// <summary>
  ///     Configures one reference-text entity table and lookup index.
  /// </summary>
  /// <typeparam name="TRow">The concrete row type.</typeparam>
  /// <param name="modelBuilder">The active model builder.</param>
  /// <param name="tableName">The physical table name.</param>
  /// <param name="indexName">The lookup index name.</param>
  private static void ConfigureReferenceTextEntity<TRow>(
      ModelBuilder modelBuilder,
      string tableName,
      string indexName)
      where TRow : ReferenceTextRowBase
  {
    modelBuilder.Entity<TRow>().ToTable(tableName);
    modelBuilder.Entity<TRow>()
        .HasIndex(t => new
        {
          t.ReferenceId,
          t.TranslationLang,
          t.TranslationEngine,
          t.GameVersion,
          t.SourceContentHash
        })
        .HasDatabaseName(indexName);
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
