// <copyright file="TranslationFieldBatchTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators;
using Echoglossian.Translators.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Verifies engine-neutral structured field translation batching.
/// </summary>
public sealed class TranslationFieldBatchTests
{
    /// <summary>
    ///     Ensures a valid field envelope uses one translator invocation.
    /// </summary>
    /// <returns>The asynchronous test task.</returns>
    [Fact]
    public async Task TranslateFieldsAsync_ValidEnvelope_UsesOneCall()
    {
        var translator = new EnvelopeTranslator(static fields =>
            TranslationFieldEnvelopeCodec.Encode(
                [
                    new TranslationField("Name", "Acoes"),
                    new TranslationField("Description", "Abre a janela."),
                ]));
        var service = this.CreateService(translator);

        var result = await service.TranslateFieldsAsync(
            [
                new TranslationField("Name", "Actions"),
                new TranslationField("Description", "Opens the window."),
            ],
            new SourceClientLanguage("en", "en"),
            "pt-BR");

        Assert.False(result.UsedIndividualFallback);
        Assert.Equal("Acoes", result.GetTranslation("Name"));
        Assert.Equal("Abre a janela.", result.GetTranslation("Description"));
        Assert.Equal(1, translator.CallCount);
    }

    /// <summary>
    ///     Ensures delimiter-bearing field values survive envelope transport.
    /// </summary>
    /// <returns>The asynchronous test task.</returns>
    [Fact]
    public async Task TranslateFieldsAsync_DelimiterBearingValues_RoundTripsSafely()
    {
        const string name = "Action|Trait\\Combo";
        const string description = "First line\nSecond\tline|tail";
        var translator = new EnvelopeTranslator(static fields =>
            TranslationFieldEnvelopeCodec.Encode(
                fields.Select(field => new TranslationField(
                    field.Name,
                    $"pt:{field.Text}"))));
        var service = this.CreateService(translator);

        var result = await service.TranslateFieldsAsync(
            [
                new TranslationField("Name", name),
                new TranslationField("Description", description),
            ],
            new SourceClientLanguage("en", "en"),
            "pt-BR");

        Assert.False(result.UsedIndividualFallback);
        Assert.Equal($"pt:{name}", result.GetTranslation("Name"));
        Assert.Equal($"pt:{description}", result.GetTranslation("Description"));
        Assert.Equal(1, translator.CallCount);
    }

    /// <summary>
    ///     Ensures human-readable field names are not exposed to translator
    ///     engines as mutable envelope keys.
    /// </summary>
    /// <returns>The asynchronous test task.</returns>
    [Fact]
    public async Task TranslateFieldsAsync_EnvelopeUsesOpaqueFieldIdentifiers()
    {
        var translator = new EnvelopeTranslator(static fields =>
            TranslationFieldEnvelopeCodec.Encode(
                fields.Select(field => new TranslationField(
                    field.Name,
                    $"pt:{field.Text}"))));
        var service = this.CreateService(translator);

        _ = await service.TranslateFieldsAsync(
            [
                new TranslationField("Name", "Actions"),
                new TranslationField("Description", "Opens the window."),
            ],
            new SourceClientLanguage("en", "en"),
            "pt-BR");

        Assert.Single(translator.Requests);
        Assert.DoesNotContain("\nName\t", translator.Requests[0], StringComparison.Ordinal);
        Assert.DoesNotContain("\nDescription\t", translator.Requests[0], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures reordered field responses cannot be applied to the wrong
    ///     field and instead use individual translations.
    /// </summary>
    /// <returns>The asynchronous test task.</returns>
    [Fact]
    public async Task TranslateFieldsAsync_ReorderedEnvelope_FallsBackIndividually()
    {
        var translator = new EnvelopeTranslator(
            static fields => TranslationFieldEnvelopeCodec.Encode(
                fields.Reverse().Select(field => new TranslationField(
                    field.Name,
                    $"batch:{field.Text}"))),
            static text => $"single:{text}");
        var service = this.CreateService(translator);

        var result = await service.TranslateFieldsAsync(
            [
                new TranslationField("Name", "Actions"),
                new TranslationField("Description", "Opens the window."),
            ],
            new SourceClientLanguage("en", "en"),
            "pt-BR");

        Assert.True(result.UsedIndividualFallback);
        Assert.Equal("single:Actions", result.GetTranslation("Name"));
        Assert.Equal("single:Opens the window.", result.GetTranslation("Description"));
        Assert.Equal(3, translator.CallCount);
    }

    /// <summary>
    ///     Ensures malformed or incomplete responses require every requested
    ///     field before accepting a batch result.
    /// </summary>
    /// <returns>The asynchronous test task.</returns>
    [Fact]
    public async Task TranslateFieldsAsync_IncompleteEnvelope_FallsBackIndividually()
    {
        var translator = new EnvelopeTranslator(
            static _ => TranslationFieldEnvelopeCodec.Encode(
                [new TranslationField("Name", "batch:Acoes")]),
            static text => $"single:{text}");
        var service = this.CreateService(translator);

        var result = await service.TranslateFieldsAsync(
            [
                new TranslationField("Name", "Actions"),
                new TranslationField("Description", "Opens the window."),
            ],
            new SourceClientLanguage("en", "en"),
            "pt-BR");

        Assert.True(result.UsedIndividualFallback);
        Assert.Equal("single:Actions", result.GetTranslation("Name"));
        Assert.Equal("single:Opens the window.", result.GetTranslation("Description"));
        Assert.Equal(3, translator.CallCount);
    }

    /// <summary>
    ///     Ensures a malformed response falls back through the translator
    ///     resolution captured for the batch rather than resolving again.
    /// </summary>
    /// <returns>The asynchronous test task.</returns>
    [Fact]
    public async Task TranslateFieldsAsync_MalformedEnvelope_UsesCapturedTranslatorForFallback()
    {
        var capturedTranslator = new EnvelopeTranslator(
            static _ => "not a field envelope",
            static text => $"captured:{text}");
        var replacementTranslator = new EnvelopeTranslator(
            static _ => throw new InvalidOperationException(),
            static text => $"replacement:{text}");
        var resolutionCount = 0;
        var service = new TranslationService(
            static text => text,
            capturedTranslator,
            translatorResolver: _ => Interlocked.Increment(ref resolutionCount) == 1
                ? new TranslationService.TranslatorResolution(
                    (int)Echoglossian.TransEngines.Google,
                    capturedTranslator)
                : new TranslationService.TranslatorResolution(
                    (int)Echoglossian.TransEngines.Deepl,
                    replacementTranslator));

        var result = await service.TranslateFieldsAsync(
            [
                new TranslationField("Name", "Actions"),
                new TranslationField("Description", "Opens the window."),
            ],
            new SourceClientLanguage("en", "en"),
            "pt-BR");

        Assert.True(result.UsedIndividualFallback);
        Assert.Equal("captured:Actions", result.GetTranslation("Name"));
        Assert.Equal("captured:Opens the window.", result.GetTranslation("Description"));
        Assert.Equal(1, resolutionCount);
        Assert.Equal(3, capturedTranslator.CallCount);
        Assert.Equal(0, replacementTranslator.CallCount);
    }

    /// <summary>
    ///     Ensures a structurally valid envelope that contains a provider
    ///     failure cannot partially bypass normal result acceptance.
    /// </summary>
    /// <returns>The asynchronous test task.</returns>
    [Fact]
    public async Task TranslateFieldsAsync_ProviderFailureField_FallsBackForEveryField()
    {
        var translator = new EnvelopeTranslator(
            static fields => TranslationFieldEnvelopeCodec.Encode(
                fields.Select(field => new TranslationField(
                    field.Name,
                    string.Equals(field.Name, "Description", StringComparison.Ordinal)
                        ? "[Translation Error: provider timeout]"
                        : "batch:Acoes"))),
            static text => $"single:{text}");
        var service = this.CreateService(translator);

        var result = await service.TranslateFieldsAsync(
            [
                new TranslationField("Name", "Actions"),
                new TranslationField("Description", "Opens the window."),
            ],
            new SourceClientLanguage("en", "en"),
            "pt-BR");

        Assert.True(result.UsedIndividualFallback);
        Assert.Equal("single:Actions", result.GetTranslation("Name"));
        Assert.Equal("single:Opens the window.", result.GetTranslation("Description"));
        Assert.Equal(3, translator.CallCount);
    }

    /// <summary>
    ///     Ensures blank field identifiers cannot enter a batch request.
    /// </summary>
    /// <returns>The asynchronous test task.</returns>
    [Fact]
    public async Task TranslateFieldsAsync_BlankFieldIdentifier_RejectsBeforeTranslation()
    {
        var translator = new EnvelopeTranslator(static _ => throw new InvalidOperationException());
        var service = this.CreateService(translator);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.TranslateFieldsAsync(
                [new TranslationField(string.Empty, "Actions")],
                new SourceClientLanguage("en", "en"),
                "pt-BR"));

        Assert.Equal(0, translator.CallCount);
    }

    /// <summary>
    ///     Ensures duplicate field identifiers cannot overwrite a batch result.
    /// </summary>
    /// <returns>The asynchronous test task.</returns>
    [Fact]
    public async Task TranslateFieldsAsync_DuplicateFieldIdentifier_RejectsBeforeTranslation()
    {
        var translator = new EnvelopeTranslator(static _ => throw new InvalidOperationException());
        var service = this.CreateService(translator);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.TranslateFieldsAsync(
                [
                    new TranslationField("Name", "Actions"),
                    new TranslationField("Name", "Other actions"),
                ],
                new SourceClientLanguage("en", "en"),
                "pt-BR"));

        Assert.Equal(0, translator.CallCount);
    }

    private TranslationService CreateService(ITranslator translator)
    {
        return new TranslationService(
            static text => text,
            translator,
            translatorResolver: _ => new TranslationService.TranslatorResolution(
                (int)Echoglossian.TransEngines.Google,
                translator));
    }

    private sealed class EnvelopeTranslator : ITranslator
    {
        private readonly Func<IReadOnlyList<TranslationField>, string> batchResponse;
        private readonly Func<string, string> individualResponse;

        internal EnvelopeTranslator(
            Func<IReadOnlyList<TranslationField>, string> batchResponse,
            Func<string, string>? individualResponse = null)
        {
            this.batchResponse = batchResponse;
            this.individualResponse = individualResponse ?? (static text => text);
        }

        internal int CallCount { get; private set; }

        /// <summary>
        ///     Gets the raw requests received by the translator.
        /// </summary>
        internal List<string> Requests { get; } = [];

        /// <inheritdoc />
        public string? Translate(string text, string sourceLanguage, string targetLanguage)
        {
            return this.TranslateCore(text);
        }

        /// <inheritdoc />
        public Task<string?> TranslateAsync(
            string text,
            string sourceLanguage,
            string targetLanguage)
        {
            return Task.FromResult<string?>(this.TranslateCore(text));
        }

        private string TranslateCore(string text)
        {
            this.CallCount++;
            this.Requests.Add(text);
            return TranslationFieldEnvelopeCodec.TryDecode(text, out var fields)
                ? this.batchResponse(fields)
                : this.individualResponse(text);
        }
    }
}
