// <copyright file="LmStudioTextModelDefaults.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.OpenAI;

namespace Echoglossian.Translators.LmStudio;

/// <summary>
///     Provides default model list for LM Studio usage.
/// </summary>
public static class LmStudioTextModelDefaults
{
    /// <summary>
    ///     Predefined LM Studio-compatible models for offline use.
    /// </summary>
    public static readonly List<LlmTextModel> PredefinedModels = new()
    {
        new LlmTextModel(
            "lmstudio/llama3",
            "🦙 LLaMA 3",
            true,
            false,
            false,
            false,
            true,
            "LmStudio"),
    };
}