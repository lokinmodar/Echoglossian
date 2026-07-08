// <copyright file="TranslationServiceTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators;

using Xunit;

using Echoglossian.Properties;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the shared translation-service pipeline independently from any live engine implementation.
/// </summary>
public class TranslationServiceTests
{
    /// <summary>
    ///     Ensures the service sanitizes text before passing it to the translator.
    /// </summary>
    [Fact]
    public void Translate_UsesSanitizedText()
    {
        var translator = new RecordingTranslator
        {
            SyncResult = "translated",
        };

        var service = new TranslationService(
            text => $"clean:{text}",
            translator);

        var result = service.Translate("raw", "en", "pt");

        Assert.Equal("translated", result);
        Assert.Equal("clean:raw", translator.LastSyncText);
    }

    /// <summary>
    ///     Ensures the service preserves leading ellipsis while translating the remaining text.
    /// </summary>
    [Fact]
    public void Translate_PreservesLeadingEllipsis()
    {
        var translator = new RecordingTranslator
        {
            SyncResult = "traduzido",
        };

        var service = new TranslationService(
            text => text,
            translator);

        var result = service.Translate("...hello", "en", "pt");

        Assert.Equal("...traduzido", result);
        Assert.Equal("hello", translator.LastSyncText);
    }

    /// <summary>
    ///     Ensures the service short-circuits sentinel text without invoking the translator.
    /// </summary>
    [Fact]
    public void Translate_DoesNotTranslateSentinelQuestionMarks()
    {
        var translator = new RecordingTranslator
        {
            SyncResult = "should-not-be-used",
        };

        var service = new TranslationService(
            text => text,
            translator);

        var result = service.Translate("???", "en", "pt");

        Assert.Equal("???", result);
        Assert.Equal(0, translator.SyncCalls);
    }

    /// <summary>
    ///     Ensures the async path uses the translator asynchronously and preserves ellipsis behavior.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task TranslateAsync_UsesAsyncTranslator()
    {
        var translator = new RecordingTranslator
        {
            AsyncResult = "assinc",
        };

        var service = new TranslationService(
            text => text,
            translator);

        var result = await service.TranslateAsync("...hello", "en", "pt");

        Assert.Equal("...assinc", result);
        Assert.Equal("hello", translator.LastAsyncText);
        Assert.Equal(1, translator.AsyncCalls);
    }

    /// <summary>
    ///     Ensures the service skips exact requests already known to fail for
    ///     the same source and target language pair plus engine.
    /// </summary>
    [Fact]
    public void Translate_SkipsKnownFailedRequest_ForExactTextAndLanguagePair()
    {
        var translator = new RecordingTranslator
        {
            SyncResult = "should-not-be-used",
        };
        TranslationRequestMetricOutcome? recordedOutcome = null;

        var service = new TranslationService(
            text => text,
            translator,
            translationEngine: 8,
            isKnownFailedTranslation: (text, source, target, engine) =>
                text == "hello" &&
                source == "en" &&
                target == "pt-BR" &&
                engine == 8,
            recordTranslationMetric: (engine, outcome, latency, failureReason, usedDialogueContext) =>
            {
                recordedOutcome = outcome;
            });

        var result = service.Translate("hello", "English", "pt");

        Assert.Equal("hello", result);
        Assert.Equal(0, translator.SyncCalls);
        Assert.Equal(
            TranslationRequestMetricOutcome.ShortCircuited,
            recordedOutcome);
    }

    /// <summary>
    ///     Ensures the exact-failure gate still honors the source and target
    ///     languages instead of suppressing unrelated requests.
    /// </summary>
    [Fact]
    public void Translate_DoesNotSkipKnownFailedRequest_ForDifferentTargetLanguage()
    {
        var translator = new RecordingTranslator
        {
            SyncResult = "traduzido",
        };

        var service = new TranslationService(
            text => text,
            translator,
            translationEngine: 8,
            isKnownFailedTranslation: (text, source, target, engine) =>
                text == "hello" &&
                source == "en" &&
                target == "pt-BR" &&
                engine == 8);

        var result = service.Translate("hello", "English", "de");

        Assert.Equal("traduzido", result);
        Assert.Equal(1, translator.SyncCalls);
    }

    /// <summary>
    ///     Ensures an empty synchronous result falls back cleanly while still
    ///     recording a transient failure reason for the persistence guard.
    /// </summary>
    [Fact]
  public void Translate_EmptyResult_RecordsTransientFailureReason()
  {
        var translator = new RecordingTranslator
        {
            SyncResult = string.Empty,
        };
        string? recordedText = null;
        string? recordedSource = null;
        string? recordedTarget = null;
        int? recordedEngine = null;
        string? recordedReason = null;

        var service = new TranslationService(
            text => text,
            translator,
            translationEngine: 11,
            recordTransientFailedTranslation: (text, source, target, engine, reason, ttl) =>
            {
                recordedText = text;
                recordedSource = source;
                recordedTarget = target;
                recordedEngine = engine;
                recordedReason = reason;
            });

        var result = service.Translate("hello", "English", "pt");

        Assert.Equal("hello", result);
        Assert.Equal("hello", recordedText);
        Assert.Equal("en", recordedSource);
        Assert.Equal("pt-BR", recordedTarget);
        Assert.Equal(11, recordedEngine);
        Assert.Equal("empty-result", recordedReason);
    }

    /// <summary>
    ///     Ensures an empty asynchronous result falls back cleanly while still
    ///     recording a transient failure reason for the persistence guard.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task TranslateAsync_EmptyResult_RecordsTransientFailureReason()
    {
        var translator = new RecordingTranslator
        {
            AsyncResult = string.Empty,
        };
        string? recordedText = null;
        string? recordedReason = null;

        var service = new TranslationService(
            text => text,
            translator,
            translationEngine: 5,
            recordTransientFailedTranslation: (text, source, target, engine, reason, ttl) =>
            {
                recordedText = text;
                recordedReason = reason;
            });

        var result = await service.TranslateAsync("...hello", "en", "pt-BR");

        Assert.Equal("...hello", result);
        Assert.Equal("hello", recordedText);
        Assert.Equal("empty-result", recordedReason);
    }

    /// <summary>
    ///     Ensures a synthetic translation-error placeholder is treated as a
    ///     failed translation and recorded as a transient synthetic-error
    ///     reason.
    /// </summary>
    [Fact]
    public void Translate_SyntheticErrorResult_RecordsTransientFailureReason()
    {
        var translator = new RecordingTranslator
        {
            SyncResult = "[Translation Error: LmStudio: Connection refused]",
        };
        string? recordedReason = null;

        var service = new TranslationService(
            text => text,
            translator,
            translationEngine: 3,
            recordTransientFailedTranslation: (text, source, target, engine, reason, ttl) =>
            {
                recordedReason = reason;
            });

        var result = service.Translate("hello", "en", "pt-BR");

        Assert.Equal("hello", result);
        Assert.Equal("llm-endpoint-unavailable", recordedReason);
    }

    /// <summary>
    ///     Ensures the async path also treats synthetic translation-error
    ///     placeholders as failed translations while recording a transient
    ///     synthetic-error reason.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task TranslateAsync_SyntheticErrorResult_RecordsTransientFailureReason()
    {
        var translator = new RecordingTranslator
        {
            AsyncResult = "[Translation Error: Ollama error: Connection refused]",
        };
        string? recordedReason = null;

        var service = new TranslationService(
            text => text,
            translator,
            translationEngine: 4,
            recordTransientFailedTranslation: (text, source, target, engine, reason, ttl) =>
            {
                recordedReason = reason;
            });

        var result = await service.TranslateAsync("...hello", "en", "pt-BR");

        Assert.Equal("...hello", result);
        Assert.Equal("llm-endpoint-unavailable", recordedReason);
    }

    /// <summary>
    ///     Ensures the async service path dispatches runtime-only short-lived
    ///     dialogue context to translators that explicitly support it.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task TranslateAsync_WithDialogueContext_UsesContextAwareTranslator()
    {
        var translator = new ContextAwareRecordingTranslator
        {
            AsyncResult = "translated-with-context",
        };

        var service = new TranslationService(
            text => text,
            translator);
        var dialogueContext = new DialogueTranslationContext(
            "Talk",
            "Krile|engine:8|target:pt-BR",
            "Krile",
            [
                new DialogueTranslationTurn(
                    "Krile",
                    "Pray return.",
                    new DateTime(2026, 05, 12, 15, 20, 0, DateTimeKind.Utc)),
            ]);

        var result = await service.TranslateAsync(
            "We must press on.",
            "English",
            "pt-BR",
            dialogueContext);

        Assert.Equal("translated-with-context", result);
        Assert.Equal(1, translator.ContextAwareAsyncCalls);
        Assert.Equal(0, translator.AsyncCalls);
        Assert.Equal("Talk", translator.LastDialogueContext?.SessionNamespace);
        Assert.True(service.WillUseDialogueContext(dialogueContext));
    }

    /// <summary>
    ///     Ensures dialogue-family requests can route to a different engine
    ///     than the global default without creating a second translation
    ///     service.
    /// </summary>
    [Fact]
    public async Task TranslateAsync_DialogueSurface_UsesRoutedTranslator()
    {
        var defaultTranslator = new RecordingTranslator
        {
            AsyncResult = "default-path",
        };
        var dialogueTranslator = new RecordingTranslator
        {
            AsyncResult = "dialogue-path",
        };
        var service = new TranslationService(
            text => text,
            defaultTranslator,
            translationEngine: (int)Echoglossian.TransEngines.Google,
            translatorResolver: surfaceGroup => surfaceGroup == TranslationSurfaceGroup.Dialogue
                ? new TranslationService.TranslatorResolution(
                    (int)Echoglossian.TransEngines.ChatGPT,
                    dialogueTranslator)
                : new TranslationService.TranslatorResolution(
                    (int)Echoglossian.TransEngines.Google,
                    defaultTranslator));

        var result = await service.TranslateAsync(
            "hello",
            "English",
            "pt-BR",
            TranslationSurfaceGroup.Dialogue);

        Assert.Equal("dialogue-path", result);
        Assert.Equal(0, defaultTranslator.AsyncCalls);
        Assert.Equal(1, dialogueTranslator.AsyncCalls);
        Assert.Equal(
            (int)Echoglossian.TransEngines.ChatGPT,
            service.GetEffectiveTranslationEngineId(
                TranslationSurfaceGroup.Dialogue));
    }

    /// <summary>
    ///     Ensures dialogue-context routing checks the translator selected for
    ///     the specific surface group instead of only the global default path.
    /// </summary>
    [Fact]
    public void WillUseDialogueContext_DialogueSurface_UsesRoutedTranslator()
    {
        var defaultTranslator = new RecordingTranslator();
        var dialogueTranslator = new ContextAwareRecordingTranslator();
        var service = new TranslationService(
            text => text,
            defaultTranslator,
            translationEngine: (int)Echoglossian.TransEngines.Google,
            translatorResolver: surfaceGroup => surfaceGroup == TranslationSurfaceGroup.Dialogue
                ? new TranslationService.TranslatorResolution(
                    (int)Echoglossian.TransEngines.ChatGPT,
                    dialogueTranslator)
                : new TranslationService.TranslatorResolution(
                    (int)Echoglossian.TransEngines.Google,
                    defaultTranslator));
        var dialogueContext = new DialogueTranslationContext(
            "Talk",
            "Krile|engine:2|target:pt-BR",
            "Krile",
            [
                new DialogueTranslationTurn(
                    "Krile",
                    "Pray return.",
                    new DateTime(2026, 05, 12, 15, 20, 0, DateTimeKind.Utc)),
            ]);

        Assert.True(
            service.WillUseDialogueContext(
                dialogueContext,
                TranslationSurfaceGroup.Dialogue));
        Assert.False(
            service.WillUseDialogueContext(
                dialogueContext,
                TranslationSurfaceGroup.Default));
    }

    /// <summary>
    ///     Ensures empty runtime-only dialogue context does not switch the
    ///     translation service into the context-aware path.
    /// </summary>
    [Fact]
    public void WillUseDialogueContext_RequiresPriorTurns()
    {
        var translator = new ContextAwareRecordingTranslator();
        var service = new TranslationService(
            text => text,
            translator);
        var dialogueContext = new DialogueTranslationContext(
            "Talk",
            "Krile|engine:8|target:pt-BR",
            "Krile",
            []);

        Assert.False(service.WillUseDialogueContext(dialogueContext));
    }

    /// <summary>
    ///     Ensures a known failed request keeps the sanitized original text
    ///     instead of collapsing to an empty string.
    /// </summary>
    [Fact]
    public void Translate_KnownFailedRequest_ReturnsSanitizedOriginalText()
    {
        var translator = new RecordingTranslator
        {
            SyncResult = "should-not-be-used",
        };

        var service = new TranslationService(
            text => $"clean:{text}",
            translator,
            translationEngine: 8,
            isKnownFailedTranslation: (text, source, target, engine) =>
                text == "clean:hello" &&
                source == "en" &&
                target == "pt-BR" &&
                engine == 8);

        var result = service.Translate("hello", "English", "pt");

        Assert.Equal("clean:hello", result);
        Assert.Equal(0, translator.SyncCalls);
    }

    /// <summary>
     ///     Ensures known localized engine-unavailable messages do not become
     ///     accepted translated output and are reported to the runtime
    ///     feedback path.
    /// </summary>
    [Fact]
    public void Translate_UnavailableMessage_FallsBackAndReportsFailure()
    {
        var translator = new RecordingTranslator
        {
            SyncResult = Resources.ChatGPTTranslationUnavailablePleaseCheckYourAPIKey,
        };
        TranslationFailureClassification? reportedClassification = null;
        TranslationRequestMetricOutcome? recordedOutcome = null;
        var service = new TranslationService(
            text => text,
            translator,
            translationEngine: (int)Echoglossian.TransEngines.ChatGPT,
            recordTranslationMetric: (engine, outcome, latency, failureReason, usedDialogueContext) =>
            {
                recordedOutcome = outcome;
            },
            reportTranslationFailure: (engine, classification) =>
            {
                reportedClassification = classification;
            });

        var result = service.Translate("hello", "en", "pt-BR");

        Assert.Equal("hello", result);
        Assert.NotNull(reportedClassification);
        Assert.Equal(
            TranslationFailureKind.EngineUnavailable,
            reportedClassification!.Kind);
        Assert.Equal(
            TranslationRequestMetricOutcome.Failure,
            recordedOutcome);
    }

    /// <summary>
    ///     Ensures transient LLM failures are recorded in the runtime-only
    ///     failure path instead of the persistent failure path.
    /// </summary>
    [Fact]
    public void Translate_QuotaFailure_RecordsTransientFailure()
    {
        var translator = new RecordingTranslator
        {
            SyncResult = "[Translation Error: OpenAI: insufficient_quota]",
        };
        string? persistentReason = null;
        string? transientReason = null;
        TimeSpan? transientTtl = null;

        var service = new TranslationService(
            text => text,
            translator,
            translationEngine: (int)Echoglossian.TransEngines.ChatGPT,
            recordFailedTranslation: (text, source, target, engine, reason, origin) =>
            {
                persistentReason = reason;
            },
            recordTransientFailedTranslation: (text, source, target, engine, reason, ttl) =>
            {
                transientReason = reason;
                transientTtl = ttl;
            });

        var result = service.Translate("hello", "en", "pt-BR");

        Assert.Equal("hello", result);
        Assert.Null(persistentReason);
        Assert.Equal("llm-quota-or-rate-limit", transientReason);
        Assert.Equal(TimeSpan.FromSeconds(30), transientTtl);
    }

    /// <summary>
    ///     Minimal fake translator for pipeline tests.
    /// </summary>
    private class RecordingTranslator : ITranslator
    {
        /// <summary>
        ///     Gets or sets the synchronous result.
        /// </summary>
        public string? SyncResult { get; set; }

        /// <summary>
        ///     Gets or sets the asynchronous result.
        /// </summary>
        public string? AsyncResult { get; set; }

        /// <summary>
        ///     Gets the number of sync calls.
        /// </summary>
        public int SyncCalls { get; private set; }

        /// <summary>
        ///     Gets the number of async calls.
        /// </summary>
        public int AsyncCalls { get; private set; }

        /// <summary>
        ///     Gets the last sync text.
        /// </summary>
        public string? LastSyncText { get; private set; }

        /// <summary>
        ///     Gets the last async text.
        /// </summary>
        public string? LastAsyncText { get; private set; }

        /// <inheritdoc/>
        public string? Translate(string text, string sourceLanguage, string targetLanguage)
        {
            this.SyncCalls++;
            this.LastSyncText = text;
            return this.SyncResult;
        }

        /// <inheritdoc/>
        public Task<string?> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
        {
            this.AsyncCalls++;
            this.LastAsyncText = text;
            return Task.FromResult(this.AsyncResult);
        }
    }

    /// <summary>
    ///     Minimal fake translator that also records runtime-only short-lived
    ///     dialogue context dispatch.
    /// </summary>
    private sealed class ContextAwareRecordingTranslator : RecordingTranslator, IDialogueContextAwareTranslator
    {
        /// <summary>
        ///     Gets the number of context-aware async calls.
        /// </summary>
        public int ContextAwareAsyncCalls { get; private set; }

        /// <summary>
        ///     Gets the last dialogue context seen by the context-aware path.
        /// </summary>
        public DialogueTranslationContext? LastDialogueContext { get; private set; }

        /// <inheritdoc/>
        public Task<string?> TranslateAsync(
            string text,
            string sourceLanguage,
            string targetLanguage,
            DialogueTranslationContext dialogueContext)
        {
            this.ContextAwareAsyncCalls++;
            this.LastDialogueContext = dialogueContext;
            return Task.FromResult(this.AsyncResult);
        }
    }
}
