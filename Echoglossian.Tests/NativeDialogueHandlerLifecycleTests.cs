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
            sourceOperation,
            sourceOperation.CancellationToken);
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
            sourceOperation,
            sourceOperation.CancellationToken);
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
            sourceOperation,
            sourceOperation.CancellationToken);
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
    ///     Ensures source retirement cancels an in-progress dialogue write so
    ///     a result cannot commit after its captured scope has changed.
    /// </summary>
    [Fact]
    public async Task TalkHandler_RetiredScope_CancelsPendingPersistence()
    {
        var translator = new ControlledTranslator();
        var persistenceStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = CreateTalkHandler(
            CreateTranslationService(translator),
            insertTalkMessageWithCancellationAsync: async (_, cancellationToken) =>
            {
                persistenceStarted.TrySetResult(true);
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    cancellationObserved.TrySetResult(true);
                    throw;
                }

                return string.Empty;
            });
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
            sourceOperation,
            sourceOperation.CancellationToken);
        await translator.WaitForRequestAsync();
        translator.Complete("Entendido.");
        await persistenceStarted.Task;

        handler.InvalidateStateForSource(german);
        await resolution;

        Assert.True(await cancellationObserved.Task);
    }

    /// <summary>
    ///     Ensures unloading a Talk handler before any source capture safely
    ///     disposes its owned operations.
    /// </summary>
    [Fact]
    public void TalkHandler_FreshUnload_DoesNotThrow()
    {
        var handler = CreateTalkHandler(
            CreateTranslationService(new ControlledTranslator()));

        var unloadException = Record.Exception(handler.OnPluginUnload);

        Assert.Null(unloadException);
    }

    /// <summary>
    ///     Ensures the managed Talk capture callback returns while database
    ///     lookup remains suspended and publishes only after lookup completes.
    /// </summary>
    [Fact]
    public async Task TalkHandler_SuspendedDatabaseLookup_DoesNotBlockCaptureCallback()
    {
        using var lookupStarted = new ManualResetEventSlim();
        using var releaseLookupPrefix = new ManualResetEventSlim();
        using var captureReturned = new ManualResetEventSlim();
        var lookupCompletion = new TaskCompletionSource<TalkMessage?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var overlayPublished = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var overlayUpdates = 0;
        using var handler = CreateTalkHandler(
            CreateTranslationService(new ControlledTranslator()),
            () =>
            {
                overlayUpdates++;
                overlayPublished.TrySetResult(true);
            },
            findTalkMessageAsync: async (_, cancellationToken) =>
            {
                lookupStarted.Set();
                releaseLookupPrefix.Wait();
                return await lookupCompletion.Task.WaitAsync(cancellationToken);
            });
        var english = new SourceClientLanguage("en", "en");
        var captureAccepted = false;
        var captureThread = new Thread(
            () =>
            {
                captureAccepted = handler.TryQueueTranslation(
                    "Alphinaud",
                    "Understood.",
                    english);
                captureReturned.Set();
            });

        handler.InvalidateStateForSource(english);

        try
        {
            captureThread.Start();

            Assert.True(lookupStarted.Wait(TimeSpan.FromSeconds(1)));
            Assert.True(captureReturned.Wait(TimeSpan.FromSeconds(1)));
            Assert.True(captureAccepted);
        }
        finally
        {
            releaseLookupPrefix.Set();
            captureThread.Join(TimeSpan.FromSeconds(1));
        }

        Assert.False(lookupCompletion.Task.IsCompleted);
        Assert.True(handler.IsTranslationInFlight);
        Assert.Equal(0, overlayUpdates);
        Assert.False(handler.TryGetCurrentResolvedTranslation(
            english,
            out _,
            out _,
            out _,
            out _));

        lookupCompletion.TrySetResult(CreateStoredTalkMessage(
            "Alphinaud",
            "Understood.",
            "Entendido."));
        await overlayPublished.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.False(handler.IsTranslationInFlight);
        Assert.Equal(1, overlayUpdates);
        Assert.True(handler.TryGetCurrentResolvedTranslation(
            english,
            out _,
            out var translatedText,
            out _,
            out _));
        Assert.Equal("Entendido.", translatedText);
    }

    /// <summary>
    ///     Ensures a suspended database result for line A cannot publish over
    ///     the newer managed state captured for line B.
    /// </summary>
    [Fact]
    public async Task TalkHandler_StaleDatabaseResult_CannotReplaceNewerManagedState()
    {
        var lookupStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var lookupCompletion = new TaskCompletionSource<TalkMessage?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var overlayUpdates = 0;
        using var handler = CreateTalkHandler(
            CreateTranslationService(new ControlledTranslator()),
            () => overlayUpdates++,
            findTalkMessageAsync: async (_, cancellationToken) =>
            {
                lookupStarted.TrySetResult(true);
                return await lookupCompletion.Task.WaitAsync(cancellationToken);
            });
        var english = new SourceClientLanguage("en", "en");

        handler.InvalidateStateForSource(english);
        Assert.True(handler.TryQueueTranslation(
            "Alphinaud",
            "Line A",
            english,
            out var lineARequestId,
            out var lineAOperation));
        var lineAResolution = handler.ResolveTranslationAsync(
            "Alphinaud",
            "Line A",
            lineARequestId,
            english,
            lineAOperation,
            lineAOperation.CancellationToken);
        await lookupStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.True(handler.TryQueueTranslation(
            "Alphinaud",
            "Line B",
            english,
            out _,
            out _));

        lookupCompletion.TrySetResult(CreateStoredTalkMessage(
            "Alphinaud",
            "Line A",
            "Linha A"));
        await lineAResolution;

        Assert.True(handler.IsTranslationInFlight);
        Assert.Equal(0, overlayUpdates);
        Assert.False(handler.TryGetCurrentResolvedTranslation(
            english,
            out _,
            out _,
            out _,
            out _));
    }

    /// <summary>
    ///     Ensures the first fresh Talk line forwards resolved interlocutor
    ///     hints to a context-aware translator without requiring prior turns.
    /// </summary>
    [Fact]
    public async Task TalkHandler_FirstFreshLine_PassesInterlocutorHintsToContextAwareTranslator()
    {
        var translator = new ContextAwareRecordingTranslator();
        using var handler = CreateTalkHandler(
            CreateTranslationService(translator),
            resolveInterlocutorHintsAsync: static (_, _, _, _) =>
                Task.FromResult(CreateInterlocutorHints()));
        var english = new SourceClientLanguage("en", "en");

        handler.InvalidateStateForSource(english);
        Assert.True(handler.TryQueueTranslation("Krile", "Stay close.", english));

        var context = await translator.WaitForDialogueContextAsync();

        Assert.Empty(context.PriorTurns);
        Assert.Equal("Alphinaud", context.AddresseeHint);
        Assert.Equal("male", context.AddresseeGenderHint);
    }

    /// <summary>
    ///     Ensures a retired Talk source generation cancels the pending
    ///     interlocutor-hint resolution before it can call the translator.
    /// </summary>
    [Fact]
    public async Task TalkHandler_RetiredScope_CancelsPendingInterlocutorHintResolution()
    {
        var resolutionStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = CreateTalkHandler(
            CreateTranslationService(new ControlledTranslator()),
            resolveInterlocutorHintsAsync: async (_, _, _, cancellationToken) =>
            {
                resolutionStarted.TrySetResult(true);
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    cancellationObserved.TrySetResult(true);
                    throw;
                }

                return CreateInterlocutorHints();
            });
        var english = new SourceClientLanguage("en", "en");
        var german = new SourceClientLanguage("de", "de");

        handler.InvalidateStateForSource(english);
        Assert.True(handler.TryQueueTranslation("Krile", "Stay close.", english));
        await resolutionStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        handler.InvalidateStateForSource(german);

        Assert.True(await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(1)));
    }

    /// <summary>
    ///     Ensures a reusable Talk database row bypasses interlocutor-hint
    ///     resolution entirely.
    /// </summary>
    [Fact]
    public async Task TalkHandler_ReusedDatabaseTranslation_DoesNotResolveInterlocutorHints()
    {
        var resolverCalls = 0;
        var overlayPublished = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = CreateTalkHandler(
            CreateTranslationService(new ControlledTranslator()),
            () => overlayPublished.TrySetResult(true),
            findTalkMessageAsync: static (_, _) => Task.FromResult<TalkMessage?>(
                CreateStoredTalkMessage("Krile", "Stay close.", "Fique perto.")),
            resolveInterlocutorHintsAsync: (_, _, _, _) =>
            {
                resolverCalls++;
                return Task.FromResult(CreateInterlocutorHints());
            });
        var english = new SourceClientLanguage("en", "en");

        handler.InvalidateStateForSource(english);
        Assert.True(handler.TryQueueTranslation("Krile", "Stay close.", english));
        await overlayPublished.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(0, resolverCalls);
    }

    /// <summary>
    ///     Ensures the managed BattleTalk capture callback returns while database
    ///     lookup remains suspended and publishes only after lookup completes.
    /// </summary>
    [Fact]
    public async Task BattleTalkHandler_SuspendedDatabaseLookup_DoesNotBlockCaptureCallback()
    {
        using var lookupStarted = new ManualResetEventSlim();
        using var releaseLookupPrefix = new ManualResetEventSlim();
        using var captureReturned = new ManualResetEventSlim();
        var lookupCompletion = new TaskCompletionSource<BattleTalkMessage?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var overlayPublished = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var overlayUpdates = 0;
        using var handler = CreateBattleTalkHandler(
            CreateTranslationService(new ControlledTranslator()),
            () =>
            {
                overlayUpdates++;
                overlayPublished.TrySetResult(true);
            },
            findBattleTalkMessageAsync: async (_, cancellationToken) =>
            {
                lookupStarted.Set();
                releaseLookupPrefix.Wait();
                return await lookupCompletion.Task.WaitAsync(cancellationToken);
            });
        var english = new SourceClientLanguage("en", "en");
        var captureAccepted = false;
        var captureThread = new Thread(
            () =>
            {
                captureAccepted = handler.TryQueueTranslation(
                    "Alphinaud",
                    "Understood.",
                    english);
                captureReturned.Set();
            });

        handler.InvalidateStateForSource(english);

        try
        {
            captureThread.Start();

            Assert.True(lookupStarted.Wait(TimeSpan.FromSeconds(1)));
            Assert.True(captureReturned.Wait(TimeSpan.FromSeconds(1)));
            Assert.True(captureAccepted);
        }
        finally
        {
            releaseLookupPrefix.Set();
            captureThread.Join(TimeSpan.FromSeconds(1));
        }

        Assert.False(lookupCompletion.Task.IsCompleted);
        Assert.True(handler.IsTranslationInFlight);
        Assert.Equal(0, overlayUpdates);
        Assert.False(handler.TryGetCurrentResolvedTranslation(
            english,
            out _,
            out _,
            out _,
            out _));

        lookupCompletion.TrySetResult(CreateStoredBattleTalkMessage(
            "Alphinaud",
            "Understood.",
            "Entendido."));
        await overlayPublished.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.False(handler.IsTranslationInFlight);
        Assert.Equal(1, overlayUpdates);
        Assert.True(handler.TryGetCurrentResolvedTranslation(
            english,
            out _,
            out var translatedText,
            out _,
            out _));
        Assert.Equal("Entendido.", translatedText);
    }

    /// <summary>
    ///     Ensures a suspended BattleTalk database result for line A cannot
    ///     publish over the newer managed state captured for line B.
    /// </summary>
    [Fact]
    public async Task BattleTalkHandler_StaleDatabaseResult_CannotReplaceNewerManagedState()
    {
        var lookupStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var lookupCompletion = new TaskCompletionSource<BattleTalkMessage?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var overlayUpdates = 0;
        using var handler = CreateBattleTalkHandler(
            CreateTranslationService(new ControlledTranslator()),
            () => overlayUpdates++,
            findBattleTalkMessageAsync: async (_, cancellationToken) =>
            {
                lookupStarted.TrySetResult(true);
                return await lookupCompletion.Task.WaitAsync(cancellationToken);
            });
        var english = new SourceClientLanguage("en", "en");

        handler.InvalidateStateForSource(english);
        Assert.True(handler.TryQueueTranslation(
            "Alphinaud",
            "Line A",
            english,
            out var lineARequestId,
            out var lineAOperation));
        var lineAResolution = handler.ResolveTranslationAsync(
            "Alphinaud",
            "Line A",
            lineARequestId,
            english,
            lineAOperation,
            lineAOperation.CancellationToken);
        await lookupStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.True(handler.TryQueueTranslation(
            "Alphinaud",
            "Line B",
            english,
            out _,
            out _));

        lookupCompletion.TrySetResult(CreateStoredBattleTalkMessage(
            "Alphinaud",
            "Line A",
            "Linha A"));
        await lineAResolution;

        Assert.True(handler.IsTranslationInFlight);
        Assert.Equal(0, overlayUpdates);
        Assert.False(handler.TryGetCurrentResolvedTranslation(
            english,
            out _,
            out _,
            out _,
            out _));
    }

    /// <summary>
    ///     Ensures the first fresh BattleTalk line forwards resolved
    ///     interlocutor hints to a context-aware translator without requiring
    ///     prior turns.
    /// </summary>
    [Fact]
    public async Task BattleTalkHandler_FirstFreshLine_PassesInterlocutorHintsToContextAwareTranslator()
    {
        var translator = new ContextAwareRecordingTranslator();
        using var handler = CreateBattleTalkHandler(
            CreateTranslationService(translator),
            resolveInterlocutorHintsAsync: static (_, _, _, _) =>
                Task.FromResult(CreateInterlocutorHints()));
        var english = new SourceClientLanguage("en", "en");

        handler.InvalidateStateForSource(english);
        Assert.True(handler.TryQueueTranslation("Krile", "Stay close.", english));

        var context = await translator.WaitForDialogueContextAsync();

        Assert.Empty(context.PriorTurns);
        Assert.Equal("Alphinaud", context.AddresseeHint);
        Assert.Equal("male", context.AddresseeGenderHint);
    }

    /// <summary>
    ///     Ensures a reusable BattleTalk database row bypasses
    ///     interlocutor-hint resolution entirely.
    /// </summary>
    [Fact]
    public async Task BattleTalkHandler_ReusedDatabaseTranslation_DoesNotResolveInterlocutorHints()
    {
        var resolverCalls = 0;
        var overlayPublished = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = CreateBattleTalkHandler(
            CreateTranslationService(new ControlledTranslator()),
            () => overlayPublished.TrySetResult(true),
            findBattleTalkMessageAsync: static (_, _) =>
                Task.FromResult<BattleTalkMessage?>(CreateStoredBattleTalkMessage(
                    "Krile",
                    "Stay close.",
                    "Fique perto.")),
            resolveInterlocutorHintsAsync: (_, _, _, _) =>
            {
                resolverCalls++;
                return Task.FromResult(CreateInterlocutorHints());
            });
        var english = new SourceClientLanguage("en", "en");

        handler.InvalidateStateForSource(english);
        Assert.True(handler.TryQueueTranslation("Krile", "Stay close.", english));
        await overlayPublished.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(0, resolverCalls);
    }

    /// <summary>
    ///     Ensures a BattleTalk callback completing after source invalidation
    ///     cannot republish stale overlay output.
    /// </summary>
    [Fact]
    public async Task BattleTalkHandler_StaleAsyncCallback_CannotRepublish()
    {
        var translator = new ControlledTranslator();
        var overlayUpdates = 0;
        using var handler = CreateBattleTalkHandler(
            CreateTranslationService(translator),
            () => overlayUpdates++);
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
            sourceOperation,
            sourceOperation.CancellationToken);
        await translator.WaitForRequestAsync();

        handler.InvalidateStateForSource(
            new SourceClientLanguage("de", "de"));
        translator.Complete("Entendido.");
        await resolution;

        Assert.Equal(0, overlayUpdates);
    }

    /// <summary>
    ///     Ensures a BattleTalk completion captured under one target and
    ///     policy does not persist or publish after the same source rebuilds
    ///     under another full operation scope.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task BattleTalkHandler_TargetAndPolicyChange_RejectsInFlightCompletion()
    {
        var translator = new ControlledTranslator();
        var configuration = new Config
        {
            TranslateBattleTalk = true,
            BattleTalkTranslationDisplayMode =
                JournalTranslationDisplayMode.TooltipTranslation,
            Lang = 81,
            ChosenTransEngine = 0,
            TranslateAlreadyTranslatedTexts = false,
        };
        BattleTalkMessage? persistedMessage = null;
        var overlayUpdates = 0;
        using var handler = CreateBattleTalkHandler(
            CreateTranslationService(translator),
            () => overlayUpdates++,
            configuration,
            insertBattleTalkMessageAsync: (message, _) =>
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
            sourceOperation,
            sourceOperation.CancellationToken);
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
    ///     Ensures source retirement cancels an in-progress BattleTalk write
    ///     so a result cannot commit after its captured scope has changed.
    /// </summary>
    [Fact]
    public async Task BattleTalkHandler_RetiredScope_CancelsPendingPersistence()
    {
        var translator = new ControlledTranslator();
        var persistenceStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = CreateBattleTalkHandler(
            CreateTranslationService(translator),
            insertBattleTalkMessageAsync: async (_, cancellationToken) =>
            {
                persistenceStarted.TrySetResult(true);
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    cancellationObserved.TrySetResult(true);
                    throw;
                }

                return string.Empty;
            });
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
            sourceOperation,
            sourceOperation.CancellationToken);
        await translator.WaitForRequestAsync();
        translator.Complete("Entendido.");
        await persistenceStarted.Task;

        handler.InvalidateStateForSource(german);
        await resolution;

        Assert.True(await cancellationObserved.Task);
    }

    /// <summary>
    ///     Ensures a BattleTalk speaker-name translation failure does not discard
    ///     the translated dialogue body.
    /// </summary>
    [Fact]
    public async Task BattleTalkHandler_SpeakerTranslationFailure_PublishesTranslatedBody()
    {
        var configuration = new Config
        {
            TranslateBattleTalk = true,
            BattleTalkTranslationDisplayMode =
                JournalTranslationDisplayMode.TooltipTranslation,
            TranslateBattleTalkNpcNames = true,
            Lang = 81,
        };
        BattleTalkMessage? insertedMessage = null;
        var overlayPublished = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = CreateBattleTalkHandler(
            CreateTranslationService(new BodySuccessSpeakerFailureTranslator()),
            () => overlayPublished.TrySetResult(true),
            configuration,
            insertBattleTalkMessageAsync: (message, _) =>
            {
                insertedMessage = message;
                return Task.FromResult(string.Empty);
            });
        var english = new SourceClientLanguage("en", "en");

        handler.InvalidateStateForSource(english);
        Assert.True(handler.TryQueueTranslation(
            "Alphinaud",
            "Understood.",
            english));

        await overlayPublished.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.NotNull(insertedMessage);
        Assert.Equal("Entendido.", insertedMessage.TranslatedBattleTalkMessage);
        Assert.Equal(string.Empty, insertedMessage.TranslatedSenderName);
        Assert.True(handler.TryGetCurrentResolvedTranslation(
            english,
            out var translatedName,
            out var translatedText,
            out _,
            out _));
        Assert.Equal(string.Empty, translatedName);
        Assert.Equal("Entendido.", translatedText);
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
        Func<TalkMessage, Task<string>>? insertTalkMessageAsync = null,
        Func<TalkMessage, CancellationToken, Task<string>>?
            insertTalkMessageWithCancellationAsync = null,
        Func<TalkMessage, CancellationToken, Task<TalkMessage?>>?
            findTalkMessageAsync = null,
        Func<string, string, SourceClientLanguage, CancellationToken,
            Task<DialogueInterlocutorHints>>? resolveInterlocutorHintsAsync = null)
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
            findTalkMessageAsync ??
            (static (_, _) => Task.FromResult<TalkMessage?>(null)),
            insertTalkMessageWithCancellationAsync ??
            ((message, _) => insertTalkMessageAsync?.Invoke(message) ??
                             Task.FromResult(string.Empty)),
            (_, _, _) => updateOverlay?.Invoke(),
            clearOverlay ?? (static () => { }),
            static text => text,
            resolveInterlocutorHintsAsync ??
            (static (_, _, _, _) => Task.FromResult(default(DialogueInterlocutorHints))),
            restoreNativeMutation: static () => { });
    }

    /// <summary>
    ///     Creates one stored Talk translation for async lookup tests.
    /// </summary>
    /// <param name="originalName">The original speaker name.</param>
    /// <param name="originalText">The original dialogue text.</param>
    /// <param name="translatedText">The translated dialogue text.</param>
    /// <returns>The stored Talk row.</returns>
    private static TalkMessage CreateStoredTalkMessage(
        string originalName,
        string originalText,
        string translatedText)
    {
        return new TalkMessage(
            originalName,
            originalText,
            "en",
            "en",
            string.Empty,
            translatedText,
            "pt-BR",
            (int)PluginEntry.TransEngines.Google,
            rtlLangTranslationImageData: null,
            DateTime.UtcNow,
            DateTime.UtcNow);
    }

    /// <summary>
    ///     Creates a BattleTalk handler with native-free publication delegates.
    /// </summary>
    /// <param name="translationService">The translation service.</param>
    /// <param name="updateOverlay">The overlay publication callback.</param>
    /// <param name="configuration">The optional handler configuration.</param>
    /// <param name="insertBattleTalkMessageAsync">The persistence callback.</param>
    /// <param name="findBattleTalkMessageAsync">The asynchronous lookup callback.</param>
    /// <returns>The configured handler.</returns>
    private static BattleTalkHandler CreateBattleTalkHandler(
        TranslationService translationService,
        Action? updateOverlay = null,
        Config? configuration = null,
        Func<BattleTalkMessage, CancellationToken, Task<string>>?
            insertBattleTalkMessageAsync = null,
        Func<BattleTalkMessage, CancellationToken, Task<BattleTalkMessage?>>?
            findBattleTalkMessageAsync = null,
        Func<string, string, SourceClientLanguage, CancellationToken,
            Task<DialogueInterlocutorHints>>? resolveInterlocutorHintsAsync = null)
    {
        return new BattleTalkHandler(
            configuration ?? new Config
            {
                TranslateBattleTalk = true,
                BattleTalkTranslationDisplayMode =
                    JournalTranslationDisplayMode.TooltipTranslation,
                Lang = 81,
            },
            translationService,
            findBattleTalkMessageAsync ??
            (static (_, _) => Task.FromResult<BattleTalkMessage?>(null)),
            insertBattleTalkMessageAsync ??
            (static (_, _) => Task.FromResult(string.Empty)),
            (_, _, _) => updateOverlay?.Invoke(),
            static () => { },
            static text => text,
            resolveInterlocutorHintsAsync ??
            (static (_, _, _, _) => Task.FromResult(default(DialogueInterlocutorHints))));
    }

    /// <summary>
    ///     Creates one stored BattleTalk translation for async lookup tests.
    /// </summary>
    /// <param name="originalName">The original speaker name.</param>
    /// <param name="originalText">The original dialogue text.</param>
    /// <param name="translatedText">The translated dialogue text.</param>
    /// <returns>The stored BattleTalk row.</returns>
    private static BattleTalkMessage CreateStoredBattleTalkMessage(
        string originalName,
        string originalText,
        string translatedText)
    {
        return new BattleTalkMessage(
            originalName,
            originalText,
            "en",
            "en",
            string.Empty,
            translatedText,
            "pt-BR",
            (int)PluginEntry.TransEngines.Google,
            rtlLangTranslationImageData: null,
            DateTime.UtcNow,
            DateTime.UtcNow);
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
    ///     Creates the fixed fresh-line hints used by handler context tests.
    /// </summary>
    /// <returns>The resolved interlocutor hints.</returns>
    private static DialogueInterlocutorHints CreateInterlocutorHints()
    {
        return new DialogueInterlocutorHints(
            "npc",
            "female",
            "Alphinaud",
            "npc",
            "male",
            "QuestSheetDerived",
            "2");
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

    /// <summary>
    ///     Records the dialogue context passed to the context-aware translation
    ///     contract for a fresh line.
    /// </summary>
    private sealed class ContextAwareRecordingTranslator :
        ITranslator,
        IDialogueContextAwareTranslator
    {
        private readonly TaskCompletionSource<DialogueTranslationContext> context = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

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
            return Task.FromResult<string?>(null);
        }

        /// <inheritdoc />
        public Task<string?> TranslateAsync(
            string text,
            string sourceLanguage,
            string targetLanguage,
            DialogueTranslationContext dialogueContext)
        {
            this.context.TrySetResult(dialogueContext);
            return Task.FromResult<string?>("Fique perto.");
        }

        /// <summary>
        ///     Waits for the first context-aware dialogue translation request.
        /// </summary>
        /// <returns>The captured dialogue context.</returns>
        public Task<DialogueTranslationContext> WaitForDialogueContextAsync()
        {
            return this.context.Task;
        }
    }

    /// <summary>
    ///     Translates dialogue text while failing speaker-name translation.
    /// </summary>
    private sealed class BodySuccessSpeakerFailureTranslator : ITranslator
    {
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
            return text == "Alphinaud"
                ? Task.FromException<string?>(
                    new InvalidOperationException("Speaker translation failed."))
                : Task.FromResult<string?>("Entendido.");
        }
    }
}
