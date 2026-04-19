// <copyright file="DbFirstStructuredStringArrayHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the canonical payload helper used by DB-first StringArrayData
///     addon runtimes.
/// </summary>
public class DbFirstStructuredStringArrayHelperTests
{
    /// <summary>
    ///     Ensures mixed ATK and StringArrayData slots are encoded into a
    ///     single canonical payload without collisions.
    /// </summary>
    [Fact]
    public void BuildCanonicalPayload_EncodesAtkAndStringArraySlots()
    {
        var payload = DbFirstStructuredStringArrayHelper.BuildCanonicalPayload(
            "Character",
            "addon:CharacterProfile",
            new Dictionary<int, string>
            {
                [0] = "Profile",
                [2] = "Biography",
            },
            new Dictionary<int, string>
            {
                [0] = "Classes/Jobs",
                [10] = "Reputation",
            });

        Assert.Equal("Character", payload.Type);
        Assert.Equal("addon:CharacterProfile", payload.ContextKey);
        Assert.Equal("Profile", payload.Slots[-1].OriginalText);
        Assert.Equal("atk:0", payload.Slots[-1].SemanticKey);
        Assert.Equal("Biography", payload.Slots[-3].OriginalText);
        Assert.Equal("Classes/Jobs", payload.Slots[0].OriginalText);
        Assert.Equal("stringarray:0", payload.Slots[0].SemanticKey);
        Assert.Equal("Reputation", payload.Slots[10].OriginalText);
    }

    /// <summary>
    ///     Ensures stable visible text-node entries are also encoded into the
    ///     canonical structured payload when present.
    /// </summary>
    [Fact]
    public void BuildCanonicalPayload_EncodesTextNodes()
    {
        var payload = DbFirstStructuredStringArrayHelper.BuildCanonicalPayload(
            "Character",
            "addon:Character",
            new Dictionary<int, string>(),
            new Dictionary<int, string>(),
            new Dictionary<string, string>
            {
                ["17:0"] = "Attributes",
                ["42:0"] = "Profile",
            });

        Assert.Equal("Attributes", payload.TextNodes["17:0"].OriginalText);
        Assert.Equal("textnode:17:0", payload.TextNodes["17:0"].SemanticKey);
        Assert.Equal("Profile", payload.TextNodes["42:0"].OriginalText);
    }

    /// <summary>
    ///     Ensures translated canonical slots project back to the live addon
    ///     maps using the original ATK and StringArrayData indices.
    /// </summary>
    [Fact]
    public void TryProjectTranslatedPayload_RestoresMixedLiveMaps()
    {
        var originalPayload = DbFirstStructuredStringArrayHelper
            .BuildCanonicalPayload(
                "Character",
                "addon:CharacterProfile",
                new Dictionary<int, string> { [0] = "Profile" },
                new Dictionary<int, string> { [10] = "Reputation" });
        var translatedPayload = DbFirstStructuredStringArrayHelper
            .BuildCanonicalPayload(
                "Character",
                "addon:CharacterProfile",
                new Dictionary<int, string> { [0] = "Profile" },
                new Dictionary<int, string> { [10] = "Reputation" });
        translatedPayload.Slots[-1].TranslatedText = "Perfil";
        translatedPayload.Slots[10].TranslatedText = "Reputacao";

        var projected = DbFirstStructuredStringArrayHelper
            .TryProjectTranslatedPayload(
                originalPayload,
                translatedPayload,
                out var projection);

        Assert.True(projected);
        Assert.Equal("Perfil", projection.AtkValues[0]);
        Assert.Equal("Reputacao", projection.StringArrayValues[10]);
    }

    /// <summary>
    ///     Ensures translated text-node payloads project back using their
    ///     stable node keys.
    /// </summary>
    [Fact]
    public void TryProjectTranslatedPayload_RestoresTextNodes()
    {
        var originalPayload = DbFirstStructuredStringArrayHelper
            .BuildCanonicalPayload(
                "Character",
                "addon:Character",
                new Dictionary<int, string>(),
                new Dictionary<int, string>(),
                new Dictionary<string, string>
                {
                    ["17:0"] = "Attributes",
                });
        var translatedPayload = DbFirstStructuredStringArrayHelper
            .BuildCanonicalPayload(
                "Character",
                "addon:Character",
                new Dictionary<int, string>(),
                new Dictionary<int, string>(),
                new Dictionary<string, string>
                {
                    ["17:0"] = "Attributes",
                });
        translatedPayload.TextNodes["17:0"].TranslatedText = "Atributos";

        var projected = DbFirstStructuredStringArrayHelper
            .TryProjectTranslatedPayload(
                originalPayload,
                translatedPayload,
                out var projection);

        Assert.True(projected);
        Assert.Equal("Atributos", projection.TextNodes["17:0"]);
    }

    /// <summary>
    ///     Ensures incomplete translated payloads are rejected so the runtime
    ///     keeps waiting instead of partially mutating the addon.
    /// </summary>
    [Fact]
    public void TryProjectTranslatedPayload_RejectsIncompleteTranslations()
    {
        var originalPayload = DbFirstStructuredStringArrayHelper
            .BuildCanonicalPayload(
                "Hud",
                "addon:Hud",
                new Dictionary<int, string>(),
                new Dictionary<int, string>
                {
                    [0] = "Duty List",
                    [1] = "Current Objective",
                });
        var translatedPayload = DbFirstStructuredStringArrayHelper
            .BuildCanonicalPayload(
                "Hud",
                "addon:Hud",
                new Dictionary<int, string>(),
                new Dictionary<int, string>
                {
                    [0] = "Duty List",
                    [1] = "Current Objective",
                });
        translatedPayload.Slots[0].TranslatedText = "Lista de Missoes";

        var projected = DbFirstStructuredStringArrayHelper
            .TryProjectTranslatedPayload(
                originalPayload,
                translatedPayload,
                out _);

        Assert.False(projected);
    }
}
