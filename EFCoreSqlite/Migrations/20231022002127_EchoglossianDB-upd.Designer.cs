﻿// <auto-generated />
using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Echoglossian.EFCoreSqlite.Migrations
{
  [DbContext(typeof(EchoglossianDbContext))]
  [Migration("20231022002127_EchoglossianDB-upd")]
  partial class EchoglossianDBupd
  {
    /// <inheritdoc />
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
      modelBuilder.HasAnnotation("ProductVersion", "7.0.12");

      modelBuilder.Entity("EFCoreSqlite.Models.BattleTalkMessage", b =>
          {
            b.Property<int>("Id")
                      .ValueGeneratedOnAdd()
                      .HasColumnType("INTEGER");

            b.Property<DateTime>("CreatedDate")
                      .HasColumnType("TEXT");

            b.Property<string>("OriginalBattleTalkMessage")
                      .IsRequired()
                      .HasMaxLength(400)
                      .HasColumnType("TEXT");

            b.Property<string>("OriginalBattleTalkMessageLang")
                      .IsRequired()
                      .HasColumnType("TEXT");

            b.Property<string>("OriginalSenderNameLang")
                      .IsRequired()
                      .HasColumnType("TEXT");

            b.Property<byte[]>("RowVersion")
                      .IsConcurrencyToken()
                      .ValueGeneratedOnAddOrUpdate()
                      .HasColumnType("BLOB");

            b.Property<string>("SenderName")
                      .IsRequired()
                      .HasMaxLength(100)
                      .HasColumnType("TEXT");

            b.Property<string>("TranslatedBattleTalkMessage")
                      .HasMaxLength(400)
                      .HasColumnType("TEXT");

            b.Property<string>("TranslatedSenderName")
                      .HasMaxLength(100)
                      .HasColumnType("TEXT");

            b.Property<int>("TranslationEngine")
                      .HasColumnType("INTEGER");

            b.Property<string>("TranslationLang")
                      .IsRequired()
                      .HasColumnType("TEXT");

            b.Property<DateTime?>("UpdatedDate")
                      .HasColumnType("TEXT");

            b.HasKey("Id");

            b.ToTable("battletalkmessages");
          });

      modelBuilder.Entity("EFCoreSqlite.Models.Journal.QuestPlate", b =>
          {
            b.Property<int>("Id")
                      .ValueGeneratedOnAdd()
                      .HasColumnType("INTEGER");

            b.Property<DateTime>("CreatedDate")
                      .HasColumnType("TEXT");

            b.Property<string>("OriginalLang")
                      .IsRequired()
                      .HasColumnType("TEXT");

            b.Property<string>("OriginalQuestMessage")
                      .IsRequired()
                      .HasMaxLength(2500)
                      .HasColumnType("TEXT");

            b.Property<string>("QuestId")
                      .IsRequired()
                      .HasColumnType("TEXT");

            b.Property<string>("QuestName")
                      .IsRequired()
                      .HasMaxLength(200)
                      .HasColumnType("TEXT");

            b.Property<byte[]>("RowVersion")
                      .IsConcurrencyToken()
                      .ValueGeneratedOnAddOrUpdate()
                      .HasColumnType("BLOB");

            b.Property<string>("TranslatedQuestMessage")
                      .IsRequired()
                      .HasMaxLength(2500)
                      .HasColumnType("TEXT");

            b.Property<string>("TranslatedQuestName")
                      .IsRequired()
                      .HasMaxLength(200)
                      .HasColumnType("TEXT");

            b.Property<int>("TranslationEngine")
                      .HasColumnType("INTEGER");

            b.Property<string>("TranslationLang")
                      .IsRequired()
                      .HasColumnType("TEXT");

            b.Property<DateTime?>("UpdatedDate")
                      .HasColumnType("TEXT");

            b.HasKey("Id");

            b.ToTable("questplates");
          });

      modelBuilder.Entity("EFCoreSqlite.Models.LocationName", b =>
          {
            b.Property<int>("Id")
                      .ValueGeneratedOnAdd()
                      .HasColumnType("INTEGER");

            b.Property<DateTime>("CreatedDate")
                      .HasColumnType("TEXT");

            b.Property<string>("OriginalLocationName")
                      .IsRequired()
                      .HasMaxLength(400)
                      .HasColumnType("TEXT");

            b.Property<string>("OriginalLocationNameLang")
                      .IsRequired()
                      .HasColumnType("TEXT");

            b.Property<byte[]>("RowVersion")
                      .IsConcurrencyToken()
                      .ValueGeneratedOnAddOrUpdate()
                      .HasColumnType("BLOB");

            b.Property<string>("TranslatedLocationName")
                      .IsRequired()
                      .HasColumnType("TEXT");

            b.Property<int>("TranslationEngine")
                      .HasColumnType("INTEGER");

            b.Property<string>("TranslationLang")
                      .IsRequired()
                      .HasColumnType("TEXT");

            b.Property<DateTime?>("UpdatedDate")
                      .HasColumnType("TEXT");

            b.HasKey("Id");

            b.ToTable("locationnames");
          });

      modelBuilder.Entity("EFCoreSqlite.Models.NpcNames", b =>
          {
            b.Property<int>("Id")
                      .ValueGeneratedOnAdd()
                      .HasColumnType("INTEGER");

            b.Property<DateTime>("CreatedDate")
                      .HasColumnType("TEXT");

            b.Property<string>("OriginalNpcName")
                      .IsRequired()
                      .HasMaxLength(400)
                      .HasColumnType("TEXT");

            b.Property<string>("OriginalNpcNameLang")
                      .IsRequired()
                      .HasColumnType("TEXT");

            b.Property<byte[]>("RowVersion")
                      .IsConcurrencyToken()
                      .ValueGeneratedOnAddOrUpdate()
                      .HasColumnType("BLOB");

            b.Property<string>("TranslatedNpcName")
                      .HasMaxLength(400)
                      .HasColumnType("TEXT");

            b.Property<int>("TranslationEngine")
                      .HasColumnType("INTEGER");

            b.Property<string>("TranslationLang")
                      .IsRequired()
                      .HasColumnType("TEXT");

            b.Property<DateTime?>("UpdatedDate")
                      .HasColumnType("TEXT");

            b.HasKey("Id");

            b.ToTable("npcnames");
          });

      modelBuilder.Entity("EFCoreSqlite.Models.TalkMessage", b =>
          {
            b.Property<int>("Id")
                      .ValueGeneratedOnAdd()
                      .HasColumnType("INTEGER");

            b.Property<DateTime>("CreatedDate")
                      .HasColumnType("TEXT");

            b.Property<string>("OriginalSenderNameLang")
                      .IsRequired()
                      .HasColumnType("TEXT");

            b.Property<string>("OriginalTalkMessage")
                      .IsRequired()
                      .HasMaxLength(400)
                      .HasColumnType("TEXT");

            b.Property<string>("OriginalTalkMessageLang")
                      .IsRequired()
                      .HasColumnType("TEXT");

            b.Property<byte[]>("RowVersion")
                      .IsConcurrencyToken()
                      .ValueGeneratedOnAddOrUpdate()
                      .HasColumnType("BLOB");

            b.Property<string>("SenderName")
                      .IsRequired()
                      .HasMaxLength(100)
                      .HasColumnType("TEXT");

            b.Property<string>("TranslatedSenderName")
                      .HasMaxLength(100)
                      .HasColumnType("TEXT");

            b.Property<string>("TranslatedTalkMessage")
                      .HasMaxLength(400)
                      .HasColumnType("TEXT");

            b.Property<int>("TranslationEngine")
                      .HasColumnType("INTEGER");

            b.Property<string>("TranslationLang")
                      .IsRequired()
                      .HasColumnType("TEXT");

            b.Property<DateTime?>("UpdatedDate")
                      .HasColumnType("TEXT");

            b.HasKey("Id");

            b.ToTable("talkmessages");
          });

      modelBuilder.Entity("EFCoreSqlite.Models.TalkSubtitleMessage", b =>
          {
            b.Property<int>("Id")
                      .ValueGeneratedOnAdd()
                      .HasColumnType("INTEGER");

            b.Property<DateTime>("CreatedDate")
                      .HasColumnType("TEXT");

            b.Property<string>("OriginalTalkSubtitleMessage")
                      .IsRequired()
                      .HasMaxLength(400)
                      .HasColumnType("TEXT");

            b.Property<string>("OriginalTalkSubtitleMessageLang")
                      .IsRequired()
                      .HasColumnType("TEXT");

            b.Property<byte[]>("RowVersion")
                      .IsConcurrencyToken()
                      .ValueGeneratedOnAddOrUpdate()
                      .HasColumnType("BLOB");

            b.Property<string>("TranslatedTalkSubtitleMessage")
                      .HasMaxLength(400)
                      .HasColumnType("TEXT");

            b.Property<int>("TranslationEngine")
                      .HasColumnType("INTEGER");

            b.Property<string>("TranslationLang")
                      .IsRequired()
                      .HasColumnType("TEXT");

            b.Property<DateTime?>("UpdatedDate")
                      .HasColumnType("TEXT");

            b.HasKey("Id");

            b.ToTable("talksubtitlemessages");
          });

      modelBuilder.Entity("EFCoreSqlite.Models.ToastMessage", b =>
          {
            b.Property<int>("Id")
                      .ValueGeneratedOnAdd()
                      .HasColumnType("INTEGER");

            b.Property<DateTime>("CreatedDate")
                      .HasColumnType("TEXT");

            b.Property<string>("OriginalLang")
                      .IsRequired()
                      .HasColumnType("TEXT");

            b.Property<string>("OriginalToastMessage")
                      .IsRequired()
                      .HasMaxLength(200)
                      .HasColumnType("TEXT");

            b.Property<byte[]>("RowVersion")
                      .IsConcurrencyToken()
                      .ValueGeneratedOnAddOrUpdate()
                      .HasColumnType("BLOB");

            b.Property<string>("ToastType")
                      .IsRequired()
                      .HasMaxLength(40)
                      .HasColumnType("TEXT");

            b.Property<string>("TranslatedToastMessage")
                      .HasMaxLength(200)
                      .HasColumnType("TEXT");

            b.Property<int>("TranslationEngine")
                      .HasColumnType("INTEGER");

            b.Property<string>("TranslationLang")
                      .IsRequired()
                      .HasColumnType("TEXT");

            b.Property<DateTime?>("UpdatedDate")
                      .HasColumnType("TEXT");

            b.HasKey("Id");

            b.ToTable("toastmessages");
          });
#pragma warning restore 612, 618
    }
  }
}
