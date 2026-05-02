// <copyright file="AllTextModelRegistry.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.DeepSeek;
using Echoglossian.Translators.Gemini;
using Echoglossian.Translators.Ollama;
using Echoglossian.Translators.OpenAI;

namespace Echoglossian.Translators;

public static class AllTextModelRegistry
{
    public static readonly List<LlmTextModel> AllModels =
        new List<LlmTextModel>()
            .Concat(OpenAITextModelDefaults.PredefinedModels)
            .Concat(GeminiTextModelDefaults.PredefinedModels)
            .Concat(DeepSeekTextModelDefaults.PredefinedModels)
            .Concat(OllamaTextModelDefaults.PredefinedModels).ToList();

    public static IReadOnlyList<LlmTextModel> ByEngine(string engineName)
    {
        return AllModels.Where(m => m.EngineName.Equals(
            engineName,
            StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public static LlmTextModel? GetById(string id)
    {
        return AllModels.FirstOrDefault(m => m.Id == id);
    }
}