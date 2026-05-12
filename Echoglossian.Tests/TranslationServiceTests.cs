// <copyright file="TranslationServiceTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators;

using Xunit;

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

        var service = new TranslationService(
            text => text,
            translator,
            translationEngine: 8,
            isKnownFailedTranslation: (text, source, target, engine) =>
                text == "hello" &&
                source == "en" &&
                target == "pt-BR" &&
                engine == 8);

        var result = service.Translate("hello", "English", "pt");

        Assert.Equal("hello", result);
        Assert.Equal(0, translator.SyncCalls);
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
    ///     Ensures an empty synchronous result falls back cleanly without
    ///     persisting a long-lived known-failure row.
    /// </summary>
    [Fact]
    public void Translate_EmptyResult_DoesNotPersistKnownFailure()
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
            recordFailedTranslation: (text, source, target, engine, reason, origin) =>
            {
                recordedText = text;
                recordedSource = source;
                recordedTarget = target;
                recordedEngine = engine;
                recordedReason = reason;
            });

        var result = service.Translate("hello", "English", "pt");

        Assert.Equal("hello", result);
        Assert.Null(recordedText);
        Assert.Null(recordedSource);
        Assert.Null(recordedTarget);
        Assert.Null(recordedEngine);
        Assert.Null(recordedReason);
    }

    /// <summary>
    ///     Ensures an empty asynchronous result falls back cleanly without
    ///     persisting a long-lived known-failure row.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task TranslateAsync_EmptyResult_DoesNotPersistKnownFailure()
    {
        var translator = new RecordingTranslator
        {
            AsyncResult = string.Empty,
        };
        string? recordedText = null;

        var service = new TranslationService(
            text => text,
            translator,
            translationEngine: 5,
            recordFailedTranslation: (text, source, target, engine, reason, origin) =>
            {
                recordedText = text;
            });

        var result = await service.TranslateAsync("...hello", "en", "pt-BR");

        Assert.Equal("...hello", result);
        Assert.Null(recordedText);
    }

    /// <summary>
    ///     Ensures a synthetic translation-error placeholder is treated as a
    ///     failed translation and never persisted as a long-lived known-failure
    ///     row.
    /// </summary>
    [Fact]
    public void Translate_SyntheticErrorResult_FallsBackWithoutPersistingFailure()
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
            recordFailedTranslation: (text, source, target, engine, reason, origin) =>
            {
                recordedReason = reason;
            });

        var result = service.Translate("hello", "en", "pt-BR");

        Assert.Equal("hello", result);
        Assert.Null(recordedReason);
    }

    /// <summary>
    ///     Ensures the async path also treats synthetic translation-error
    ///     placeholders as failed translations without persisting a long-lived
    ///     known-failure row.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task TranslateAsync_SyntheticErrorResult_FallsBackWithoutPersistingFailure()
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
            recordFailedTranslation: (text, source, target, engine, reason, origin) =>
            {
                recordedReason = reason;
            });

        var result = await service.TranslateAsync("...hello", "en", "pt-BR");

        Assert.Equal("...hello", result);
        Assert.Null(recordedReason);
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
    ///     Ensures a failed translation records the explicit origin context
    ///     when one is provided by the caller.
    /// </summary>
    [Fact]
    public void Translate_RecordsExplicitOriginContext()
    {
        var translator = new RecordingTranslator
        {
            SyncResult = string.Empty,
        };
        string? recordedOrigin = null;

        var service = new TranslationService(
            text => text,
            translator,
            translationEngine: 11,
            recordFailedTranslation: (text, source, target, engine, reason, origin) =>
            {
                recordedOrigin = origin;
            });

        _ = service.Translate(
            "hello",
            "en",
            "pt-BR",
            originContext: "ActionDetailPrefetch.Name");

        Assert.Equal("ActionDetailPrefetch.Name", recordedOrigin);
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
        var service = new TranslationService(
            text => text,
            translator,
            translationEngine: (int)Echoglossian.TransEngines.ChatGPT,
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
    private sealed class RecordingTranslator : ITranslator
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
}
