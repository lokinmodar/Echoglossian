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
    /// <summary>Individual fallback must preserve rejection rather than returning source text as accepted output.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("[Translation Error: simulated provider failure]")]
    [InlineData("unavailable-fixture")]
    public async Task TranslateFieldsAsync_RejectedIndividualField_RejectsEntireBatch(string rejected)
    {
        if (rejected == "unavailable-fixture")
        {
            rejected = global::Echoglossian.Properties.Resources.ChatGPTTranslationUnavailablePleaseCheckYourAPIKey;
        }

        var translator = new EnvelopeTranslator(_ => "invalid envelope", text => text == "Actions" ? "Acoes" : rejected);
        var failures = 0;
        var service = new TranslationService(text => text, translator,
            recordFailedTranslation: (_, _, _, _, _, _) => failures++,
            recordTransientFailedTranslation: (_, _, _, _, _, _) => failures++);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.TranslateFieldsAsync(
            [new TranslationField("Name", "Actions"), new TranslationField("Description", "Open actions.")],
            new SourceClientLanguage("en", "en"), "pt"));
        Assert.Equal(3, translator.CallCount);
        Assert.Equal(1, failures);
    }

    /// <summary>A known failed field must not become accepted merely because the service bypasses the provider.</summary>
    [Fact]
    public async Task TranslateFieldsAsync_KnownFailureCache_RejectsWithoutProviderCall()
    {
        var translator = new EnvelopeTranslator(_ => throw new InvalidOperationException("Provider should not run."));
        var service = new TranslationService(text => text, translator,
            isKnownFailedTranslation: (_, _, _, _) => true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.TranslateFieldsAsync(
            [new TranslationField("Name", "Actions")], new SourceClientLanguage("en", "en"), "pt"));
        Assert.Equal(0, translator.CallCount);
        Assert.Equal("Actions", await service.TranslateAsync("Actions", "en", "pt"));
    }

    /// <summary>A real accepted translation may equal the source and must remain distinguishable from fallback.</summary>
    [Fact]
    public async Task TranslateFieldsAsync_AcceptedIdenticalIndividualField_RemainsAccepted()
    {
        var translator = new EnvelopeTranslator(_ => "invalid envelope", text => text);
        var result = await this.CreateService(translator).TranslateFieldsAsync(
            [new TranslationField("Name", "Actions"), new TranslationField("Description", "Open actions.")],
            new SourceClientLanguage("en", "en"), "pt");
        Assert.Equal("Actions", result.GetTranslation("Name"));
        Assert.Equal("Open actions.", result.GetTranslation("Description"));
        Assert.True(result.UsedIndividualFallback);
    }

    /// <summary>Batch and individual fallback retain the engine captured before settings change.</summary>
    /// <param name="malformed">Whether the provider forces individual fallback.</param>
    /// <returns>The asynchronous test task.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TranslateFieldsAsync_CapturedResolutionSurvivesSettingsChange(bool malformed)
    {
        var original = new EnvelopeTranslator(fields => malformed ? "broken envelope" :
            TranslationFieldEnvelopeCodec.Encode(fields.Select(field => new TranslationField(field.Name, "captured:" + field.Text))),
            text => "captured:" + text);
        var replacement = new EnvelopeTranslator(_ => throw new InvalidOperationException("Live engine used after capture."));
        var current = new TranslationService.TranslatorResolution(0, original);
        var service = new TranslationService(text => text, original, translatorResolver: _ => current);
        var captured = service.CaptureTranslatorResolution(0, TranslationSurfaceGroup.Default);
        current = new TranslationService.TranslatorResolution(4, replacement);
        var result = await service.TranslateFieldsAsync(
            [new TranslationField("Name", "Actions"), new TranslationField("Description", "Open actions.")],
            new SourceClientLanguage("en", "en"), "pt", captured);
        Assert.Equal("captured:Actions", result.GetTranslation("Name"));
        Assert.Equal("captured:Open actions.", result.GetTranslation("Description"));
        Assert.Equal(malformed, result.UsedIndividualFallback);
        Assert.Equal(0, replacement.CallCount);
    }

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

    /// <summary>
    ///     Ensures a translator timeout without caller cancellation safely
    ///     falls back to individual field translation.
    /// </summary>
    /// <returns>The asynchronous test task.</returns>
    [Fact]
    public async Task TranslateFieldsAsync_TranslatorCancellationWithoutCallerCancellation_FallsBackIndividually()
    {
        var translator = new EnvelopeTranslator(
            static _ => throw new TaskCanceledException("translator timeout"),
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
    ///     Ensures caller-requested cancellation remains observable rather
    ///     than being transformed into a fallback result.
    /// </summary>
    /// <returns>The asynchronous test task.</returns>
    [Fact]
    public async Task TranslateFieldsAsync_CallerCancellation_Propagates()
    {
        var translator = new EnvelopeTranslator(static _ => throw new InvalidOperationException());
        var service = this.CreateService(translator);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await service.TranslateFieldsAsync(
                [new TranslationField("Name", "Actions")],
                new SourceClientLanguage("en", "en"),
                "pt-BR",
                cancellationToken: cancellationSource.Token));

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
