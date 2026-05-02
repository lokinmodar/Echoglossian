// <copyright file="OllamaTextModelDefaults.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.OpenAI;

namespace Echoglossian.Translators.Ollama;

public static class OllamaTextModelDefaults
{
    public static readonly List<LlmTextModel> PredefinedModels = new()
    {
        new LlmTextModel(
            "llama3",
            "🦙 llama3",
            true,
            false,
            false,
            false,
            true,
            "Ollama"),
        new LlmTextModel(
            "mistral",
            "🦙 mistral",
            true,
            false,
            false,
            false,
            false,
            "Ollama"),
        new LlmTextModel(
            "gemma",
            "🦙 gemma",
            true,
            false,
            false,
            false,
            false,
            "Ollama"),
        new LlmTextModel(
            "phi3",
            "🦙 phi3",
            true,
            false,
            false,
            false,
            false,
            "Ollama"),
    };
}