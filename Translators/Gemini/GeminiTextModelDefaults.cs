// <copyright file="GeminiTextModelDefaults.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.OpenAI;

namespace Echoglossian.Translators.Gemini;

public static class GeminiTextModelDefaults
{
    public static readonly List<LlmTextModel> PredefinedModels = new()
    {
        new LlmTextModel(
            "gemini-pro",
            "🔷 Gemini Pro",
            true,
            false,
            true,
            false,
            true,
            "Gemini"),
        new LlmTextModel(
            "gemini-1.5-pro",
            "🟢 Gemini 1.5 Pro",
            true,
            false,
            true,
            false,
            false,
            "Gemini"),
        new LlmTextModel(
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