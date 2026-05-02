// <copyright file="DeepSeekTextModelDefaults.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.OpenAI;

namespace Echoglossian.Translators.DeepSeek;

public static class DeepSeekTextModelDefaults
{
    public static readonly List<LlmTextModel> PredefinedModels = new()
    {
        new LlmTextModel(
            "deepseek-chat",
            "💬 DeepSeek Chat",
            true,
            false,
            true,
            false,
            true,
            "DeepSeek"),
        new LlmTextModel(
            "deepseek-reasoner",
            "🧠 DeepSeek Reasoner",
            true,
            false,
            false,
            false,
            false,
            "DeepSeek"),
    };
}