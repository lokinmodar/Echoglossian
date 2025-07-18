// <copyright file="GeminiTextModelDefaults.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.OpenAI;

namespace Echoglossian.Translators.Gemini;

public static class GeminiTextModelDefaults
{
    public static readonly List<OpenAITextModel> PredefinedModels = new()
    {
        new OpenAITextModel(
            "gemini-pro",
            "🔷 Gemini Pro",
            true,
            false,
            true,
            false,
            true,
            "Gemini"),
        new OpenAITextModel(
            "gemini-1.5-pro",
            "🟢 Gemini 1.5 Pro",
            true,
            false,
            true,
            false,
            false,
            "Gemini"),
        new OpenAITextModel(
            "gemini-1.5-flash",
            "⚡ Gemini 1.5 Flash",
            true,
            false,
            true,
            true,
            false,
            "Gemini"),
    };
}