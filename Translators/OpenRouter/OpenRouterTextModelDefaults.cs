// <copyright file="OpenRouterTextModelDefaults.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.OpenAI;

namespace Echoglossian.Translators.OpenRouter;

public static class OpenRouterTextModelDefaults
{
  public static readonly List<OpenAITextModel> PredefinedModels = new()
  {
    new(
      Id: "mistral",
      DisplayName: "🛰 Mistral (default)",
      SupportsText: true,
      SupportsVision: false,
      IsTurbo: false,
      IsMini: false,
      IsDefault: true,
      EngineName: "OpenRouter"
    ),
    new(
      Id: "openchat/openchat-3.5",
      DisplayName: "🛰 OpenChat 3.5",
      SupportsText: true,
      SupportsVision: false,
      IsTurbo: true,
      IsMini: false,
      IsDefault: false,
      EngineName: "OpenRouter"
    ),
    new(
      Id: "gryphe/mythomax-l2-13b",
      DisplayName: "🛰 Mythomax L2 13B",
      SupportsText: true,
      SupportsVision: false,
      IsTurbo: false,
      IsMini: false,
      IsDefault: false,
      EngineName: "OpenRouter"
    ),
    new(
      Id: "meta-llama/llama-3-70b-instruct",
      DisplayName: "🛰 LLaMA 3 70B",
      SupportsText: true,
      SupportsVision: false,
      IsTurbo: false,
      IsMini: false,
      IsDefault: false,
      EngineName: "OpenRouter"
    ),
    new(
      Id: "google/gemini-pro",
      DisplayName: "🛰 Gemini Pro via OpenRouter",
      SupportsText: true,
      SupportsVision: false,
      IsTurbo: true,
      IsMini: false,
      IsDefault: false,
      EngineName: "OpenRouter"
    ),
  };
}
