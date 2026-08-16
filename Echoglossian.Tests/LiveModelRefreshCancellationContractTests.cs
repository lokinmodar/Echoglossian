// <copyright file="LiveModelRefreshCancellationContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Guards shutdown cancellation wiring for live model refresh requests.
/// </summary>
public sealed class LiveModelRefreshCancellationContractTests
{
    /// <summary>
    ///     Ensures the production engine panels pass the coordinator-owned
    ///     cancellation token into their live model refresh delegates instead
    ///     of discarding it.
    /// </summary>
    [Fact]
    public void EnginePanels_PropagateCoordinatorCancellationToken_ToLiveModelRefreshDelegates()
    {
        var root = FindRepositoryRoot();
        var expectedSnippets = new Dictionary<string, string[]>
        {
            ["ChatGptEngineUI.cs"] =
            [
                "cancellationToken => RefreshLiveModelsAsync(",
                "CancellationToken cancellationToken)",
                "await RefreshCustomLiveModelsAsync(",
                "\"OpenAI-Compatible\",",
                "\"OpenAI\",",
            ],
            ["ClaudeEngineUI.cs"] =
            [
                "cancellationToken => ClaudeModelManager.RefreshAsync(",
                "cancellationToken));",
            ],
            ["DeepSeekEngineUI.cs"] =
            [
                "cancellationToken => DeepSeekModelManager.RefreshAsync(",
                "cancellationToken));",
            ],
            ["GeminiEngineUI.cs"] =
            [
                "cancellationToken => GeminiModelManager.RefreshAsync(",
                "cancellationToken));",
            ],
            ["LmStudioEngineUI.cs"] =
            [
                "cancellationToken => LmStudioModelManager.RefreshAsync(",
                "cancellationToken));",
            ],
            ["OllamaEngineUI.cs"] =
            [
                "cancellationToken => OllamaModelManager.RefreshAsync(",
                "cancellationToken));",
            ],
            ["OpenRouterEngineUI.cs"] =
            [
                "cancellationToken => OpenRouterModelManager.RefreshAsync(",
                "cancellationToken));",
            ],
        };

        foreach (var (fileName, snippets) in expectedSnippets)
        {
            var source = File.ReadAllText(Path.Combine(
                root.FullName,
                "PluginUI",
                "EngineConfigUI",
                fileName));

            foreach (var snippet in snippets)
            {
                Assert.Contains(snippet, source, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    ///     Ensures each live model manager accepts a cancellation token and
    ///     uses it in the provider request path so plugin shutdown can cancel
    ///     in-flight discovery.
    /// </summary>
    [Fact]
    public void LiveModelManagers_UseCancellationAwareProviderRequests()
    {
        var root = FindRepositoryRoot();
        var expectedSnippets = new Dictionary<string, string[]>
        {
            [Path.Combine("Translators", "OpenAI", "OpenAIModelManager.cs")] =
            [
                "CancellationToken cancellationToken = default)",
                "using var response = await HttpClient.SendAsync(request, cancellationToken);",
                "string json = await response.Content.ReadAsStringAsync(cancellationToken);",
            ],
            [Path.Combine("Translators", "Claude", "ClaudeModelManager.cs")] =
            [
                "CancellationToken cancellationToken = default)",
                "await HttpClient.SendAsync(",
                "cancellationToken).ConfigureAwait(false);",
                "await response.Content.ReadAsStringAsync(",
            ],
            [Path.Combine("Translators", "DeepSeek", "DeepSeekModelManager.cs")] =
            [
                "CancellationToken cancellationToken = default)",
                "await HttpClient.SendAsync(",
                "cancellationToken).ConfigureAwait(false);",
                "await response.Content.ReadAsStringAsync(",
            ],
            [Path.Combine("Translators", "Gemini", "GeminiModelManager.cs")] =
            [
                "CancellationToken cancellationToken = default)",
                "var response = await HttpClient.SendAsync(request, cancellationToken);",
                "var json = await response.Content.ReadAsStringAsync(cancellationToken);",
            ],
            [Path.Combine("Translators", "LmStudio", "LmStudioModelManager.cs")] =
            [
                "CancellationToken cancellationToken = default)",
                "var response = await client.SendAsync(request, cancellationToken);",
                "var json = await response.Content.ReadAsStringAsync(cancellationToken);",
            ],
            [Path.Combine("Translators", "Ollama", "OllamaModelManager.cs")] =
            [
                "CancellationToken cancellationToken = default)",
                "var response = await client.GetStringAsync(url, cancellationToken);",
            ],
            [Path.Combine("Translators", "OpenRouter", "OpenRouterModelManager.cs")] =
            [
                "CancellationToken cancellationToken = default)",
                "var response = await HttpClient.SendAsync(request, cancellationToken);",
                "var json = await response.Content.ReadAsStringAsync(cancellationToken);",
            ],
        };

        foreach (var (relativePath, snippets) in expectedSnippets)
        {
            var source = File.ReadAllText(Path.Combine(root.FullName, relativePath));

            foreach (var snippet in snippets)
            {
                Assert.Contains(snippet, source, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    ///     Finds the repository root from the current test directory.
    /// </summary>
    /// <returns>The repository root directory.</returns>
    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Echoglossian.sln")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root.");
    }
}
