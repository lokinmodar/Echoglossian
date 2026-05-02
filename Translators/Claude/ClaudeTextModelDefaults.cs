// <copyright file="ClaudeTextModelDefaults.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.OpenAI;

namespace Echoglossian.Translators.Claude;

/// <summary>
///     Provides the committed fallback Claude model list used when live fetch is disabled or unavailable.
/// </summary>
public static class ClaudeTextModelDefaults
{
    /// <summary>
    ///     Gets the predefined Claude models.
    /// </summary>
    public static readonly List<LlmTextModel> PredefinedModels = new()
    {
        new LlmTextModel(
            "claude-sonnet-4-20250514",
            "🟢 Claude Sonnet 4 (2025-05-14)",
            true,
            false,
            true,
            false,
            true,
            "Claude"),
        new LlmTextModel(
            "claude-3-7-sonnet-latest",
            "🟢 Claude 3.7 Sonnet (Latest)",
            true,
            false,
            true,
            false,
            false,
            "Claude"),
        new LlmTextModel(
            "claude-3-5-haiku-latest",
            "⚡ Claude 3.5 Haiku (Latest)",
            true,
            false,
            true,
            true,
            false,
            "Claude"),
        new LlmTextModel(
            "claude-opus-4-1-20250805",
            "🧠 Claude Opus 4.1 (2025-08-05)",
            true,
            false,
            false,
            false,
            false,
            "Claude"),
    };
}
