// <copyright file="OpenAITextModelDefaults.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators.OpenAI;

public static class OpenAITextModelDefaults
{
  public static readonly List<LlmTextModel> PredefinedModels = new()
  {
    new("gpt-4-0613", "🧠 GPT-4 (0613)", true, false, false, false, false, "OpenAI"),
    new("gpt-4", "🧠 GPT-4", true, false, false, false, false, "OpenAI"),
    new("gpt-3.5-turbo", "⚡ GPT-3.5 Turbo", true, false, true, false, false, "OpenAI"),
    new("gpt-4.1-mini-2025-04-14", "🔹 GPT-4.1 Mini (2025-04-14)", true, false, false, true, false, "OpenAI"),
    new("gpt-4.1-mini", "🔹 GPT-4.1 Mini", true, false, false, true, false, "OpenAI"),
    new("gpt-4.1-nano-2025-04-14", "🔹 GPT-4.1 Nano (2025-04-14)", true, false, false, true, false, "OpenAI"),
    new("gpt-4.1-nano", "🔹 GPT-4.1 Nano", true, false, false, true, false, "OpenAI"),
    new("gpt-3.5-turbo-instruct", "💬 GPT-3.5 Instruct", true, false, false, false, false, "OpenAI"),
    new("gpt-3.5-turbo-instruct-0914", "💬 GPT-3.5 Instruct (0914)", true, false, false, false, false, "OpenAI"),
    new("gpt-4-1106-preview", "🧠 GPT-4 (1106 Preview)", true, false, true, false, false, "OpenAI"),
    new("gpt-3.5-turbo-1106", "⚡ GPT-3.5 Turbo (1106)", true, false, true, false, false, "OpenAI"),
    new("gpt-4-0125-preview", "🧠 GPT-4 (0125 Preview)", true, false, true, false, false, "OpenAI"),
    new("gpt-4-turbo-preview", "🟢 GPT-4 Turbo Preview", true, false, true, false, false, "OpenAI"),
    new("gpt-3.5-turbo-0125", "⚡ GPT-3.5 Turbo (0125)", true, false, true, false, false, "OpenAI"),
    new("gpt-4-turbo", "🟢 GPT-4 Turbo", true, false, true, false, false, "OpenAI"),
    new("gpt-4-turbo-2024-04-09", "🟢 GPT-4 Turbo (2024-04-09)", true, false, true, false, false, "OpenAI"),
    new("gpt-4o", "👁 GPT-4o", true, true, true, false, false, "OpenAI"),
    new("gpt-4o-2024-05-13", "👁 GPT-4o (2024-05-13)", true, true, true, false, false, "OpenAI"),
    new("gpt-4o-mini-2024-07-18", "⚡ GPT-4o Mini (2024-07-18)", true, true, true, true, false, "OpenAI"),
    new("gpt-4o-mini", "⚡ GPT-4o Mini", true, true, true, true, true, "OpenAI"),
    new("gpt-4o-2024-08-06", "👁 GPT-4o (2024-08-06)", true, true, true, false, false, "OpenAI"),
    new("chatgpt-4o-latest", "👁 ChatGPT-4o Latest", true, true, true, false, false, "OpenAI"),
    new("o1-preview-2024-09-12", "🔷 O1 Preview (2024-09-12)", true, false, true, false, false, "OpenAI"),
    new("o1-preview", "🔷 O1 Preview", true, false, true, false, false, "OpenAI"),
    new("o1-mini-2024-09-12", "🔹 O1 Mini (2024-09-12)", true, false, true, true, false, "OpenAI"),
    new("o1-mini", "🔹 O1 Mini", true, false, true, true, false, "OpenAI"),
    new("gpt-4o-realtime-preview-2024-10-01", "👁 GPT-4o Realtime (2024-10-01)", true, true, true, false, false, "OpenAI"),
    new("gpt-4o-realtime-preview", "👁 GPT-4o Realtime", true, true, true, false, false, "OpenAI"),
    new("gpt-4o-2024-11-20", "👁 GPT-4o (2024-11-20)", true, true, true, false, false, "OpenAI"),
    new("gpt-4.5-preview", "🧠 GPT-4.5 Preview", true, false, true, false, false, "OpenAI"),
    new("gpt-4.5-preview-2025-02-27", "🧠 GPT-4.5 Preview (2025-02-27)", true, false, true, false, false, "OpenAI"),
    new("gpt-4.1-2025-04-14", "🧠 GPT-4.1 (2025-04-14)", true, false, false, false, false, "OpenAI"),
    new("gpt-4.1", "🧠 GPT-4.1", true, false, false, false, false, "OpenAI"),
    new("gpt-3.5-turbo-16k", "⚡ GPT-3.5 Turbo 16k", true, false, true, false, false, "OpenAI"),
  };
}
