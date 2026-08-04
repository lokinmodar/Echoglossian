// <copyright file="TooltipAddonPersistenceTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;
using System.Runtime.CompilerServices;

using Echoglossian.EFCoreSqlite;
using Echoglossian.LanguagesHandling;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Xunit;

using PluginEntry = Echoglossian.Echoglossian;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the dedicated Tooltip addon persistence contract.
/// </summary>
public sealed class TooltipAddonPersistenceTests
{
    /// <summary>
    ///     Ensures the dedicated writer inserts one identity row and updates
    ///     that row rather than creating a duplicate.
    /// </summary>
    [Fact]
    public async Task InsertTooltipTextData_InsertsThenUpdatesDedicatedRow()
    {
        var tooltipTextType = ResolveTooltipTextType();
        var configDir = CreateTempConfigDirectory();
        var previousConfigDirectory = PluginEntry.ConfigDirectory;
        PluginEntry.ConfigDirectory = configDir + Path.DirectorySeparatorChar;
        var plugin = CreateFormattingPlugin(new Config
        {
            Lang = 28,
            ChosenTransEngine = 0,
            TranslateAlreadyTranslatedTexts = true,
        });
        var originalTexts = JsonConvert.SerializeObject(new[] { "Social" });

        try
        {
            await using (var context = new EchoglossianDbContext(configDir))
            {
                await context.Database.MigrateAsync();
            }

            var row = CreateTooltipTextRow(
                tooltipTextType,
                "Tooltip",
                originalTexts,
                "en",
                JsonConvert.SerializeObject(new[] { "Social" }),
                "pt-BR",
                0,
                "test-version",
                "tooltip-write-hash");

            Assert.Equal(
                "Data inserted to TooltipTexts table.",
                await InvokeInsertTooltipTextDataAsync(plugin, row));

            SetPropertyValue(row, "Id", 0);
            SetPropertyValue(
                row,
                "TranslatedTextsAsText",
                JsonConvert.SerializeObject(new[] { "Social traduzido" }));
            Assert.Equal(
                "Data updated in TooltipTexts table.",
                await InvokeInsertTooltipTextDataAsync(plugin, row));

            await using var verification = new EchoglossianDbContext(configDir);
            var tooltipTextsProperty = typeof(EchoglossianDbContext).GetProperty(
                "TooltipTexts",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(tooltipTextsProperty);

            var set = tooltipTextsProperty!.GetValue(verification);
            Assert.NotNull(set);
            var rows = Assert.IsAssignableFrom<System.Collections.IEnumerable>(set)
                .Cast<object>()
                .ToList();
            var persisted = Assert.Single(rows);
            Assert.Equal(
                JsonConvert.SerializeObject(new[] { "Social traduzido" }),
                GetPropertyValue<string>(persisted, "TranslatedTextsAsText"));
        }
        finally
        {
            PluginEntry.ConfigDirectory = previousConfigDirectory;
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures a Tooltip payload reuses a row scoped to its source
    ///     content hash.
    /// </summary>
    [Fact]
    public void FindTooltipText_ReusesHashScopedPayload()
    {
        var tooltipTextType = ResolveTooltipTextType();
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
                context.Add(CreateTooltipTextRow(
                    tooltipTextType,
                    "Tooltip",
                    JsonConvert.SerializeObject(new[] { "Travel" }),
                    "en",
                    JsonConvert.SerializeObject(new[] { "Viagem" }),
                    "pt-BR",
                    0,
                    "test-version",
                    "tooltip-hash"));
                context.SaveChanges();
            }

            var lookup = CreateTooltipTextRow(
                tooltipTextType,
                "Tooltip",
                JsonConvert.SerializeObject(new[] { "Travel" }),
                "en",
                string.Empty,
                "pt-BR",
                0,
                "test-version",
                "tooltip-hash");

            Assert.NotNull(InvokeFindTooltipText(plugin, lookup));
        }
        finally
        {
            PluginEntry.ConfigDirectory = previousConfigDirectory;
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Resolves the TooltipText model type once the implementation exists.
    /// </summary>
    /// <returns>The resolved model type.</returns>
    private static Type ResolveTooltipTextType()
    {
        var tooltipTextType = typeof(PluginEntry).Assembly.GetType(
            "Echoglossian.EFCoreSqlite.Models.TooltipText");

        Assert.NotNull(tooltipTextType);
        return tooltipTextType!;
    }

    /// <summary>
    ///     Creates one dedicated TooltipText model instance via reflection.
    /// </summary>
    /// <param name="tooltipTextType">The resolved TooltipText type.</param>
    /// <param name="addonName">The addon name.</param>
    /// <param name="originalTextsAsText">The serialized original texts.</param>
    /// <param name="originalLang">The original language.</param>
    /// <param name="translatedTextsAsText">The serialized translated texts.</param>
    /// <param name="translationLang">The target language.</param>
    /// <param name="translationEngine">The translation engine id.</param>
    /// <param name="gameVersion">The stored game version.</param>
    /// <param name="sourceContentHash">The source content hash.</param>
    /// <returns>The initialized row instance.</returns>
    private static object CreateTooltipTextRow(
        Type tooltipTextType,
        string addonName,
        string originalTextsAsText,
        string originalLang,
        string translatedTextsAsText,
        string translationLang,
        int translationEngine,
        string gameVersion,
        string sourceContentHash)
    {
        var row = Activator.CreateInstance(tooltipTextType);
        Assert.NotNull(row);
        SetPropertyValue(row, "AddonName", addonName);
        SetPropertyValue(row, "OriginalTextsAsText", originalTextsAsText);
        SetPropertyValue(row, "OriginalLang", originalLang);
        SetPropertyValue(row, "TranslatedTextsAsText", translatedTextsAsText);
        SetPropertyValue(row, "TranslationLang", translationLang);
        SetPropertyValue(row, "TranslationEngine", translationEngine);
        SetPropertyValue(row, "GameVersion", gameVersion);
        SetPropertyValue(row, "SourceContentHash", sourceContentHash);
        SetPropertyValue(row, "CreatedDate", DateTime.UtcNow);
        SetPropertyValue(row, "UpdatedDate", DateTime.UtcNow);
        return row!;
    }

    /// <summary>
    ///     Invokes the dedicated Tooltip lookup seam via reflection.
    /// </summary>
    /// <param name="plugin">The plugin instance to query.</param>
    /// <param name="lookup">The lookup row.</param>
    /// <returns>The matched persisted row, or <see langword="null" />.</returns>
    private static object? InvokeFindTooltipText(PluginEntry plugin, object lookup)
    {
        var method = typeof(PluginEntry).GetMethod(
            "FindTooltipText",
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);
        return method!.Invoke(plugin, [lookup]);
    }

    /// <summary>
    ///     Invokes the dedicated Tooltip writer seam via reflection.
    /// </summary>
    /// <param name="plugin">The plugin instance to update.</param>
    /// <param name="row">The row to persist.</param>
    /// <returns>The persistence status text.</returns>
    private static async Task<string> InvokeInsertTooltipTextDataAsync(
        PluginEntry plugin,
        object row)
    {
        var method = typeof(PluginEntry).GetMethod(
            "InsertTooltipTextData",
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);

        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(plugin, [row]));
        await task;
        return GetPropertyValue<string>(task, "Result");
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
    ///     Sets one reflected property value on a dedicated model instance.
    /// </summary>
    /// <param name="instance">The instance to mutate.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The property value.</param>
    private static void SetPropertyValue(
        object? instance,
        string propertyName,
        object? value)
    {
        Assert.NotNull(instance);
        var property = instance!.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        property!.SetValue(instance, value);
    }

    /// <summary>
    ///     Reads one reflected property value from a dedicated model instance.
    /// </summary>
    /// <typeparam name="T">The expected property type.</typeparam>
    /// <param name="instance">The instance to inspect.</param>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The property value.</returns>
    private static T GetPropertyValue<T>(object? instance, string propertyName)
    {
        Assert.NotNull(instance);
        var property = instance!.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        return Assert.IsType<T>(property!.GetValue(instance));
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
