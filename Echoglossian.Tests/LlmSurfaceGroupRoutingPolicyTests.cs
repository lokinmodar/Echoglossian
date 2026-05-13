// <copyright file="LlmSurfaceGroupRoutingPolicyTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the first-pass LLM-only surface-group routing policy.
/// </summary>
public class LlmSurfaceGroupRoutingPolicyTests
{
  /// <summary>
  ///     Ensures dialogue-family requests can route to a configured LLM
  ///     override while the global engine remains the default path.
  /// </summary>
  [Fact]
  public void ResolveEngine_DialogueSurface_UsesConfiguredLlmOverride()
  {
    var config = new Config
    {
      ChosenTransEngine = (int)Echoglossian.TransEngines.Google,
      ChosenTransEngineKey = nameof(Echoglossian.TransEngines.Google),
      UseDialogueLlmOverride = true,
      DialogueLlmEngine = (int)Echoglossian.TransEngines.Ollama,
      DialogueLlmEngineKey = nameof(Echoglossian.TransEngines.Ollama),
      OllamaUrl = "http://localhost:11434",
      OllamaModel = "llama3",
    };

    var resolved = LlmSurfaceGroupRoutingPolicy.ResolveEngine(
        config,
        TranslationSurfaceGroup.Dialogue);

    Assert.Equal(Echoglossian.TransEngines.Ollama, resolved);
  }

  /// <summary>
  ///     Ensures non-LLM override selections are ignored so the first-pass
  ///     routing remains LLM-only.
  /// </summary>
  [Fact]
  public void ResolveEngine_DialogueSurface_IgnoresNonLlmOverride()
  {
    var config = new Config
    {
      ChosenTransEngine = (int)Echoglossian.TransEngines.Google,
      ChosenTransEngineKey = nameof(Echoglossian.TransEngines.Google),
      UseDialogueLlmOverride = true,
      DialogueLlmEngine = (int)Echoglossian.TransEngines.Microsoft,
      DialogueLlmEngineKey = nameof(Echoglossian.TransEngines.Microsoft),
      MicrosoftTranslatorApiKey = "test-key",
      MicrosoftTranslatorRegion = "brazilsouth",
      MicrosoftTranslatorEndpoint = "https://api.cognitive.microsofttranslator.com",
    };

    var resolved = LlmSurfaceGroupRoutingPolicy.ResolveEngine(
        config,
        TranslationSurfaceGroup.Dialogue);

    Assert.Equal(Echoglossian.TransEngines.Google, resolved);
  }

  /// <summary>
  ///     Ensures dialogue-family requests fall back to the global engine when
  ///     the override engine is not configured enough to be used safely.
  /// </summary>
  [Fact]
  public void ResolveEngine_DialogueSurface_FallsBackWhenOverrideIsIncomplete()
  {
    var config = new Config
    {
      ChosenTransEngine = (int)Echoglossian.TransEngines.Google,
      ChosenTransEngineKey = nameof(Echoglossian.TransEngines.Google),
      UseDialogueLlmOverride = true,
      DialogueLlmEngine = (int)Echoglossian.TransEngines.ChatGPT,
      DialogueLlmEngineKey = nameof(Echoglossian.TransEngines.ChatGPT),
      ChatGptApiKey = string.Empty,
    };

    var resolved = LlmSurfaceGroupRoutingPolicy.ResolveEngine(
        config,
        TranslationSurfaceGroup.Dialogue);

    Assert.Equal(Echoglossian.TransEngines.Google, resolved);
  }
}
