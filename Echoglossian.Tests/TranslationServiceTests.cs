// <copyright file="TranslationServiceTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Game;
using Dalamud.Game.Text.Sanitizer;
using Dalamud.Plugin.Services;

using Echoglossian.Translators;
using Echoglossian.LanguagesHandling;
using System.Globalization;

using Xunit;

using Echoglossian.Properties;
using Serilog;
using Serilog.Events;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the shared translation-service pipeline independently from any live engine implementation.
/// </summary>
public class TranslationServiceTests
{
    /// <summary>
    ///     Ensures translation-service request diagnostics include the
    ///     surface or element context as a square-bracketed prefix.
    /// </summary>
    [Fact]
    public void Translate_DebugLog_IncludesOriginContextAsSurfaceScope()
    {
        var pluginLog = new CapturingPluginLog();
        var service = new TranslationService(
            new Config
            {
                ChosenTransEngine = (int)Echoglossian.TransEngines.All,
            },
            pluginLog,
            new Sanitizer(ClientLanguage.English));

        _ = service.Translate(
            "Steel Fangs",
            new SourceClientLanguage("en", "en"),
            "pt-BR",
            originContext: "ActionTooltip/Name");

        Assert.Contains(
            pluginLog.DebugMessages,
            message =>
                message.StartsWith(
                    "[ActionTooltip/Name] TranslationService: Translate called",
                    StringComparison.Ordinal) &&
                message.Contains("text: Steel Fangs", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Ensures async translation-service request diagnostics include the
    ///     surface or element context as a square-bracketed prefix.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task TranslateAsync_DebugLog_IncludesOriginContextAsSurfaceScope()
    {
        var pluginLog = new CapturingPluginLog();
        var service = new TranslationService(
            new Config
            {
                ChosenTransEngine = (int)Echoglossian.TransEngines.All,
            },
            pluginLog,
            new Sanitizer(ClientLanguage.English));

        _ = await service.TranslateAsync(
            "Quest accepted.",
            new SourceClientLanguage("en", "en"),
            "pt-BR",
            originContext: "QuestToast/Centre");

        Assert.Contains(
            pluginLog.DebugMessages,
            message =>
                message.StartsWith(
                    "[QuestToast/Centre] TranslationService: TranslateAsync called",
                    StringComparison.Ordinal) &&
                message.Contains("text: Quest accepted.", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Ensures captured translator-resolution requests still include a
    ///     square-bracketed surface indicator when no explicit origin context is
    ///     supplied by the caller.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task TranslateAsync_CapturedTranslatorResolutionWithoutOriginContext_UsesSurfaceGroupScope()
    {
        var pluginLog = new CapturingPluginLog();
        var service = new TranslationService(
            new Config
            {
                ChosenTransEngine = (int)Echoglossian.TransEngines.All,
            },
            pluginLog,
            new Sanitizer(ClientLanguage.English));
        var translatorResolution = service.CaptureTranslatorResolution(
            (int)Echoglossian.TransEngines.All,
            TranslationSurfaceGroup.Dialogue);

        _ = await service.TranslateAsync(
            "Talk line.",
            new SourceClientLanguage("en", "en"),
            "pt-BR",
            TranslationSurfaceGroup.Dialogue,
            translatorResolution);

        Assert.Contains(
            pluginLog.DebugMessages,
            message =>
                message.StartsWith(
                    "[Dialogue] TranslationService: TranslateAsync called",
                    StringComparison.Ordinal) &&
                message.Contains("text: Talk line.", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Ensures non-request translation-service diagnostics also include a
    ///     square-bracketed surface indicator.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task TranslateAsync_MissingLanguageAssetsBypass_UsesSurfaceScope()
    {
        var previousSelectedLanguage = Echoglossian.SelectedLanguage;
        var previousAssetFiles = AssetsManager.AssetFiles;
        var previousAssetsPath = AssetsManager.AssetsPath;
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            Echoglossian.SelectedLanguage = new LanguageInfo(
                "ja",
                "Japanese",
                "NotoSansCJKjp-Regular.otf",
                string.Empty,
                []);
            AssetsManager.AssetFiles =
            [
                "NotoSansCJKjp-Regular.otf",
            ];
            AssetsManager.AssetsPath = tempDirectory.FullName;

            var pluginLog = new CapturingPluginLog();
            var service = new TranslationService(
                new Config
                {
                    ChosenTransEngine = (int)Echoglossian.TransEngines.All,
                },
                pluginLog,
                new Sanitizer(ClientLanguage.English));
            var translatorResolution = service.CaptureTranslatorResolution(
                (int)Echoglossian.TransEngines.All,
                TranslationSurfaceGroup.Dialogue);

            _ = await service.TranslateAsync(
                "Missing assets line.",
                new SourceClientLanguage("en", "en"),
                "ja",
                TranslationSurfaceGroup.Dialogue,
                translatorResolution);

            Assert.Contains(
                pluginLog.DebugMessages,
                message =>
                    message.StartsWith(
                        "[Dialogue] TranslationService: bypassing translation",
                        StringComparison.Ordinal));
        }
        finally
        {
            Echoglossian.SelectedLanguage = previousSelectedLanguage;
            AssetsManager.AssetFiles = previousAssetFiles;
            AssetsManager.AssetsPath = previousAssetsPath;
            tempDirectory.Delete(recursive: true);
        }
    }

    /// <summary>
    ///     Ensures distinct simplified and traditional Chinese client sources
    ///     retain separate failure identities while sharing the provider code.
    /// </summary>
    [Fact]
    public void Translate_ChineseClientSources_SeparateFailureIdentity()
    {
        var translator = new RecordingTranslator
        {
            SyncResult = string.Empty,
        };
        var knownFailureSources = new HashSet<string>(StringComparer.Ordinal);
        var lookupSources = new List<string>();
        var recordedSources = new List<string>();
        var service = new TranslationService(
            text => text,
            translator,
            translationEngine: 8,
            isKnownFailedTranslation: (text, source, target, engine) =>
            {
                lookupSources.Add(source);
                return knownFailureSources.Contains(source);
            },
            recordFailedTranslation: (text, source, target, engine, reason, origin) =>
            {
                recordedSources.Add(source);
                knownFailureSources.Add(source);
            },
            recordTransientFailedTranslation: (text, source, target, engine, reason, ttl) =>
            {
                recordedSources.Add(source);
                knownFailureSources.Add(source);
            });

        var chsResult = service.Translate(
            "same text",
            new SourceClientLanguage("chs", "zh-CN"),
            "pt-BR");
        var chtResult = service.Translate(
            "same text",
            new SourceClientLanguage("cht", "zh-CN"),
            "pt-BR");

        Assert.Equal("same text", chsResult);
        Assert.Equal("same text", chtResult);
        Assert.Equal(2, translator.SyncCalls);
        Assert.Equal(["zh-CN", "zh-CN"], translator.SyncSourceLanguages);
        Assert.Equal(["chs", "cht"], lookupSources);
        Assert.Equal(["chs", "cht"], recordedSources);
    }

    /// <summary>
    ///     Ensures an unresolved source contract fails before failure-cache,
    ///     provider, or persistence activity.
    /// </summary>
    [Fact]
    public void Translate_UnknownCapturedSource_PerformsNoWork()
    {
        var translator = new RecordingTranslator
        {
            SyncResult = "should-not-be-used",
        };
        var failureLookupCalls = 0;
        var failureRecordCalls = 0;
        var transientFailureRecordCalls = 0;
        var service = new TranslationService(
            text => text,
            translator,
            isKnownFailedTranslation: (text, source, target, engine) =>
            {
                failureLookupCalls++;
                return false;
            },
            recordFailedTranslation: (text, source, target, engine, reason, origin) =>
            {
                failureRecordCalls++;
            },
            recordTransientFailedTranslation: (text, source, target, engine, reason, ttl) =>
            {
                transientFailureRecordCalls++;
            });

        var result = service.Translate(
            "unknown source text",
            default(SourceClientLanguage),
            "pt-BR");

        Assert.Equal("unknown source text", result);
        Assert.Equal(0, translator.SyncCalls);
        Assert.Equal(0, failureLookupCalls);
        Assert.Equal(0, failureRecordCalls);
        Assert.Equal(0, transientFailureRecordCalls);
    }

    /// <summary>
    ///     Ensures delayed async dialogue work retains its captured simplified
    ///     Chinese persistence identity after the live resolver changes.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task TranslateAsync_CapturedChsAfterResolverChangesToCht_PreservesSourceScope()
    {
        var translator = new ContextAwareRecordingTranslator
        {
            AsyncResult = string.Empty,
        };
        var defaultTranslator = new RecordingTranslator
        {
            AsyncResult = "wrong-surface",
        };
        var capturedSource = new SourceClientLanguage("chs", "zh-CN");
        var liveSource = capturedSource;
        var lookupSources = new List<string>();
        var lookupEngines = new List<int>();
        var recordedSources = new List<string>();
        var recordedEngines = new List<int>();
        var service = new TranslationService(
            text => text,
            defaultTranslator,
            translationEngine: 4,
            isKnownFailedTranslation: (text, source, target, engine) =>
            {
                lookupSources.Add(source);
                lookupEngines.Add(engine);
                return false;
            },
            recordFailedTranslation: (text, source, target, engine, reason, origin) =>
            {
                recordedSources.Add(source);
                recordedEngines.Add(engine);
            },
            recordTransientFailedTranslation: (text, source, target, engine, reason, ttl) =>
            {
                recordedSources.Add(source);
                recordedEngines.Add(engine);
            },
            translatorResolver: surface => surface == TranslationSurfaceGroup.Dialogue
                ? new TranslationService.TranslatorResolution(8, translator)
                : new TranslationService.TranslatorResolution(4, defaultTranslator),
            sourceLanguageResolver: _ => liveSource);
        var dialogueContext = new DialogueTranslationContext(
            "Talk",
            "captured-chs",
            "Krile",
            [
                new DialogueTranslationTurn(
                    "Krile",
                    "Pray return.",
                    new DateTime(2026, 07, 13, 12, 0, 0, DateTimeKind.Utc)),
            ]);

        liveSource = new SourceClientLanguage("cht", "zh-CN");
        var result = await service.TranslateAsync(
            "same text",
            capturedSource,
            "pt-BR",
            dialogueContext,
            TranslationSurfaceGroup.Dialogue);

        Assert.Equal("same text", result);
        Assert.Equal(["chs"], lookupSources);
        Assert.Equal([8], lookupEngines);
        Assert.Equal(["chs"], recordedSources);
        Assert.Equal([8], recordedEngines);
        Assert.Equal(["zh-CN"], translator.ContextAwareSourceLanguages);
        Assert.Equal(1, translator.ContextAwareAsyncCalls);
        Assert.Equal(0, translator.AsyncCalls);
        Assert.Equal(0, defaultTranslator.AsyncCalls);
    }

    /// <summary>
    ///     Ensures unknown or internally mismatched captured async sources fail
    ///     before resolver, failure-cache, provider, or persistence activity.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task TranslateAsync_UnknownOrMismatchedCapturedSource_PerformsNoWork()
    {
        var translator = new RecordingTranslator
        {
            AsyncResult = "should-not-be-used",
        };
        var sourceResolverCalls = 0;
        var failureLookupCalls = 0;
        var failureRecordCalls = 0;
        var service = new TranslationService(
            text => text,
            translator,
            isKnownFailedTranslation: (text, source, target, engine) =>
            {
                failureLookupCalls++;
                return false;
            },
            recordFailedTranslation: (text, source, target, engine, reason, origin) =>
            {
                failureRecordCalls++;
            },
            recordTransientFailedTranslation: (text, source, target, engine, reason, ttl) =>
            {
                failureRecordCalls++;
            },
            sourceLanguageResolver: _ =>
            {
                sourceResolverCalls++;
                return new SourceClientLanguage("cht", "zh-CN");
            });
        var invalidSources = new[]
        {
            default(SourceClientLanguage),
            new SourceClientLanguage("unknown", "unknown"),
            new SourceClientLanguage("chs", "zh-TW"),
        };

        foreach (var invalidSource in invalidSources)
        {
            var result = await service.TranslateAsync(
                "unknown source text",
                invalidSource,
                "pt-BR");

            Assert.Equal("unknown source text", result);
        }

        Assert.Equal(0, sourceResolverCalls);
        Assert.Equal(0, failureLookupCalls);
        Assert.Equal(0, failureRecordCalls);
        Assert.Equal(0, translator.AsyncCalls);
    }

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
    ///     Ensures cancellation stops waiting for an in-flight translator result.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task TranslateAsync_CancellationToken_CancelsBeforeProviderResult()
    {
        var translator = new PendingAsyncTranslator();
        var service = new TranslationService(
            text => text,
            translator);
        using var cancellationTokenSource = new CancellationTokenSource();

        var translationTask = service.TranslateAsync(
            "hello",
            "en",
            "pt",
            cancellationTokenSource.Token);
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await translationTask);

        translator.Complete("provider-result");

        Assert.True(translationTask.IsCanceled);
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
    public async Task TranslateAsync_WithFirstTurnDialogueContext_UsesContextAwareTranslator()
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
            []);

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
    ///     Ensures a captured translator resolution continues to use the
    ///     original instance when subsequent requests would resolve a
    ///     different configured engine.
    /// </summary>
    [Fact]
    public async Task TranslateAsync_CapturedTranslatorResolution_PinsEngineInstance()
    {
        var firstTranslator = new RecordingTranslator
        {
            AsyncResult = "first-engine",
        };
        var secondTranslator = new RecordingTranslator
        {
            AsyncResult = "second-engine",
        };
        var currentTranslator = firstTranslator;
        var service = new TranslationService(
            text => text,
            firstTranslator,
            translationEngine: (int)Echoglossian.TransEngines.Google,
            translatorResolver: _ => new TranslationService.TranslatorResolution(
                (int)Echoglossian.TransEngines.ChatGPT,
                currentTranslator));
        var capturedResolution = service.CaptureTranslatorResolution(
            (int)Echoglossian.TransEngines.ChatGPT,
            TranslationSurfaceGroup.Dialogue);
        currentTranslator = secondTranslator;

        var result = await service.TranslateAsync(
            "hello",
            new SourceClientLanguage("en", "en"),
            "pt-BR",
            TranslationSurfaceGroup.Dialogue,
            capturedResolution);

        Assert.Equal("first-engine", result);
        Assert.Equal(1, firstTranslator.AsyncCalls);
        Assert.Equal(0, secondTranslator.AsyncCalls);
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
    ///     Ensures anonymous first-turn dialogue context still routes through
    ///     the dialogue-aware translator contract.
    /// </summary>
    [Fact]
    public async Task TranslateAsync_WithAnonymousFirstTurnDialogueContext_UsesContextAwareTranslator()
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
            string.Empty,
            []);

        var result = await service.TranslateAsync(
            "We must press on.",
            "English",
            "pt-BR",
            dialogueContext);

        Assert.Equal("translated-with-context", result);
        Assert.Equal(1, translator.ContextAwareAsyncCalls);
        Assert.Equal(0, translator.AsyncCalls);
        Assert.True(service.WillUseDialogueContext(dialogueContext));
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
        private readonly List<string> asyncSourceLanguages = [];
        private readonly List<string> syncSourceLanguages = [];

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

        /// <summary>
        ///     Gets the provider source languages supplied to synchronous calls.
        /// </summary>
        public IReadOnlyList<string> SyncSourceLanguages =>
            this.syncSourceLanguages;

        /// <summary>
        ///     Gets the provider source languages supplied to asynchronous calls.
        /// </summary>
        public IReadOnlyList<string> AsyncSourceLanguages =>
            this.asyncSourceLanguages;

        /// <inheritdoc/>
        public string? Translate(string text, string sourceLanguage, string targetLanguage)
        {
            this.SyncCalls++;
            this.LastSyncText = text;
            this.syncSourceLanguages.Add(sourceLanguage);
            return this.SyncResult;
        }

        /// <inheritdoc/>
        public Task<string?> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
        {
            this.AsyncCalls++;
            this.LastAsyncText = text;
            this.asyncSourceLanguages.Add(sourceLanguage);
            return Task.FromResult(this.AsyncResult);
        }
    }

    /// <summary>
    ///     Fake translator that leaves asynchronous requests pending until the
    ///     test explicitly completes them.
    /// </summary>
    private sealed class PendingAsyncTranslator : ITranslator
    {
        private readonly TaskCompletionSource<string?> completionSource = new();

        /// <summary>
        ///     Completes the pending provider request.
        /// </summary>
        /// <param name="result">The provider result.</param>
        public void Complete(string? result)
        {
            this.completionSource.SetResult(result);
        }

        /// <inheritdoc/>
        public string? Translate(string text, string sourceLanguage, string targetLanguage)
        {
            throw new InvalidOperationException("Synchronous translation must not be used.");
        }

        /// <inheritdoc/>
        public Task<string?> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
        {
            return this.completionSource.Task;
        }
    }

    /// <summary>
    ///     Minimal fake translator that also records runtime-only short-lived
    ///     dialogue context dispatch.
    /// </summary>
    private sealed class ContextAwareRecordingTranslator : RecordingTranslator, IDialogueContextAwareTranslator
    {
        private readonly List<string> contextAwareSourceLanguages = [];

        /// <summary>
        ///     Gets the number of context-aware async calls.
        /// </summary>
        public int ContextAwareAsyncCalls { get; private set; }

        /// <summary>
        ///     Gets the last dialogue context seen by the context-aware path.
        /// </summary>
        public DialogueTranslationContext? LastDialogueContext { get; private set; }

        /// <summary>
        ///     Gets the provider source languages supplied to context-aware calls.
        /// </summary>
        public IReadOnlyList<string> ContextAwareSourceLanguages =>
            this.contextAwareSourceLanguages;

        /// <inheritdoc/>
        public Task<string?> TranslateAsync(
            string text,
            string sourceLanguage,
            string targetLanguage,
            DialogueTranslationContext dialogueContext)
        {
            this.ContextAwareAsyncCalls++;
            this.LastDialogueContext = dialogueContext;
            this.contextAwareSourceLanguages.Add(sourceLanguage);
            return Task.FromResult(this.AsyncResult);
        }
    }

    /// <summary>
    ///     Captures debug messages written through the Dalamud plugin logger.
    /// </summary>
    private sealed class CapturingPluginLog : IPluginLog
    {
        private readonly List<string> debugMessages = [];

        /// <summary>
        ///     Gets captured debug messages.
        /// </summary>
        public IReadOnlyList<string> DebugMessages => this.debugMessages;

        /// <summary>
        ///     Gets the inert Serilog logger required by the interface.
        /// </summary>
        public ILogger Logger { get; } = new LoggerConfiguration().CreateLogger();

        /// <summary>
        ///     Gets or sets the minimum log level accepted by this logger.
        /// </summary>
        public LogEventLevel MinimumLogLevel { get; set; } = LogEventLevel.Verbose;

        /// <inheritdoc/>
        public void Fatal(string messageTemplate, params object[] values)
        {
        }

        /// <inheritdoc/>
        public void Fatal(Exception? exception, string messageTemplate, params object[] values)
        {
        }

        /// <inheritdoc/>
        public void Error(string messageTemplate, params object[] values)
        {
        }

        /// <inheritdoc/>
        public void Error(Exception? exception, string messageTemplate, params object[] values)
        {
        }

        /// <inheritdoc/>
        public void Warning(string messageTemplate, params object[] values)
        {
        }

        /// <inheritdoc/>
        public void Warning(Exception? exception, string messageTemplate, params object[] values)
        {
        }

        /// <inheritdoc/>
        public void Information(string messageTemplate, params object[] values)
        {
        }

        /// <inheritdoc/>
        public void Information(Exception? exception, string messageTemplate, params object[] values)
        {
        }

        /// <inheritdoc/>
        public void Info(string messageTemplate, params object[] values)
        {
        }

        /// <inheritdoc/>
        public void Info(Exception? exception, string messageTemplate, params object[] values)
        {
        }

        /// <inheritdoc/>
        public void Debug(string messageTemplate, params object[] values)
        {
            this.debugMessages.Add(
                values.Length == 0
                    ? messageTemplate
                    : string.Format(
                        CultureInfo.InvariantCulture,
                        messageTemplate,
                        values));
        }

        /// <inheritdoc/>
        public void Debug(Exception? exception, string messageTemplate, params object[] values)
        {
            this.Debug(messageTemplate, values);
        }

        /// <inheritdoc/>
        public void Verbose(string messageTemplate, params object[] values)
        {
        }

        /// <inheritdoc/>
        public void Verbose(Exception? exception, string messageTemplate, params object[] values)
        {
        }

        /// <inheritdoc/>
        public void Write(LogEventLevel level, Exception? exception, string messageTemplate, params object[] values)
        {
            if (level == LogEventLevel.Debug)
            {
                this.Debug(messageTemplate, values);
            }
        }
    }
}
