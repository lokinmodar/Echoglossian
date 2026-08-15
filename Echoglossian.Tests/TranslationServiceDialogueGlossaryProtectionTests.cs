// <copyright file="TranslationServiceDialogueGlossaryProtectionTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators;

using FluentAssertions;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers deterministic dialogue glossary enforcement in the shared
///     translation-service pipeline.
/// </summary>
public class TranslationServiceDialogueGlossaryProtectionTests
{
    /// <summary>
    ///     Ensures dialogue LLM requests protect glossary terms before the
    ///     provider call and restore the configured targets after the provider
    ///     returns the marker-bearing payload unchanged.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task TranslateAsync_DialogueGlossaryTerms_RestoreConfiguredTargets()
    {
        var glossaryPath = CreateGlossaryFile(
            """
            [
              {
                "source_text": "Triple Triad",
                "target_text": "Triple Triad [GLOSSARY-OK]"
              }
            ]
            """);

        try
        {
            StructuredDialogueGlossaryStore.Clear();
            StructuredDialogueGlossaryStore.Refresh(glossaryPath).Should().BeTrue();

            var translator = new EchoDialogueTranslator();
            var service = new TranslationService(
                text => text,
                translator,
                translationEngine: 8);
            var context = new DialogueTranslationContext(
                "Talk",
                "gold-saucer",
                "Roland",
                []);

            var result = await service.TranslateAsync(
                "Triple Triad is popular here.",
                new SourceClientLanguage("en", "en"),
                "pt-BR",
                context,
                TranslationSurfaceGroup.Dialogue);

            result.Should().Be("Triple Triad [GLOSSARY-OK] is popular here.");
            translator.ContextAwareInputs.Should().ContainSingle();
            translator.ContextAwareInputs[0].Should().NotContain("Triple Triad");
        }
        finally
        {
            StructuredDialogueGlossaryStore.Clear();
            File.Delete(glossaryPath);
        }
    }

    /// <summary>
    ///     Ensures a provider response that damages a required glossary marker
    ///     follows the existing sanitized-source fallback path.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task TranslateAsync_DialogueGlossaryMarkerDamage_FallsBackToSanitizedSource()
    {
        var glossaryPath = CreateGlossaryFile(
            """
            [
              {
                "source_text": "Triple Triad",
                "target_text": "Triple Triad [GLOSSARY-OK]"
              }
            ]
            """);

        try
        {
            StructuredDialogueGlossaryStore.Clear();
            StructuredDialogueGlossaryStore.Refresh(glossaryPath).Should().BeTrue();

            var failureReasons = new List<string>();
            var translator = new EchoDialogueTranslator
            {
                ResponseFactory = _ => "The provider ignored the required marker.",
            };
            var service = new TranslationService(
                text => text,
                translator,
                translationEngine: 8,
                recordTransientFailedTranslation: (text, source, target, engine, reason, ttl) =>
                    failureReasons.Add(reason));
            var context = new DialogueTranslationContext(
                "Talk",
                "gold-saucer",
                "Roland",
                []);

            var result = await service.TranslateAsync(
                "Triple Triad is popular here.",
                new SourceClientLanguage("en", "en"),
                "pt-BR",
                context,
                TranslationSurfaceGroup.Dialogue);

            result.Should().Be("Triple Triad is popular here.");
            failureReasons.Should().ContainSingle("missing-required-marker");
        }
        finally
        {
            StructuredDialogueGlossaryStore.Clear();
            File.Delete(glossaryPath);
        }
    }

    /// <summary>
    ///     Writes one temporary structured dialogue glossary file for one test.
    /// </summary>
    /// <param name="json">The JSON payload to write.</param>
    /// <returns>The temporary glossary file path.</returns>
    private static string CreateGlossaryFile(string json)
    {
        var filePath = Path.Combine(
            Path.GetTempPath(),
            $"{nameof(TranslationServiceDialogueGlossaryProtectionTests)}-{Guid.NewGuid():N}.json");
        File.WriteAllText(filePath, json);
        return filePath;
    }

    /// <summary>
    ///     Minimal dialogue-aware translator used to observe the protected text
    ///     that reaches the provider boundary.
    /// </summary>
    private sealed class EchoDialogueTranslator : ITranslator, IDialogueContextAwareTranslator
    {
        /// <summary>
        ///     Gets the protected dialogue inputs received by the context-aware
        ///     translation path.
        /// </summary>
        public List<string> ContextAwareInputs { get; } = [];

        /// <summary>
        ///     Gets or sets the response factory used to emulate provider output.
        /// </summary>
        public Func<string, string> ResponseFactory { get; set; } = text => text;

        /// <inheritdoc />
        public string? Translate(string text, string sourceLanguage, string targetLanguage)
        {
            return text;
        }

        /// <inheritdoc />
        public Task<string?> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
        {
            return Task.FromResult<string?>(text);
        }

        /// <inheritdoc />
        public Task<string?> TranslateAsync(
            string text,
            string sourceLanguage,
            string targetLanguage,
            DialogueTranslationContext dialogueContext)
        {
            this.ContextAwareInputs.Add(text);
            return Task.FromResult<string?>(this.ResponseFactory(text));
        }
    }
}
