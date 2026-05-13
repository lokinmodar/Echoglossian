// <copyright file="OllamaTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Net.Http.Json;
using Echoglossian.PluginUI.Helpers;
using Echoglossian.Translators.Helpers;

namespace Echoglossian.Translators;

public class OllamaTranslator : ITranslator, IDialogueContextAwareTranslator
{
    private readonly string endpoint;
    private readonly HttpClient httpClient;
    private readonly string model;
    private readonly IPluginLog pluginLog;
    private readonly string promptTemplate;
    private readonly float temperature;
    private readonly ConcurrentTranslationRequestCache translationCache = new();

    public OllamaTranslator(IPluginLog pluginLog, Config config)
    {
        this.pluginLog = pluginLog;
        this.endpoint =
            config.OllamaUrl?.TrimEnd('/') ?? "http://localhost:11434";
        this.model = config.OllamaModel ?? "llama3";
        this.promptTemplate = string.IsNullOrWhiteSpace(config.OllamaPrompt)
            ? PromptTemplateManager.GetDefaultPrompt(Echoglossian.PromptType.Ollama)
            : config.OllamaPrompt;
        this.temperature = config.OllamaTemperature;

        this.httpClient = new HttpClient
        {
            BaseAddress = new Uri(this.endpoint),
        };
        this.httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public string Translate(
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        return this.TranslateAsync(text, sourceLanguage, targetLanguage)
            .GetAwaiter().GetResult() ?? string.Empty;
    }

    public async Task<string?> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        var cacheKey = $"{text}_{sourceLanguage}_{targetLanguage}";
        if (this.translationCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        return await this.translationCache.GetOrAddAsync(
            cacheKey,
            () => this.TranslateCoreAsync(
                text,
                sourceLanguage,
                targetLanguage,
                cacheKey)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string?> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        DialogueTranslationContext dialogueContext)
    {
        if (!HasUsableDialogueContext(dialogueContext))
        {
            return await this.TranslateAsync(
                text,
                sourceLanguage,
                targetLanguage).ConfigureAwait(false);
        }

        var cacheKey = this.BuildDialogueContextCacheKey(
            text,
            sourceLanguage,
            targetLanguage,
            dialogueContext);
        if (this.translationCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        return await this.translationCache.GetOrAddAsync(
            cacheKey,
            () => this.TranslateCoreAsync(
                text,
                sourceLanguage,
                targetLanguage,
                cacheKey,
                dialogueContext)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Performs the actual Ollama translation request for one cache key.
    /// </summary>
    /// <param name="text">The text to translate.</param>
    /// <param name="sourceLanguage">The source language of the text.</param>
    /// <param name="targetLanguage">The target language for the translation.</param>
    /// <param name="cacheKey">The normalized cache key for this request.</param>
    /// <returns>The translated text or an error placeholder.</returns>
    private async Task<string?> TranslateCoreAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        string cacheKey,
        DialogueTranslationContext? dialogueContext = null)
    {
        var prompt = this.BuildPrompt(
            text,
            sourceLanguage,
            targetLanguage,
            dialogueContext);

        var request = new
        {
            this.model,
            prompt,
            this.temperature,
            stream = false,
        };

        try
        {
            var response =
                await this.httpClient.PostAsJsonAsync("/api/generate", request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var parsed = JObject.Parse(json);
            var output = parsed["response"]?.ToString()?.Trim();

            if (!string.IsNullOrWhiteSpace(output))
            {
                var cleaned = FixText(output.Trim('"'));
                if (TranslationResultGuard.IsPersistableTranslation(cleaned))
                {
                    this.translationCache.Remember(cacheKey, cleaned);
                }

                return cleaned;
            }

            PluginRuntimeLog.Warning(this.pluginLog, "OllamaTranslator: No output returned.");
            return
                $"[{Resources.TranslationError} No translation received from Ollama]";
        }
        catch (Exception ex)
        {
            PluginRuntimeLog.Error(this.pluginLog, $"OllamaTranslator failed: {ex.Message}");
            return $"[{Resources.TranslationError} Ollama error: {ex.Message}]";
        }
    }

    private static bool HasUsableDialogueContext(DialogueTranslationContext dialogueContext)
    {
        return dialogueContext.PriorTurns.Count > 0;
    }

    private string BuildPrompt(
        string text,
        string sourceLanguage,
        string targetLanguage,
        DialogueTranslationContext? dialogueContext = null)
    {
        var fixedText = FixText(text);
        var prompt = this.promptTemplate
            .Replace("{text}", fixedText)
            .Replace("{sourceLanguage}", sourceLanguage)
            .Replace("{targetLanguage}", targetLanguage);

        if (!dialogueContext.HasValue ||
            !HasUsableDialogueContext(dialogueContext.Value))
        {
            return prompt;
        }

        var priorTurns = string.Join(
            Environment.NewLine,
            dialogueContext.Value.PriorTurns.Select(
                (turn, index) =>
                    $"[{index + 1}] {turn.SpeakerName}: {FixText(turn.SourceText)}"));

        return
            $"{prompt}{Environment.NewLine}{Environment.NewLine}Previous dialogue context for translation consistency only (translate only the current text, not the history):{Environment.NewLine}Current speaker: {dialogueContext.Value.SpeakerName}{Environment.NewLine}{priorTurns}";
    }

    private string BuildDialogueContextCacheKey(
        string text,
        string sourceLanguage,
        string targetLanguage,
        DialogueTranslationContext dialogueContext)
    {
        var historyKey = string.Join(
            "|",
            dialogueContext.PriorTurns.Select(
                turn => $"{turn.SpeakerName}:{turn.SourceText}"));

        return
            $"dialogue|{dialogueContext.SessionNamespace}|{dialogueContext.SessionKey}|{historyKey}|{text}_{sourceLanguage}_{targetLanguage}";
    }
}
