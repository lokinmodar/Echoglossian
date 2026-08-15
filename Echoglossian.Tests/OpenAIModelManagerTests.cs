// <copyright file="OpenAIModelManagerTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.Translators.Capabilities;
using Echoglossian.Translators.OpenAI;
using FluentAssertions;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers provider-scoped state retained by the shared OpenAI-family model
///     manager.
/// </summary>
public class OpenAIModelManagerTests
{
    /// <summary>
    ///     Ensures successful model discovery promotes known family defaults
    ///     to exact-model capability overlays.
    /// </summary>
    [Fact]
    public void ApplyRefreshSuccess_WithKnownFamilyModel_PromotesExactModelOverlay()
    {
        this.WithTemporaryConfigurationDirectory(_ =>
        {
            OpenAIModelManager.ResetAllForTesting();

            OpenAIModelManager.ApplyRefreshSuccess(
                "OpenAI",
                new DateTime(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc),
                "OpenAI",
                "https://api.openai.com/v1",
                [
                    new LlmTextModel(
                        "gpt-5.6-terra",
                        "GPT-5.6 Terra",
                        true,
                        false,
                        false,
                        false,
                        false,
                        "OpenAI"),
                ]);

            LlmCapabilityCacheManager.GetRuleDefinitions()
                .Should()
                .Contain(rule => rule.MatchValue == "gpt-5.6-terra");
        });
    }

    /// <summary>
    ///     Ensures official OpenAI and custom OpenAI-compatible model lists do
    ///     not overwrite each other inside the shared manager.
    /// </summary>
    [Fact]
    public void ApplyRefreshSuccess_WithDifferentProviders_KeepsListsIsolated()
    {
        OpenAIModelManager.ResetAllForTesting();

        OpenAIModelManager.ApplyRefreshSuccess(
            "OpenAI",
            new DateTime(2026, 7, 10, 15, 0, 0, DateTimeKind.Utc),
            "OpenAI",
            "https://api.openai.com/v1",
            new[]
            {
                new LlmTextModel(
                    "gpt-4o-mini",
                    "GPT-4o mini",
                    true,
                    false,
                    false,
                    true,
                    true,
                    "OpenAI"),
            });
        OpenAIModelManager.ApplyRefreshSuccess(
            "OpenAI-Compatible",
            new DateTime(2026, 7, 10, 15, 1, 0, DateTimeKind.Utc),
            "OpenAI-Compatible",
            "https://example.test/v1",
            new[]
            {
                new LlmTextModel(
                    "llama-3.3-70b",
                    "Llama 3.3 70B",
                    true,
                    false,
                    false,
                    false,
                    false,
                    "OpenAI-Compatible"),
            });

        OpenAIModelManager.GetCurrentModelList("OpenAI")
            .Select(model => model.Id)
            .Should()
            .Equal("gpt-4o-mini");
        OpenAIModelManager.GetCurrentModelList("OpenAI-Compatible")
            .Select(model => model.Id)
            .Should()
            .Equal("llama-3.3-70b");
    }

    /// <summary>
    ///     Ensures resetting one provider profile does not discard another
    ///     provider's retained live model list.
    /// </summary>
    [Fact]
    public void ResetToDefault_WithSpecificProvider_LeavesOtherProviderStateIntact()
    {
        OpenAIModelManager.ResetAllForTesting();

        OpenAIModelManager.ApplyRefreshSuccess(
            "OpenAI",
            new DateTime(2026, 7, 10, 15, 2, 0, DateTimeKind.Utc),
            "OpenAI",
            "https://api.openai.com/v1",
            new[]
            {
                new LlmTextModel(
                    "gpt-4.1-mini",
                    "GPT-4.1 mini",
                    true,
                    false,
                    false,
                    true,
                    true,
                    "OpenAI"),
            });
        OpenAIModelManager.ApplyRefreshSuccess(
            "OpenAI-Compatible",
            new DateTime(2026, 7, 10, 15, 3, 0, DateTimeKind.Utc),
            "OpenAI-Compatible",
            "https://example.test/v1",
            new[]
            {
                new LlmTextModel(
                    "qwen-72b",
                    "Qwen 72B",
                    true,
                    false,
                    false,
                    false,
                    false,
                    "OpenAI-Compatible"),
            });

        OpenAIModelManager.ResetToDefault("OpenAI-Compatible");

        OpenAIModelManager.GetCurrentModelList("OpenAI")
            .Select(model => model.Id)
            .Should()
            .Equal("gpt-4.1-mini");
        OpenAIModelManager.GetCurrentModelList("OpenAI-Compatible")
            .Should()
            .BeEquivalentTo(OpenAITextModelDefaults.PredefinedModels);
    }

    /// <summary>
    ///     Runs an action with an isolated persisted capability database.
    /// </summary>
    /// <param name="action">The action that exercises the model manager.</param>
    private void WithTemporaryConfigurationDirectory(Action<string> action)
    {
        var configDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var originalConfigDirectory = Echoglossian.ConfigDirectory;
        Directory.CreateDirectory(configDir);
        Echoglossian.ConfigDirectory = configDir;
        LlmCapabilityCacheManager.Clear();

        try
        {
            action(configDir);
        }
        finally
        {
            LlmCapabilityCacheManager.Clear();
            Echoglossian.ConfigDirectory = originalConfigDirectory;
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(configDir, recursive: true);
        }
    }
}
