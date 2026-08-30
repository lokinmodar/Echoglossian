// <copyright file="GTranslateTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using GTranslate;
using GTranslate.Translators;

namespace Echoglossian.Translators;

/// <summary>
///     Provides translation services using GTranslate.
/// </summary>
public class GTranslateTranslator : ITranslator
{
    private readonly IPluginLog pluginLog;
    private readonly AggregateTranslator translator;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GTranslateTranslator" />
    ///     class.
    /// </summary>
    /// <param name="pluginLog">The plugin log.</param>
    /// <param name="config">The configuration settings.</param>
    public GTranslateTranslator(IPluginLog pluginLog, Config config)
    {
        this.pluginLog = pluginLog;
        this.translator =
            new AggregateTranslator(); // Switch to GoogleTranslator() if you want to force only Google
    }

    /// <summary>
    /// Resolves the requested target language using the runtime language
    /// normalization contract shared across translation services.
    /// </summary>
    /// <param name="requestedTargetLanguage">The requested target language code.</param>
    /// <returns>The resolved GTranslate language metadata.</returns>
    internal static Language ResolveRequestedTargetLanguage(
        string requestedTargetLanguage)
    {
        var normalizedTargetLanguage =
            RuntimeLanguageHelper.NormalizeLanguage(requestedTargetLanguage);
        if (string.IsNullOrWhiteSpace(normalizedTargetLanguage))
        {
            throw new ArgumentException(
                "A target language is required.",
                nameof(requestedTargetLanguage));
        }

        return Language.GetLanguage(normalizedTargetLanguage);
    }

    /// <summary>
    ///     Translates the given text from the source language to the target language
    ///     synchronously.
    /// </summary>
    /// <param name="text">The text to translate.</param>
    /// <param name="sourceLanguage">The language of the input text.</param>
    /// <param name="targetLanguage">The language to translate the text into.</param>
    /// <returns>A translated string.</returns>
    public string Translate(
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        PluginRuntimeLog.Debug(this.pluginLog, "GTranslate sync translate requested.");

        return this.TranslateAsync(text, sourceLanguage, targetLanguage).Result ?? string.Empty;
    }

    /// <summary>
    ///     Translates the given text from the source language to the target language
    ///     asynchronously.
    /// </summary>
    /// <param name="text">The text to translate.</param>
    /// <param name="sourceLanguage">The language of the input text.</param>
    /// <param name="targetLanguage">The language to translate the text into.</param>
    /// <returns>
    ///     A <see cref="Task" /> representing the asynchronous operation, with a
    ///     translated string as the result.
    /// </returns>
    public async Task<string?> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var fixedText = FixText(text);
        PluginRuntimeLog.Debug(this.pluginLog, $"GTranslate input: {fixedText}");

        PluginRuntimeLog.Debug(this.pluginLog, $"GTranslate source language: {sourceLanguage}");

        try
        {
            var resolvedTargetLanguage =
                ResolveRequestedTargetLanguage(targetLanguage);
            PluginRuntimeLog.Debug(
                this.pluginLog,
                $"GTranslate target language: {resolvedTargetLanguage}");

            var result = await this.translator.TranslateAsync(
                fixedText,
                resolvedTargetLanguage.Name,
                sourceLanguage);
            var cleaned = FixText(result.Translation);
            PluginRuntimeLog.Debug(this.pluginLog, $"GTranslate result: {cleaned}");
            return cleaned;
        }
        catch (Exception ex)
        {
            PluginRuntimeLog.Error(this.pluginLog, $"GTranslate error: {ex}");
            return string.Empty;
        }
    }
}
