// <copyright file="MainCommandCanonicalTextResolverTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.NativeUI.Helpers;
using System.Security.Cryptography;
using System.Text;

namespace Echoglossian.Tests;

/// <summary>
///     Covers canonical MainCommand text resolution used by the live
///     <c>_MainCommand</c> and <c>AddonContextMenuTitle</c> handlers.
/// </summary>
public class MainCommandCanonicalTextResolverTests
{
    private const int TestTranslationEngine = 91042;
    private const string TestTargetLanguage = "fa";
    private const string TestGameVersion = "resolver-test";

    /// <summary>
    ///     Ensures integer-keyed payload maps can reuse canonical MainCommand
    ///     translations without requiring an exact persisted GameWindow row.
    /// </summary>
    [Fact]
    public void TryResolveTranslatedIntMap_UsesCanonicalMainCommandText()
    {
        ReferenceTextCacheRegistry.MainCommandTexts.Update(
            CreateCanonicalMainCommandTextRow(
                referenceId: 7001001,
                originalName: "Character",
                translatedName: "شخصیت"));

        var sourceValues = new SortedDictionary<int, string>
        {
            [3] = "Character",
            [4] = "Actions & Traits",
        };

        var resolved = MainCommandCanonicalTextResolver
            .TryResolveTranslatedIntMap(
                sourceValues,
                TestTargetLanguage,
                TestTranslationEngine,
                TestGameVersion,
                out var translatedValues);

        Assert.True(resolved);
        Assert.Equal("شخصیت", translatedValues[3]);
        Assert.Equal("Actions & Traits", translatedValues[4]);
    }

    /// <summary>
    ///     Ensures integer-keyed payload maps can recover canonical originals
    ///     from visible translated MainCommand labels.
    /// </summary>
    [Fact]
    public void TryResolveOriginalIntMap_UsesCanonicalMainCommandText()
    {
        ReferenceTextCacheRegistry.MainCommandTexts.Update(
            CreateCanonicalMainCommandTextRow(
                referenceId: 7001002,
                originalName: "Journal",
                translatedName: "گزارش"));

        var sourceValues = new SortedDictionary<int, string>
        {
            [3] = "گزارش",
            [4] = "Timers",
        };

        var resolved = MainCommandCanonicalTextResolver
            .TryResolveOriginalIntMap(
                sourceValues,
                TestTargetLanguage,
                TestTranslationEngine,
                TestGameVersion,
                out var originalValues);

        Assert.True(resolved);
        Assert.Equal("Journal", originalValues[3]);
        Assert.Equal("Timers", originalValues[4]);
    }

    /// <summary>
    ///     Ensures text-node payload maps can reuse canonical MainCommand
    ///     translations for title-only surfaces such as
    ///     <c>AddonContextMenuTitle</c>.
    /// </summary>
    [Fact]
    public void TryResolveTranslatedTextMap_UsesCanonicalMainCommandText()
    {
        ReferenceTextCacheRegistry.MainCommandTexts.Update(
            CreateCanonicalMainCommandTextRow(
                referenceId: 7001003,
                originalName: "Actions & Traits",
                translatedName: "اقدامات و ویژگی\u200cها"));

        var sourceValues = new SortedDictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["17:0"] = "Actions & Traits",
            ["18:0"] = "Unmapped Title",
        };

        var resolved = MainCommandCanonicalTextResolver
            .TryResolveTranslatedTextMap(
                sourceValues,
                TestTargetLanguage,
                TestTranslationEngine,
                TestGameVersion,
                out var translatedValues);

        Assert.True(resolved);
        Assert.Equal("اقدامات و ویژگی\u200cها", translatedValues["17:0"]);
        Assert.Equal("Unmapped Title", translatedValues["18:0"]);
    }

    /// <summary>
    ///     Ensures canonical resolution reports failure when the cache cannot
    ///     improve any visible MainCommand label.
    /// </summary>
    [Fact]
    public void TryResolveTranslatedIntMap_ReturnsFalse_WhenNothingChanges()
    {
        var sourceValues = new SortedDictionary<int, string>
        {
            [3] = "No Cached Match",
        };

        var resolved = MainCommandCanonicalTextResolver
            .TryResolveTranslatedIntMap(
                sourceValues,
                TestTargetLanguage,
                TestTranslationEngine,
                TestGameVersion,
                out var translatedValues);

        Assert.False(resolved);
        Assert.Equal("No Cached Match", translatedValues[3]);
    }

    /// <summary>
    ///     Creates one canonical MainCommand row using the runtime's
    ///     metadata-sensitive hash semantics.
    /// </summary>
    /// <param name="referenceId">The stable MainCommand row identifier.</param>
    /// <param name="originalName">The original visible name.</param>
    /// <param name="translatedName">The translated visible name.</param>
    /// <returns>The canonical MainCommand row.</returns>
    private static MainCommandText CreateCanonicalMainCommandTextRow(
        uint referenceId,
        string originalName,
        string translatedName)
    {
        var originalPayload = new ReferenceTextCanonicalPayload
        {
            ReferenceId = referenceId,
            IconId = referenceId,
            CategoryId = 1,
            MainCommandCategoryId = 2,
            Unknown0 = 3,
            SortId = referenceId,
            Name = originalName,
        };
        var translatedPayload = new ReferenceTextCanonicalPayload
        {
            ReferenceId = referenceId,
            IconId = referenceId,
            CategoryId = 1,
            MainCommandCategoryId = 2,
            Unknown0 = 3,
            SortId = referenceId,
            Name = originalName,
            TranslatedName = translatedName,
        };

        var row = ReferenceTextPersistenceHelper.CreateCanonicalRow<MainCommandText>(
            originalLang: "en",
            translationLang: TestTargetLanguage,
            translationEngine: TestTranslationEngine,
            gameVersion: TestGameVersion,
            originalPayload,
            translatedPayload);
        row.IconId = originalPayload.IconId;
        row.CategoryId = originalPayload.CategoryId;
        row.MainCommandCategoryId = originalPayload.MainCommandCategoryId;
        row.Unknown0 = originalPayload.Unknown0;
        row.SortId = originalPayload.SortId;
        row.SourceContentHash = ComputeMainCommandSourceContentHash(
            originalPayload);
        return row;
    }

    /// <summary>
    ///     Computes the metadata-sensitive MainCommand source hash used by the
    ///     canonical cache.
    /// </summary>
    /// <param name="payload">The canonical source payload.</param>
    /// <returns>The stable source hash.</returns>
    private static string ComputeMainCommandSourceContentHash(
        ReferenceTextCanonicalPayload payload)
    {
        var builder = new StringBuilder();
        builder.Append(payload.SchemaVersion)
            .Append('|')
            .Append(payload.ReferenceId)
            .Append('|')
            .Append(payload.IconId?.ToString() ?? string.Empty)
            .Append('|')
            .Append(payload.CategoryId?.ToString() ?? string.Empty)
            .Append('|')
            .Append(payload.MainCommandCategoryId?.ToString() ?? string.Empty)
            .Append('|')
            .Append(payload.Unknown0?.ToString() ?? string.Empty)
            .Append('|')
            .Append(payload.SortId?.ToString() ?? string.Empty)
            .Append('|')
            .Append(payload.Name)
            .Append('|')
            .Append(payload.Description ?? string.Empty);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes);
    }
}
