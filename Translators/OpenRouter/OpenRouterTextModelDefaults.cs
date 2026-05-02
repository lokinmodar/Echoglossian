// <copyright file="OpenRouterTextModelDefaults.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.OpenAI;

namespace Echoglossian.Translators.OpenRouter;

public static class OpenRouterTextModelDefaults
{
    public static readonly List<LlmTextModel> PredefinedModels = new()
    {
        new LlmTextModel(
            "mistral",
            "🛰 Mistral (default)",
            true,
            false,
            false,
            false,
            true,
            "OpenRouter"),
        new LlmTextModel(
            "openchat/openchat-3.5",
            "🛰 OpenChat 3.5",
            true,
            false,
            true,
            false,
            false,
            "OpenRouter"),
        new LlmTextModel(
            "gryphe/mythomax-l2-13b",
            "🛰 Mythomax L2 13B",
            true,
            false,
            false,
            false,
            false,
            "OpenRouter"),
        new LlmTextModel(
            "meta-llama/llama-3-70b-instruct",
            "🛰 LLaMA 3 70B",
            true,
            false,
            false,
            false,
            false,
            "OpenRouter"),
        new LlmTextModel(
            "google/gemini-pro",
            "🛰 Gemini Pro via OpenRouter",
            true,
            false,
            true,
            false,
            false,
            "OpenRouter"),
    };
}