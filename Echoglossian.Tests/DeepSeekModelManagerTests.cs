// <copyright file="DeepSeekModelManagerTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.DeepSeek;
using Echoglossian.Translators.OpenAI;
using FluentAssertions;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers DeepSeek live model-list endpoint normalization.
/// </summary>
public class DeepSeekModelManagerTests
{
    /// <summary>
    ///     Ensures successful DeepSeek model discovery retains the discovered
    ///     list while capability overlay promotion runs best-effort.
    /// </summary>
    [Fact]
    public void ApplyRefreshSuccess_WithDiscoveredModels_RetainsDiscoveredModelList()
    {
        DeepSeekModelManager.ResetToDefault();

        DeepSeekModelManager.ApplyRefreshSuccess(
            "https://api.deepseek.com/v1",
            [
                new LlmTextModel(
                    "deepseek-chat",
                    "DeepSeek Chat",
                    true,
                    false,
                    false,
                    false,
                    false,
                    "DeepSeek"),
            ],
            new DateTime(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc));

        DeepSeekModelManager.CurrentModelList
            .Select(static model => model.Id)
            .Should()
            .Equal("deepseek-chat");
    }

    /// <summary>
    ///     Ensures the configured DeepSeek base URL maps to the models
    ///     endpoint without duplicating version segments.
    /// </summary>
    /// <param name="baseUrl">The configured base URL.</param>
    /// <param name="expectedEndpoint">The expected models endpoint.</param>
    [Theory]
    [InlineData("https://api.deepseek.com/v1", "https://api.deepseek.com/v1/models")]
    [InlineData("https://api.deepseek.com", "https://api.deepseek.com/models")]
    [InlineData(" https://api.deepseek.com/v1/ ", "https://api.deepseek.com/v1/models")]
    public void BuildModelsEndpoint_WithConfiguredBaseUrl_NormalizesExpectedPath(
        string baseUrl,
        string expectedEndpoint)
    {
        var endpoint = DeepSeekModelManager.BuildModelsEndpoint(baseUrl);

        endpoint.Should().Be(expectedEndpoint);
    }
}
