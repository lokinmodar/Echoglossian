// <copyright file="TranslationEngineSelectionMigrationHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers compatibility and safety rules for persisted translation engine
///     selections.
/// </summary>
public class TranslationEngineSelectionMigrationHelperTests
{
    /// <summary>
    ///     Ensures the v3.25.x YandexPublic id is remapped to the current
    ///     YandexPublic engine id.
    /// </summary>
    [Fact]
    public void TryMigrateLegacyV325Selection_YandexPublic_RemapApplied()
    {
        var migrated = TranslationEngineSelectionMigrationHelper
            .TryMigrateLegacyV325Selection(
                5,
                9,
                out var migratedEngineId);

        Assert.True(migrated);
        Assert.Equal((int)Echoglossian.TransEngines.YandexPublic, migratedEngineId);
    }

    /// <summary>
    ///     Ensures legacy Bing selections are mapped to the current Microsoft
    ///     translator slot.
    /// </summary>
    [Fact]
    public void TryMigrateLegacyV325Selection_Bing_RemapApplied()
    {
        var migrated = TranslationEngineSelectionMigrationHelper
            .TryMigrateLegacyV325Selection(
                5,
                3,
                out var migratedEngineId);

        Assert.True(migrated);
        Assert.Equal((int)Echoglossian.TransEngines.Microsoft, migratedEngineId);
    }

    /// <summary>
    ///     Ensures already-current config versions do not get a full legacy
    ///     remap applied.
    /// </summary>
    [Fact]
    public void TryMigrateLegacyV325Selection_CurrentSchema_DoesNotRemap()
    {
        var migrated = TranslationEngineSelectionMigrationHelper
            .TryMigrateLegacyV325Selection(
                15,
                9,
                out var migratedEngineId);

        Assert.False(migrated);
        Assert.Equal(9, migratedEngineId);
    }

    /// <summary>
    ///     Ensures the legacy ChatGPT endpoint URL is normalized to the API
    ///     root.
    /// </summary>
    [Fact]
    public void NormalizeLegacyChatGptBaseUrl_LegacyCompletionsPath_ReturnsApiRoot()
    {
        var normalized = TranslationEngineSelectionMigrationHelper
            .NormalizeLegacyChatGptBaseUrl(
                "https://api.openai.com/v1/chat/completions");

        Assert.Equal("https://api.openai.com/v1", normalized);
    }

    /// <summary>
    ///     Ensures legacy YandexPublic-to-Amazon collisions are repaired when
    ///     no explicit Amazon configuration exists.
    /// </summary>
    [Fact]
    public void TryRepairLikelyLegacyAmazonCollision_NoAmazonConfig_RemapApplied()
    {
        var config = new Config
        {
            ChosenTransEngine = (int)Echoglossian.TransEngines.Amazon,
        };

        var repaired = TranslationEngineSelectionMigrationHelper
            .TryRepairLikelyLegacyAmazonCollision(config);

        Assert.True(repaired);
        Assert.Equal((int)Echoglossian.TransEngines.YandexPublic, config.ChosenTransEngine);
    }

    /// <summary>
    ///     Ensures explicit Amazon configuration is respected and not remapped
    ///     as a legacy collision.
    /// </summary>
    [Fact]
    public void TryRepairLikelyLegacyAmazonCollision_WithAmazonConfig_DoesNotRemap()
    {
        var config = new Config
        {
            ChosenTransEngine = (int)Echoglossian.TransEngines.Amazon,
            AwsAccessKey = "configured-access-key",
        };

        var repaired = TranslationEngineSelectionMigrationHelper
            .TryRepairLikelyLegacyAmazonCollision(config);

        Assert.False(repaired);
        Assert.Equal((int)Echoglossian.TransEngines.Amazon, config.ChosenTransEngine);
    }

    /// <summary>
    ///     Ensures the helper rejects sentinel values that are not concrete
    ///     runtime engine selections.
    /// </summary>
    [Fact]
    public void IsConcreteEngineId_AllSentinel_IsRejected()
    {
        var valid = TranslationEngineSelectionMigrationHelper.IsConcreteEngineId(
            (int)Echoglossian.TransEngines.All);

        Assert.False(valid);
    }
}
