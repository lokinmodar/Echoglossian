// <copyright file="GenericAddonHandlerHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;
using Echoglossian.Translators;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the shared DB-first payload translation helper used by
///     GameWindow-family addon runtimes.
/// </summary>
public sealed class GenericAddonHandlerHelperTests
{
    /// <summary>
    ///     Ensures one batch response that falls back to the original delimited
    ///     payload does not count as translated coverage and instead falls back
    ///     to per-entry translation.
    /// </summary>
    /// <returns>The asynchronous test task.</returns>
    [Fact]
    public async Task TranslatePayloadAsync_BatchFallbackToOriginal_UsesIndividualEntryFallback()
    {
        var translator = new OriginalBatchFallbackTranslator();
        var translationService = new TranslationService(text => text, translator);

        var translatedPayload = await GenericAddonHandlerHelper.TranslatePayloadAsync(
            new Dictionary<int, string>
            {
                [3] = "Character",
                [4] = "Duty",
            },
            new Dictionary<int, string>(),
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<int, string>
            {
                [3] = "Character",
                [4] = "Duty",
            },
            new Dictionary<int, string>(),
            new Dictionary<string, string>(StringComparer.Ordinal),
            new SourceClientLanguage("en", "en"),
            "pt-BR",
            translationService);

        Assert.True(translatedPayload.HasValue);
        Assert.Equal("Personagem", translatedPayload.Value.AtkValues[3]);
        Assert.Equal("Dever", translatedPayload.Value.AtkValues[4]);
        Assert.Equal(
            [
                "a3|Character|a4|Duty",
                "Character",
                "Duty",
            ],
            translator.Requests);
    }

    private sealed class OriginalBatchFallbackTranslator : ITranslator
    {
        /// <summary>
        ///     Gets every translation request received by the translator.
        /// </summary>
        public List<string> Requests { get; } = [];

        /// <inheritdoc />
        public string Translate(
            string text,
            string sourceLanguage,
            string targetLanguage)
        {
            return this.Resolve(text);
        }

        /// <inheritdoc />
        public Task<string?> TranslateAsync(
            string text,
            string sourceLanguage,
            string targetLanguage)
        {
            return Task.FromResult<string?>(this.Resolve(text));
        }

        private string Resolve(string text)
        {
            this.Requests.Add(text);
            return text switch
            {
                "a3|Character|a4|Duty" => "a3|Character|a4|Duty",
                "Character" => "Personagem",
                "Duty" => "Dever",
                _ => text,
            };
        }
    }
}
