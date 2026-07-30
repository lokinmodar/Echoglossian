// <copyright file="ContextMenuPersistenceTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;
using System.Runtime.CompilerServices;

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.LanguagesHandling;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Xunit;

using PluginEntry = Echoglossian.Echoglossian;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the dedicated ContextMenu persistence contract.
/// </summary>
public class ContextMenuPersistenceTests
{
    /// <summary>
    ///     Ensures a ContextMenu payload reuses a row scoped to its source
    ///     content hash.
    /// </summary>
    [Fact]
    public void FindContextMenuText_ReusesHashScopedPayload()
    {
        var configDir = CreateTempConfigDirectory();
        var previousConfigDirectory = PluginEntry.ConfigDirectory;
        PluginEntry.ConfigDirectory = configDir + Path.DirectorySeparatorChar;
        var plugin = CreateFormattingPlugin(new Config
        {
            Lang = 28,
            ChosenTransEngine = 0,
            TranslateAlreadyTranslatedTexts = true,
        });

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
                context.Add(new ContextMenuText
                {
                    AddonName = "ContextMenu",
                    OriginalTextsAsText = JsonConvert.SerializeObject(new[] { "Dismiss", "Emote" }),
                    OriginalLang = "en",
                    TranslatedTextsAsText = JsonConvert.SerializeObject(new[] { "Dispensar", "Emote" }),
                    TranslationLang = "pt-BR",
                    TranslationEngine = 0,
                    GameVersion = "test-version",
                    SourceContentHash = "contextmenu-hash",
                    CreatedDate = DateTime.UtcNow,
                    UpdatedDate = DateTime.UtcNow,
                });
                context.SaveChanges();
            }

            var lookup = new ContextMenuText
            {
                AddonName = "ContextMenu",
                OriginalTextsAsText = JsonConvert.SerializeObject(new[] { "Dismiss", "Emote" }),
                OriginalLang = "en",
                TranslatedTextsAsText = string.Empty,
                TranslationLang = "pt-BR",
                TranslationEngine = 0,
                GameVersion = "test-version",
                SourceContentHash = "contextmenu-hash",
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow,
            };

            Assert.NotNull(plugin.FindContextMenuText(lookup));
        }
        finally
        {
            PluginEntry.ConfigDirectory = previousConfigDirectory;
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Creates an isolated temporary configuration directory.
    /// </summary>
    /// <returns>The created configuration directory path.</returns>
    private static string CreateTempConfigDirectory()
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);
        return configDir;
    }

    /// <summary>
    ///     Creates the minimal plugin instance required by database lookups.
    /// </summary>
    /// <param name="config">The configuration to assign to the plugin.</param>
    /// <returns>The formatting-only plugin instance.</returns>
    private static PluginEntry CreateFormattingPlugin(Config config)
    {
        var plugin = (PluginEntry)RuntimeHelpers.GetUninitializedObject(
            typeof(PluginEntry));
        var languages = new Dictionary<int, LanguageInfo>
        {
            [28] = new LanguageInfo(
                "pt-BR",
                "Portuguese",
                string.Empty,
                string.Empty,
                []),
        };

        SetPrivateField(plugin, "configuration", config);
        SetPrivateField(plugin, "languagesDictionary", languages);
        return plugin;
    }

    /// <summary>
    ///     Sets one private plugin field for an isolated formatting test.
    /// </summary>
    /// <param name="plugin">The plugin instance to update.</param>
    /// <param name="fieldName">The private field name.</param>
    /// <param name="value">The field value.</param>
    private static void SetPrivateField(
        PluginEntry plugin,
        string fieldName,
        object value)
    {
        var field = typeof(PluginEntry).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(plugin, value);
    }

    /// <summary>
    ///     Deletes an isolated temporary directory when it is no longer used.
    /// </summary>
    /// <param name="path">The directory path to delete.</param>
    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best-effort cleanup for temporary SQLite files.
        }
    }
}
