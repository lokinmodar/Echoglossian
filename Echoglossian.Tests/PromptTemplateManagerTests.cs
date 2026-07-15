// <copyright file="PromptTemplateManagerTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Helpers;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers engine-specific default prompt selection.
/// </summary>
public class PromptTemplateManagerTests
{
  /// <summary>
  ///     Ensures local LLM engines use compact defaults instead of the shared
  ///     cloud-LLM prompt when no custom prompt is configured.
  /// </summary>
  [Theory]
  [InlineData(Echoglossian.PromptType.Ollama, PromptTemplateManager.OllamaDefaultPrompt)]
  [InlineData(Echoglossian.PromptType.LmStudio, PromptTemplateManager.LmStudioDefaultPrompt)]
  public void GetPromptOrDefault_LocalLlmPromptType_ReturnsCompactDefault(
      Echoglossian.PromptType promptType,
      string expectedPrompt)
  {
    var manager = new PromptTemplateManager(new Config());

    var prompt = manager.GetPromptOrDefault(promptType);

    Assert.Equal(expectedPrompt, prompt);
    Assert.Contains("{text}", prompt, StringComparison.Ordinal);
    Assert.Contains("{sourceLanguage}", prompt, StringComparison.Ordinal);
    Assert.Contains("{targetLanguage}", prompt, StringComparison.Ordinal);
    Assert.NotEqual(PromptTemplateManager.DefaultPrompt, prompt);
  }

  /// <summary>
  ///     Ensures non-local LLM prompt types keep using the shared default
  ///     prompt when no custom prompt is configured.
  /// </summary>
  [Fact]
  public void GetPromptOrDefault_ChatGptPromptType_ReturnsSharedDefault()
  {
    var manager = new PromptTemplateManager(new Config());

    var prompt = manager.GetPromptOrDefault(Echoglossian.PromptType.ChatGPT);

    Assert.Equal(PromptTemplateManager.DefaultPrompt, prompt);
  }

  /// <summary>
  ///     Ensures prompt rendering applies the standard placeholders used by
  ///     the LLM translator family.
  /// </summary>
  [Fact]
  public void RenderPrompt_ReplacesAllStandardPlaceholders()
  {
    var rendered = PromptTemplateManager.RenderPrompt(
        "Translate {text} from {sourceLanguage} to {targetLanguage}.",
        "Pray return",
        "English",
        "Portuguese (Brazil)");

    Assert.Equal(
        "Translate Pray return from English to Portuguese (Brazil).",
        rendered);
  }
}
