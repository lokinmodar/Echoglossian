// <copyright file="QuestOperationSourceScopeTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

using Dalamud.Game;
using Dalamud.Plugin.Services;

using Echoglossian.EFCoreSqlite.Models.Journal;
using Echoglossian.LanguagesHandling;
using Echoglossian.NativeUI.AddonHandlers.Quest;
using Echoglossian.Translators;

using Xunit;

using PluginEntry = Echoglossian.Echoglossian;

namespace Echoglossian.Tests;

/// <summary>
/// Covers operation-scoped source identity in the shared quest-handler flow.
/// </summary>
public class QuestOperationSourceScopeTests
{
    /// <summary>
    /// Ensures queued provider and persistence work retains the source identity
    /// captured when the owning operation began.
    /// </summary>
    [Fact]
    public void QueuedOperation_ClientLanguageChanges_UsesCapturedSourceIdentity()
    {
        var originalClientState = PluginEntry.ClientStateInterface;
        var originalDataManager = PluginEntry.DManager;
        var originalLanguageInt = PluginEntry.LanguageInt;
        var originalLanguages = PluginEntry.LangDict;

        try
        {
            PluginEntry.ClientStateInterface =
                TranslationReuseScopeTests.CreateClientState(ClientLanguage.English);
            PluginEntry.DManager = CreateDataManager();
            PluginEntry.LanguageInt = 28;
            PluginEntry.LangDict = CreateTargetLanguages();
            var translator = new RecordingTranslator();
            var recorder = new QuestOperationRecorder();
            var handler = new QuestOperationProbeHandler(
                CreateDependencies(translator, recorder));

            handler.BeginQueuedOperation("A New Adventure");

            PluginEntry.ClientStateInterface =
                TranslationReuseScopeTests.CreateClientState((ClientLanguage)4);
            var translatedText = recorder.ExecuteResolver();

            PluginEntry.ClientStateInterface =
                TranslationReuseScopeTests.CreateClientState(ClientLanguage.French);
            recorder.ExecuteCompletion(translatedText);

            Assert.Equal(
                ("en", "en"),
                (translator.LastSourceLanguage,
                    recorder.InsertedQuestPlate?.OriginalLang));
        }
        finally
        {
            PluginEntry.ClientStateInterface = originalClientState;
            PluginEntry.DManager = originalDataManager;
            PluginEntry.LanguageInt = originalLanguageInt;
            PluginEntry.LangDict = originalLanguages;
        }
    }

    /// <summary>
    /// Ensures an unknown source exits an owning quest operation before any
    /// cache, database, provider, or persistence work begins.
    /// </summary>
    [Fact]
    public void OwningOperation_UnknownSource_ReturnsWithoutWork()
    {
        var originalClientState = PluginEntry.ClientStateInterface;
        var originalDataManager = PluginEntry.DManager;
        var originalLanguageInt = PluginEntry.LanguageInt;
        var originalLanguages = PluginEntry.LangDict;

        try
        {
            PluginEntry.ClientStateInterface =
                TranslationReuseScopeTests.CreateClientState((ClientLanguage)99);
            PluginEntry.DManager = CreateDataManager();
            PluginEntry.LanguageInt = 28;
            PluginEntry.LangDict = CreateTargetLanguages();
            var translator = new RecordingTranslator();
            var recorder = new QuestOperationRecorder();
            var handler = new QuestOperationProbeHandler(
                CreateDependencies(translator, recorder));

            var exception = Record.Exception(
                () => handler.BeginQueuedOperation("Unknown Source Quest"));

            Assert.Null(exception);
            Assert.Equal(
                (0, 0, 0, 0),
                (recorder.LookupCalls,
                    recorder.QueueCalls,
                    translator.TranslateCalls,
                    recorder.InsertCalls));
        }
        finally
        {
            PluginEntry.ClientStateInterface = originalClientState;
            PluginEntry.DManager = originalDataManager;
            PluginEntry.LanguageInt = originalLanguageInt;
            PluginEntry.LangDict = originalLanguages;
        }
    }

    /// <summary>
    /// Ensures the delayed RecommendList lifecycle owner resolves before
    /// scheduling and captures the resulting source identity in its
    /// continuation.
    /// </summary>
    [Fact]
    public void RecommendListDelayedOperation_CapturesSourceInContinuation()
    {
        var delayedOwner = typeof(RecommendListHandler).GetMethod(
            "OnRecommendListEventAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var sourceResolver = typeof(RuntimeLanguageHelper).GetMethod(
            nameof(RuntimeLanguageHelper.TryResolveCurrentSourceLanguage),
            BindingFlags.Static | BindingFlags.Public);
        var delayedClosure = typeof(RecommendListHandler)
            .GetNestedTypes(BindingFlags.NonPublic)
            .SingleOrDefault(type => type
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Any(method => method.Name.Contains(
                    "<OnRecommendListEventAsync>",
                    StringComparison.Ordinal)));

        Assert.NotNull(delayedOwner);
        Assert.NotNull(sourceResolver);
        Assert.True(MethodReferences(delayedOwner!, sourceResolver!));
        Assert.NotNull(delayedClosure);
        Assert.Contains(
            delayedClosure!.GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            field => field.FieldType == typeof(SourceClientLanguage));
    }

    /// <summary>
    /// Creates the target-language dictionary required by quest plate and
    /// translation helpers.
    /// </summary>
    /// <returns>The focused target-language dictionary.</returns>
    private static Dictionary<int, LanguageInfo> CreateTargetLanguages()
    {
        return new Dictionary<int, LanguageInfo>
        {
            [28] = new LanguageInfo(
                "pt-BR",
                "Portuguese",
                string.Empty,
                string.Empty,
                []),
        };
    }

    /// <summary>
    /// Determines whether one compiled method body references another method.
    /// </summary>
    /// <param name="method">The method body to inspect.</param>
    /// <param name="referencedMethod">The expected referenced method.</param>
    /// <returns>True when the metadata token appears in the method body.</returns>
    private static bool MethodReferences(
        MethodInfo method,
        MethodInfo referencedMethod)
    {
        var methodBody = method.GetMethodBody()?.GetILAsByteArray();
        if (methodBody == null)
        {
            return false;
        }

        var referencedToken = BitConverter.GetBytes(referencedMethod.MetadataToken);
        return methodBody.AsSpan().IndexOf(referencedToken) >= 0;
    }

    /// <summary>
    /// Creates the minimal data-manager proxy required by the game-version
    /// field on quest plates.
    /// </summary>
    /// <returns>The configured data-manager proxy.</returns>
    private static IDataManager CreateDataManager()
    {
        var gameDataProperty = typeof(IDataManager).GetProperty("GameData") ??
                               throw new MissingMemberException(
                                   typeof(IDataManager).FullName,
                                   "GameData");
        var gameData = RuntimeHelpers.GetUninitializedObject(
            gameDataProperty.PropertyType);
        var repositoriesProperty = gameDataProperty.PropertyType.GetProperty(
            "Repositories") ??
            throw new MissingMemberException(
                gameDataProperty.PropertyType.FullName,
                "Repositories");
        var repositories = (IDictionary)(Activator.CreateInstance(
            repositoriesProperty.PropertyType) ??
            throw new InvalidOperationException(
                "Could not create a Lumina repository dictionary."));
        var repositoryType = repositoriesProperty.PropertyType
            .GetGenericArguments()[1];
        var repository = RuntimeHelpers.GetUninitializedObject(repositoryType);
        SetMember(repository, "Version", "test-version");
        repositories.Add("ffxiv", repository);
        SetMember(gameData, "Repositories", repositories);

        var dataManager = DispatchProxy.Create<IDataManager, DataManagerProxy>();
        ((DataManagerProxy)(object)dataManager).GameData = gameData;
        return dataManager;
    }

    /// <summary>
    /// Sets a property or compiler-generated backing field on an uninitialized
    /// test object.
    /// </summary>
    /// <param name="instance">The object to update.</param>
    /// <param name="memberName">The member name.</param>
    /// <param name="value">The value to assign.</param>
    private static void SetMember(
        object instance,
        string memberName,
        object value)
    {
        var property = instance.GetType().GetProperty(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property?.SetMethod != null)
        {
            property.SetValue(instance, value);
            return;
        }

        var backingField = instance.GetType().GetField(
            $"<{memberName}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new MissingMemberException(instance.GetType().FullName, memberName);
        backingField.SetValue(instance, value);
    }

    /// <summary>
    /// Creates the dependency bundle used by the quest operation probe.
    /// </summary>
    /// <param name="translator">The provider recorder.</param>
    /// <param name="recorder">The quest operation recorder.</param>
    /// <returns>The configured quest-handler dependencies.</returns>
    private static QuestAddonHandlerDependencies CreateDependencies(
        RecordingTranslator translator,
        QuestOperationRecorder recorder)
    {
        return new QuestAddonHandlerDependencies
        {
            Config = new Config(),
            TranslationService = new TranslationService(text => text, translator),
            FindQuestPlate = recorder.FindQuestPlate,
            FindQuestPlateByName = recorder.FindQuestPlate,
            FindQuestPopupText = _ => null,
            InsertQuestPlate = recorder.InsertQuestPlate,
            InsertQuestPopupTextAsync = _ => Task.FromResult(string.Empty),
            UpdateQuestPlate = static _ => string.Empty,
            UpdateQuestPlateGameVersion = static (_, _) => { },
            NormalizeText = static text => text,
            DisableTranslationAccordingToState = static () => false,
            TryGetQueuedTranslation = static (
                string _,
                out string translatedText) =>
            {
                translatedText = string.Empty;
                return false;
            },
            QueueTranslation = recorder.QueueTranslation,
            QueueTranslationBatch = static (_, _, _, _) => false,
            RequestAcceptedQuestPrefetch = static (_, _) => { },
            RemoveHoverTooltipByPrefix = static _ => { },
            RegisterTranslatedHoverTooltipAddon = null!,
            RegisterTranslatedHoverTooltipTextNode = null!,
            RegisterTranslatedHoverTooltipResNode = null!,
            RegisterTranslatedHoverTooltipBounds = static (
                _,
                _,
                _,
                _,
                _,
                _,
                _,
                _) => { },
        };
    }

    /// <summary>
    /// Minimal quest operation owner that exercises the shared base-helper and
    /// queued-callback contract without requiring a live game addon.
    /// </summary>
    private sealed class QuestOperationProbeHandler : QuestAddonHandlerBase
    {
        private static readonly Type[] OperationScopedCreateQuestPlateTypes =
        [
            typeof(SourceClientLanguage),
            typeof(string),
            typeof(string),
            typeof(string),
        ];

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="QuestOperationProbeHandler" /> class.
        /// </summary>
        /// <param name="dependencies">The shared handler dependencies.</param>
        public QuestOperationProbeHandler(
            QuestAddonHandlerDependencies dependencies)
            : base(dependencies)
        {
        }

        /// <summary>
        /// Begins a lookup and queued translation operation using whichever
        /// base contract is present in the build under test.
        /// </summary>
        /// <param name="questName">The source quest name.</param>
        public void BeginQueuedOperation(string questName)
        {
            if (this.SupportsOperationScopedSource())
            {
                if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
                        out var sourceLanguage))
                {
                    return;
                }

                this.BeginOperation(questName, sourceLanguage);
                return;
            }

            this.BeginLegacyOperation(questName);
        }

        /// <summary>
        /// Determines whether the base helpers require an explicit source
        /// identity.
        /// </summary>
        /// <returns>True when the operation-scoped contract is available.</returns>
        private bool SupportsOperationScopedSource()
        {
            return this.FindBaseMethod(
                    "CreateQuestPlate",
                    OperationScopedCreateQuestPlateTypes) != null;
        }

        /// <summary>
        /// Runs the operation-scoped base-helper flow.
        /// </summary>
        /// <param name="questName">The source quest name.</param>
        /// <param name="sourceLanguage">The captured source identity.</param>
        private void BeginOperation(
            string questName,
            SourceClientLanguage sourceLanguage)
        {
            var questPlate = this.InvokeBaseMethod<QuestPlate>(
                "CreateQuestPlate",
                OperationScopedCreateQuestPlateTypes,
                sourceLanguage,
                questName,
                string.Empty,
                string.Empty);
            _ = this.FindQuestPlateByName(questPlate);
            this.QueueTranslation(
                $"QuestProbe|{questName}",
                () => this.InvokeBaseMethod<string>(
                    "Translate",
                    [typeof(string), typeof(SourceClientLanguage)],
                    questName,
                    sourceLanguage),
                translatedQuestName =>
                {
                    var translatedQuestPlate = this.InvokeBaseMethod<QuestPlate>(
                        "CreateTranslatedQuestPlate",
                        [
                            typeof(SourceClientLanguage),
                            typeof(string),
                            typeof(string),
                            typeof(string),
                            typeof(string),
                            typeof(string),
                        ],
                        sourceLanguage,
                        questName,
                        string.Empty,
                        translatedQuestName,
                        string.Empty,
                        string.Empty);
                    _ = this.InsertQuestPlate(translatedQuestPlate);
                });
        }

        /// <summary>
        /// Runs the rejected helper-scoped base flow for RED verification.
        /// </summary>
        /// <param name="questName">The source quest name.</param>
        private void BeginLegacyOperation(string questName)
        {
            var questPlate = this.InvokeBaseMethod<QuestPlate>(
                "CreateQuestPlate",
                [typeof(string), typeof(string), typeof(string)],
                questName,
                string.Empty,
                string.Empty);
            _ = this.FindQuestPlateByName(questPlate);
            this.QueueTranslation(
                $"QuestProbe|{questName}",
                () => this.InvokeBaseMethod<string>(
                    "Translate",
                    [typeof(string)],
                    questName),
                translatedQuestName =>
                {
                    var translatedQuestPlate = this.InvokeBaseMethod<QuestPlate>(
                        "CreateTranslatedQuestPlate",
                        [
                            typeof(string),
                            typeof(string),
                            typeof(string),
                            typeof(string),
                            typeof(string),
                        ],
                        questName,
                        string.Empty,
                        translatedQuestName,
                        string.Empty,
                        string.Empty);
                    _ = this.InsertQuestPlate(translatedQuestPlate);
                });
        }

        /// <summary>
        /// Finds one protected base method with the specified signature.
        /// </summary>
        /// <param name="name">The method name.</param>
        /// <param name="parameterTypes">The method parameter types.</param>
        /// <returns>The matching base method, if present.</returns>
        private MethodInfo? FindBaseMethod(
            string name,
            Type[] parameterTypes)
        {
            return typeof(QuestAddonHandlerBase).GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);
        }

        /// <summary>
        /// Invokes one protected base helper and preserves its original
        /// exception rather than the reflection wrapper.
        /// </summary>
        /// <typeparam name="T">The helper return type.</typeparam>
        /// <param name="name">The method name.</param>
        /// <param name="parameterTypes">The method parameter types.</param>
        /// <param name="arguments">The helper arguments.</param>
        /// <returns>The helper result.</returns>
        private T InvokeBaseMethod<T>(
            string name,
            Type[] parameterTypes,
            params object?[] arguments)
        {
            var method = this.FindBaseMethod(name, parameterTypes) ??
                         throw new MissingMethodException(
                             typeof(QuestAddonHandlerBase).FullName,
                             name);

            try
            {
                return (T)method.Invoke(this, arguments)!;
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }

    /// <summary>
    /// Supplies the game-data member required by quest plate construction.
    /// </summary>
    private class DataManagerProxy : DispatchProxy
    {
        /// <summary>Gets or sets the fake Lumina game-data instance.</summary>
        public object? GameData { get; set; }

        /// <summary>
        /// Dispatches the game-data getter used by the game-version helper.
        /// </summary>
        /// <param name="targetMethod">The invoked interface method.</param>
        /// <param name="args">The invoked method arguments.</param>
        /// <returns>The configured game-data instance.</returns>
        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
        {
            if (targetMethod?.Name == "get_GameData")
            {
                return this.GameData;
            }

            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    /// <summary>
    /// Records lookup, queue, and persistence boundaries for one quest
    /// operation.
    /// </summary>
    private sealed class QuestOperationRecorder
    {
        private Func<string>? resolver;
        private Action<string>? completion;

        /// <summary>Gets the number of quest lookups.</summary>
        public int LookupCalls { get; private set; }

        /// <summary>Gets the number of queued translations.</summary>
        public int QueueCalls { get; private set; }

        /// <summary>Gets the number of quest inserts.</summary>
        public int InsertCalls { get; private set; }

        /// <summary>Gets the inserted quest plate.</summary>
        public QuestPlate? InsertedQuestPlate { get; private set; }

        /// <summary>
        /// Records a quest lookup.
        /// </summary>
        /// <param name="questPlate">The lookup quest plate.</param>
        /// <returns>No stored quest plate.</returns>
        public QuestPlate? FindQuestPlate(QuestPlate questPlate)
        {
            this.LookupCalls++;
            return null;
        }

        /// <summary>
        /// Records a queued resolver and completion callback.
        /// </summary>
        /// <param name="key">The queue key.</param>
        /// <param name="resolver">The queued resolver.</param>
        /// <param name="onResolved">The completion callback.</param>
        /// <returns>True because the operation was accepted.</returns>
        public bool QueueTranslation(
            string key,
            Func<string> resolver,
            Action<string>? onResolved)
        {
            this.QueueCalls++;
            this.resolver = resolver;
            this.completion = onResolved;
            return true;
        }

        /// <summary>
        /// Executes the captured queued resolver.
        /// </summary>
        /// <returns>The translated payload.</returns>
        public string ExecuteResolver()
        {
            return Assert.IsType<Func<string>>(this.resolver)();
        }

        /// <summary>
        /// Executes the captured completion callback.
        /// </summary>
        /// <param name="translatedText">The translated payload.</param>
        public void ExecuteCompletion(string translatedText)
        {
            Assert.IsType<Action<string>>(this.completion)(translatedText);
        }

        /// <summary>
        /// Records a persisted translated quest plate.
        /// </summary>
        /// <param name="questPlate">The translated quest plate.</param>
        /// <returns>An empty persistence result.</returns>
        public string InsertQuestPlate(QuestPlate questPlate)
        {
            this.InsertCalls++;
            this.InsertedQuestPlate = questPlate;
            return string.Empty;
        }
    }

    /// <summary>
    /// Records provider source input for queued translation assertions.
    /// </summary>
    private sealed class RecordingTranslator : ITranslator
    {
        /// <summary>Gets the most recent provider source code.</summary>
        public string? LastSourceLanguage { get; private set; }

        /// <summary>Gets the number of synchronous provider calls.</summary>
        public int TranslateCalls { get; private set; }

        /// <inheritdoc />
        public string? Translate(
            string text,
            string sourceLanguage,
            string targetLanguage)
        {
            this.TranslateCalls++;
            this.LastSourceLanguage = sourceLanguage;
            return $"translated:{text}";
        }

        /// <inheritdoc />
        public Task<string?> TranslateAsync(
            string text,
            string sourceLanguage,
            string targetLanguage)
        {
            return Task.FromResult<string?>($"translated:{text}");
        }
    }
}
