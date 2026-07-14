// <copyright file="NativeDialogueHandlerLifecycleTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.LanguagesHandling;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.NativeUI.AddonHandlers.Common;
using Echoglossian.NativeUI.AddonHandlers.Talk;
using Echoglossian.Translators;

using Xunit;

using PluginEntry = Echoglossian.Echoglossian;

namespace Echoglossian.Tests;

/// <summary>
///     Covers source ownership through real dialogue handler lifecycle and
///     asynchronous completion seams without a live native addon.
/// </summary>
public sealed class NativeDialogueHandlerLifecycleTests : IDisposable
{
    private readonly int originalLanguageInt;
    private readonly Dictionary<int, LanguageInfo> originalLanguages;

    /// <summary>
    ///     Initializes the target language used by dialogue session identities.
    /// </summary>
    public NativeDialogueHandlerLifecycleTests()
    {
        this.originalLanguageInt = PluginEntry.LanguageInt;
        this.originalLanguages = PluginEntry.LangDict;
        PluginEntry.LanguageInt = 81;
        PluginEntry.LangDict = new Dictionary<int, LanguageInfo>
        {
            [81] = new LanguageInfo(
                "pt-BR",
                "Brazilian Portuguese",
                string.Empty,
                string.Empty,
                []),
            [82] = new LanguageInfo(
                "ja",
                "Japanese",
                string.Empty,
                string.Empty,
                []),
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        PluginEntry.LanguageInt = this.originalLanguageInt;
        PluginEntry.LangDict = this.originalLanguages;
    }

    /// <summary>
    ///     Ensures Talk and BattleTalk history cannot cross a source-client
    ///     transition for the same speaker, engine, and target.
    /// </summary>
    [Fact]
    public void DialogueSessionKeys_DifferentSource_Differ()
    {
        var translationService = CreateTranslationService(new ControlledTranslator());
        var talkHandler = CreateTalkHandler(translationService);
        var battleTalkHandler = CreateBattleTalkHandler(translationService);
        var english = new SourceClientLanguage("en", "en");
        var german = new SourceClientLanguage("de", "de");

        Assert.NotEqual(
            talkHandler.BuildDialogueSessionKey("Alphinaud", english),
            talkHandler.BuildDialogueSessionKey("Alphinaud", german));
        Assert.NotEqual(
            battleTalkHandler.BuildDialogueSessionKey("Alphinaud", english),
            battleTalkHandler.BuildDialogueSessionKey("Alphinaud", german));
    }

    /// <summary>
    ///     Ensures the handler's source lifecycle invalidation clears both the
    ///     published overlay and the active replacement state.
    /// </summary>
    [Fact]
    public async Task TalkHandler_SourceInvalidation_ClearsOverlayAndReplacement()
    {
        var translator = new ControlledTranslator();
        var overlayUpdates = 0;
        var overlayClears = 0;
        var handler = CreateTalkHandler(
            CreateTranslationService(translator),
            () => overlayUpdates++,
            () => overlayClears++);
        var english = new SourceClientLanguage("en", "en");
        var german = new SourceClientLanguage("de", "de");

        handler.InvalidateStateForSource(english);
        Assert.True(handler.TryQueueTranslation(
            "Alphinaud",
            "Understood.",
            english,
            out var requestId,
            out var sourceOperation));

        var resolution = handler.ResolveTranslationAsync(
            "Alphinaud",
            "Understood.",
            requestId,
            english,
            sourceOperation);
        await translator.WaitForRequestAsync();
        translator.Complete("Entendido.");
        await resolution;

        Assert.Equal(1, overlayUpdates);
        Assert.True(handler.TryGetCurrentResolvedTranslation(
            english,
            out _,
            out _,
            out _,
            out _));

        handler.InvalidateStateForSource(german);

        Assert.True(overlayClears > 0);
        Assert.False(handler.TryGetCurrentResolvedTranslation(
            german,
            out _,
            out _,
            out _,
            out _));
    }

    /// <summary>
    ///     Ensures an English callback completing after German source
    ///     invalidation cannot republish stale overlay output.
    /// </summary>
    [Fact]
    public async Task TalkHandler_StaleAsyncCallback_CannotRepublish()
    {
        var translator = new ControlledTranslator();
        var overlayUpdates = 0;
        var handler = CreateTalkHandler(
            CreateTranslationService(translator),
            () => overlayUpdates++,
            static () => { });
        var english = new SourceClientLanguage("en", "en");

        handler.InvalidateStateForSource(english);
        Assert.True(handler.TryQueueTranslation(
            "Alphinaud",
            "Understood.",
            english,
            out var requestId,
            out var sourceOperation));
        var resolution = handler.ResolveTranslationAsync(
            "Alphinaud",
            "Understood.",
            requestId,
            english,
            sourceOperation);
        await translator.WaitForRequestAsync();

        handler.InvalidateStateForSource(
            new SourceClientLanguage("de", "de"));
        translator.Complete("Entendido.");
        await resolution;

        Assert.Equal(0, overlayUpdates);
    }

    /// <summary>
    ///     Ensures a Talk completion captured under one target and policy does
    ///     not persist or publish after the same source rebuilds under another
    ///     full operation scope.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task TalkHandler_TargetAndPolicyChange_RejectsInFlightCompletion()
    {
        var translator = new ControlledTranslator();
        var configuration = new Config
        {
            TranslateTalk = true,
            TalkTranslationDisplayMode =
                JournalTranslationDisplayMode.TooltipTranslation,
            Lang = 81,
            ChosenTransEngine = 0,
            TranslateAlreadyTranslatedTexts = false,
        };
        TalkMessage? persistedMessage = null;
        var overlayUpdates = 0;
        var handler = CreateTalkHandler(
            CreateTranslationService(translator),
            () => overlayUpdates++,
            static () => { },
            configuration,
            message =>
            {
                persistedMessage = message;
                return Task.FromResult(string.Empty);
            });
        var english = new SourceClientLanguage("en", "en");

        handler.InvalidateStateForSource(english);
        Assert.True(handler.TryQueueTranslation(
            "Alphinaud",
            "Understood.",
            english,
            out var requestId,
            out var sourceOperation));
        var resolution = handler.ResolveTranslationAsync(
            "Alphinaud",
            "Understood.",
            requestId,
            english,
            sourceOperation);
        await translator.WaitForRequestAsync();

        configuration.Lang = 82;
        configuration.ChosenTransEngine = 8;
        configuration.TranslateAlreadyTranslatedTexts = true;
        handler.InvalidateStateForSource(english);
        translator.Complete("Entendido.");
        await resolution;

        Assert.Null(persistedMessage);
        Assert.Equal(0, overlayUpdates);
        Assert.False(handler.TryGetCurrentResolvedTranslation(
            english,
            out _,
            out _,
            out _,
            out _));
    }

    /// <summary>
    ///     Creates a Talk handler with native-free publication delegates.
    /// </summary>
    /// <param name="translationService">The translation service.</param>
    /// <param name="updateOverlay">The overlay publication callback.</param>
    /// <param name="clearOverlay">The overlay clear callback.</param>
    /// <returns>The configured handler.</returns>
    private static TalkHandler CreateTalkHandler(
        TranslationService translationService,
        Action? updateOverlay = null,
        Action? clearOverlay = null,
        Config? configuration = null,
        Func<TalkMessage, Task<string>>? insertTalkMessageAsync = null)
    {
        return new TalkHandler(
            configuration ?? new Config
            {
                TranslateTalk = true,
                TalkTranslationDisplayMode =
                    JournalTranslationDisplayMode.TooltipTranslation,
                Lang = 81,
            },
            translationService,
            static _ => null,
            insertTalkMessageAsync ?? (static _ => Task.FromResult(string.Empty)),
            (_, _, _) => updateOverlay?.Invoke(),
            clearOverlay ?? (static () => { }),
            static text => text,
            restoreNativeMutation: static () => { });
    }

    /// <summary>
    ///     Creates a BattleTalk handler for session-key assertions.
    /// </summary>
    /// <param name="translationService">The translation service.</param>
    /// <returns>The configured handler.</returns>
    private static BattleTalkHandler CreateBattleTalkHandler(
        TranslationService translationService)
    {
        return new BattleTalkHandler(
            new Config(),
            translationService,
            static _ => null,
            static _ => Task.FromResult(string.Empty),
            static (_, _, _) => { },
            static () => { },
            static text => text);
    }

    /// <summary>
    ///     Creates the test translation service.
    /// </summary>
    /// <param name="translator">The controlled translator.</param>
    /// <returns>The configured service.</returns>
    private static TranslationService CreateTranslationService(
        ITranslator translator)
    {
        return new TranslationService(
            static text => text,
            translator,
            translationEngine: (int)PluginEntry.TransEngines.Google);
    }

    /// <summary>
    ///     Provides deterministic control over one asynchronous translation.
    /// </summary>
    private sealed class ControlledTranslator : ITranslator
    {
        private readonly TaskCompletionSource<string?> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> requestStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        ///     Completes the pending translation.
        /// </summary>
        /// <param name="translatedText">The translated result.</param>
        public void Complete(string translatedText)
        {
            this.completion.TrySetResult(translatedText);
        }

        /// <summary>
        ///     Waits until the handler reaches the translator callback.
        /// </summary>
        /// <returns>A task representing the wait.</returns>
        public Task WaitForRequestAsync()
        {
            return this.requestStarted.Task;
        }

        /// <inheritdoc />
        public string? Translate(
            string text,
            string sourceLanguage,
            string targetLanguage)
        {
            return null;
        }

        /// <inheritdoc />
        public Task<string?> TranslateAsync(
            string text,
            string sourceLanguage,
            string targetLanguage)
        {
            this.requestStarted.TrySetResult(true);
            return this.completion.Task;
        }
    }
}
