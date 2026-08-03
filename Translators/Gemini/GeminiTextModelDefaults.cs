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
            "gemini-2.5-flash",
            "⚡ Gemini 2.5 Flash",
            true,
            false,
            true,
            true,
            true,
            "Gemini"),
        new LlmTextModel(
            "gemini-2.5-flash-lite",
            "⚪ Gemini 2.5 Flash-Lite",
            true,
            false,
            true,
            true,
            false,
            "Gemini"),
        new LlmTextModel(
            "gemini-2.5-pro",
            "🟢 Gemini 2.5 Pro",
            true,
            false,
            true,
            false,
            false,
            "Gemini"),
    };
}
