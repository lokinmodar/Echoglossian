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

    /// <summary>
    ///     Ensures an unsupported selected engine is normalized to the first
    ///     supported concrete engine for the active language.
    /// </summary>
    [Fact]
    public void ResolveSupportedEngineSelection_UnsupportedSelection_ReturnsFirstSupported()
    {
        var resolved = TranslationEngineSelectionMigrationHelper
            .ResolveSupportedEngineSelection(
                (int)Echoglossian.TransEngines.Google,
                new[] { (int)Echoglossian.TransEngines.Microsoft, (int)Echoglossian.TransEngines.Amazon });

        Assert.Equal((int)Echoglossian.TransEngines.Microsoft, resolved);
    }

    /// <summary>
    ///     Ensures a supported selected engine remains unchanged.
    /// </summary>
    [Fact]
    public void ResolveSupportedEngineSelection_SupportedSelection_Preserved()
    {
        var resolved = TranslationEngineSelectionMigrationHelper
            .ResolveSupportedEngineSelection(
                (int)Echoglossian.TransEngines.Amazon,
                new[] { (int)Echoglossian.TransEngines.Microsoft, (int)Echoglossian.TransEngines.Amazon });

        Assert.Equal((int)Echoglossian.TransEngines.Amazon, resolved);
    }

    /// <summary>
    ///     Ensures the persisted engine key takes precedence when both the key
    ///     and the numeric engine id exist.
    /// </summary>
    [Fact]
    public void NormalizeAndSyncSelection_ValidEngineKey_PrefersKey()
    {
        var config = new Config
        {
            Version = 5,
            ChosenTransEngine = (int)Echoglossian.TransEngines.Google,
            ChosenTransEngineKey = nameof(Echoglossian.TransEngines.Claude),
        };

        var changed = TranslationEngineSelectionMigrationHelper
            .NormalizeAndSyncSelection(config, 5);

        Assert.True(changed);
        Assert.Equal((int)Echoglossian.TransEngines.Claude, config.ChosenTransEngine);
        Assert.Equal(nameof(Echoglossian.TransEngines.Claude), config.ChosenTransEngineKey);
    }

    /// <summary>
    ///     Ensures the helper backfills the persisted engine key when only the
    ///     numeric engine id is present.
    /// </summary>
    [Fact]
    public void NormalizeAndSyncSelection_MissingEngineKey_SynchronizesKey()
    {
        var config = new Config
        {
            Version = 15,
            ChosenTransEngine = (int)Echoglossian.TransEngines.Microsoft,
            ChosenTransEngineKey = string.Empty,
        };

        var changed = TranslationEngineSelectionMigrationHelper
            .NormalizeAndSyncSelection(config, 15);

        Assert.True(changed);
        Assert.Equal((int)Echoglossian.TransEngines.Microsoft, config.ChosenTransEngine);
        Assert.Equal(nameof(Echoglossian.TransEngines.Microsoft), config.ChosenTransEngineKey);
    }

    /// <summary>
    ///     Ensures the helper still normalizes the resolved engine against the
    ///     active language support list.
    /// </summary>
    [Fact]
    public void NormalizeAndSyncSelection_UnsupportedResolvedEngine_FallsBackToSupported()
    {
        var config = new Config
        {
            Version = 15,
            ChosenTransEngine = (int)Echoglossian.TransEngines.Claude,
            ChosenTransEngineKey = nameof(Echoglossian.TransEngines.Claude),
        };

        var changed = TranslationEngineSelectionMigrationHelper
            .NormalizeAndSyncSelection(
                config,
                15,
                new[] { (int)Echoglossian.TransEngines.Google, (int)Echoglossian.TransEngines.Microsoft });

        Assert.True(changed);
        Assert.Equal((int)Echoglossian.TransEngines.Google, config.ChosenTransEngine);
        Assert.Equal(nameof(Echoglossian.TransEngines.Google), config.ChosenTransEngineKey);
    }

    /// <summary>
    ///     Ensures current-schema configs prefer the concrete numeric engine id
    ///     when a stale engine key disagrees with it.
    /// </summary>
    [Fact]
    public void NormalizeAndSyncSelection_CurrentSchema_PrefersConcreteEngineId()
    {
        var config = new Config
        {
            Version = 15,
            ChosenTransEngine = (int)Echoglossian.TransEngines.Google,
            ChosenTransEngineKey = nameof(Echoglossian.TransEngines.ChatGPT),
        };

        var changed = TranslationEngineSelectionMigrationHelper
            .NormalizeAndSyncSelection(config, 15);

        Assert.True(changed);
        Assert.Equal((int)Echoglossian.TransEngines.Google, config.ChosenTransEngine);
        Assert.Equal(nameof(Echoglossian.TransEngines.Google), config.ChosenTransEngineKey);
    }

    /// <summary>
    ///     Ensures an explicit user selection updates both persisted engine
    ///     forms immediately.
    /// </summary>
    [Fact]
    public void ApplyExplicitSelection_SynchronizesEngineIdAndKey()
    {
        var config = new Config
        {
            Version = 15,
            ChosenTransEngine = (int)Echoglossian.TransEngines.ChatGPT,
            ChosenTransEngineKey = nameof(Echoglossian.TransEngines.ChatGPT),
        };

        TranslationEngineSelectionMigrationHelper.ApplyExplicitSelection(
            config,
            (int)Echoglossian.TransEngines.Google);

        Assert.Equal((int)Echoglossian.TransEngines.Google, config.ChosenTransEngine);
        Assert.Equal(nameof(Echoglossian.TransEngines.Google), config.ChosenTransEngineKey);
    }
}
