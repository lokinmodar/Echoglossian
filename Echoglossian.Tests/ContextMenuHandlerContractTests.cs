// <copyright file="ContextMenuHandlerContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

using Dalamud.Plugin.Services;

using Echoglossian.NativeUI.AddonHandlers.Common;
using Echoglossian.NativeUI.AddonHandlers.MainMenu;
using Echoglossian.Translators;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Guards the dedicated ContextMenu runtime wiring contract.
/// </summary>
public sealed class ContextMenuHandlerContractTests
{
    /// <summary>
    ///     Ensures changing only the dedicated ContextMenu toggle invalidates
    ///     addon handler registration.
    /// </summary>
    [Fact]
    public void AddonHandlerRegistrationSignature_ChangesWhenContextMenuToggleChanges()
    {
        var disabled = new Config { TranslateContextMenu = false };
        var enabled = new Config { TranslateContextMenu = true };

        Assert.NotEqual(
            Echoglossian.ComputeAddonHandlerRegistrationSignature(disabled),
            Echoglossian.ComputeAddonHandlerRegistrationSignature(enabled));
    }

    /// <summary>
    ///     Ensures addon wiring registers the dedicated ContextMenu handler.
    /// </summary>
    [Fact]
    public void AddonHandlerWiring_RegistersContextMenuHandler()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "Helpers",
            "AddonHandlerWiring.cs"));

        Assert.Contains("(AddonName: \"ContextMenu\"", source, StringComparison.Ordinal);
        Assert.Contains("new ContextMenuHandler(", source, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the dedicated ContextMenu handler uses its own config and
    ///     text-node DB-first runtime.
    /// </summary>
    [Fact]
    public void ContextMenuHandler_UsesDedicatedConfigAndTextNodeRuntime()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "MainMenu",
            "ContextMenuHandler.cs"));

        Assert.Contains("addonName: \"ContextMenu\"", source, StringComparison.Ordinal);
        Assert.Contains("configuration.TranslateContextMenu", source, StringComparison.Ordinal);
        Assert.Contains("configuration.ContextMenuTranslationDisplayMode", source, StringComparison.Ordinal);
        Assert.Contains("useAtkValues: false", source, StringComparison.Ordinal);
        Assert.Contains("useTextNodes: true", source, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the DB-first PreDraw shortcut still treats custom
    ///     ContextMenu hover fallback as tooltip lifetime work.
    /// </summary>
    [Fact]
    public void DbFirstPreDrawShortcut_ConsidersCustomHoverFallbackLifetime()
    {
        var preDraw = typeof(DbFirstGameWindowAddonHandler).GetMethod(
            "OnPreDrawEvent",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var requiresCustomFallback = typeof(DbFirstGameWindowAddonHandler)
            .GetMethod(
                "RequiresCustomHoverTooltipFallback",
                BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(preDraw);
        Assert.NotNull(requiresCustomFallback);
        Assert.True(MethodReferences(preDraw, requiresCustomFallback));
    }

    /// <summary>
    ///     Ensures translation completion invokes an addon-local dedicated
    ///     persistence seam before the generic GameWindow path.
    /// </summary>
    [Fact]
    public async Task PersistResolvedGameWindowPayload_UsesDedicatedPersistenceSeam()
    {
        var handler = new DedicatedPersistenceProbe();
        var payload = CreateTextNodePayload(
            ("3:0", "Dismiss"));

        Assert.True(await handler.Persist(
            new TranslationReuseScope("en", "pt-BR", 0, false),
            payload));

        Assert.Equal(1, handler.DedicatedPersistenceCalls);
    }

    /// <summary>
    ///     Ensures dedicated persistence failure does not redirect the payload to the generic store.
    /// </summary>
    [Fact]
    public async Task PersistResolvedGameWindowPayload_DedicatedFailureDoesNotFallBack()
    {
        var handler = new DedicatedPersistenceProbe { PersistenceSucceeds = false };
        var payload = CreateTextNodePayload(("3:0", "Dismiss"));

        Assert.False(await handler.Persist(
            new TranslationReuseScope("en", "pt-BR", 0, false),
            payload));

        Assert.Equal(1, handler.DedicatedPersistenceCalls);
    }

    /// <summary>
    ///     Ensures dedicated owners reject provider failure markers before admitting persistence.
    /// </summary>
    /// <param name="tooltip">Whether to exercise Tooltip instead of ContextMenu.</param>
    /// <param name="translatedText">The proposed translated node text.</param>
    /// <param name="expected">Whether the payload may be persisted.</param>
    [Theory]
    [InlineData(true, "Dispensar", true)]
    [InlineData(false, "Dispensar", true)]
    [InlineData(true, GoogleTranslator.V2NoTranslationMarker, false)]
    [InlineData(false, GoogleTranslator.V2NoTranslationMarker, false)]
    public void DedicatedHandler_RejectsNonpersistablePayloadBeforeAdmission(bool tooltip, string translatedText, bool expected)
    {
        DbFirstGameWindowAddonHandler handler = tooltip
            ? new TooltipHandler(
                new Config(), null!, null!, null!, null!, static _ => null, static _ => [],
                static (_, _) => throw new InvalidOperationException("Admission checks must not write."))
            : new ContextMenuHandler(
                new Config(), null!, null!, static _ => null,
                static _ => throw new InvalidOperationException("Admission checks must not write."));
        var method = handler.GetType().GetMethod(
            "ShouldPersistNewGameWindowPayload",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(DbFirstGameWindowPayload), typeof(DbFirstGameWindowPayload)],
            modifiers: null);
        Assert.NotNull(method);

        var admitted = Assert.IsType<bool>(method.Invoke(handler,
        [
            CreateTextNodePayload(("3:0", "Dismiss")),
            CreateTextNodePayload(("3:0", translatedText)),
        ]));

        Assert.Equal(expected, admitted);
    }

    /// <summary>
    ///     Ensures the actual dedicated handlers await their injected write and propagate its outcome.
    /// </summary>
    /// <param name="tooltip">Whether to exercise Tooltip instead of ContextMenu.</param>
    /// <param name="status">The dedicated persistence completion status.</param>
    /// <param name="expected">The expected persistence success result.</param>
    [Theory]
    [InlineData(true, "Record saved.", true)]
    [InlineData(false, "Record saved.", true)]
    [InlineData(true, "ErrorSavingData: unavailable", false)]
    [InlineData(false, "ErrorSavingData: unavailable", false)]
    [InlineData(true, "", false)]
    [InlineData(false, "", false)]
    [InlineData(true, " ", false)]
    [InlineData(false, " ", false)]
    [InlineData(true, null, false)]
    [InlineData(false, null, false)]
    [InlineData(true, "No data to save.", false)]
    [InlineData(false, "No data to save.", false)]
    public async Task DedicatedHandler_AwaitsWriteAndPropagatesCompletion(bool tooltip, string? status, bool expected)
    {
        var originalDataManager = Echoglossian.DManager;
        try
        {
            Echoglossian.DManager = DispatchProxy.Create<IDataManager, DataManagerProxy>();
            var write = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0;
            DbFirstGameWindowAddonHandler handler = tooltip
                ? new TooltipHandler(
                    new Config(), null!, null!, null!, null!, static _ => null, static _ => [],
                    (_, _) =>
                    {
                        calls++;
                        return write.Task;
                    })
                : new ContextMenuHandler(
                    new Config(), null!, null!, static _ => null,
                    _ =>
                    {
                        calls++;
                        return write.Task;
                    });
            var completion = handler.PersistResolvedGameWindowPayload(
                new TranslationReuseScope("en", "pt-BR", 0, false),
                CreateTextNodePayload(("3:0", "Dismiss")),
                CreateTextNodePayload(("3:0", "Dispensar")),
                classJobId: null);

            Assert.Equal(1, calls);
            Assert.False(completion.IsCompleted);

            write.SetResult(status!);

            Assert.Equal(expected, await completion.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            Echoglossian.DManager = originalDataManager;
        }
    }

    /// <summary>
    ///     Ensures a stale dedicated completion cannot publish or clear the current operation's cooldown.
    /// </summary>
    /// <param name="tooltip">Whether to exercise Tooltip instead of ContextMenu.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DedicatedHandler_StaleCompletionPreservesNewScope(bool tooltip)
    {
        var originalDataManager = Echoglossian.DManager;
        try
        {
            Echoglossian.DManager = DispatchProxy.Create<IDataManager, DataManagerProxy>();
            var write = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            Func<Action, bool>? publishIfCurrent = null;
            DbFirstGameWindowAddonHandler handler = tooltip
                ? new TooltipHandler(
                    new Config(), null!, null!, null!, null!, static _ => null, static _ => [],
                    (_, guard) =>
                    {
                        publishIfCurrent = guard;
                        return write.Task;
                    })
                : new ContextMenuHandler(new Config(), null!, null!, static _ => null, _ => write.Task);
            var gateField = typeof(DbFirstGameWindowAddonHandler).GetField(
                "retryGate", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(gateField);
            var gate = Assert.IsType<DbFirstSourceRetryGate>(gateField.GetValue(handler));
            var scope = new TranslationReuseScope("en", "pt-BR", 0, false);
            var operation = gate.TransitionTo(scope);
            var persistMethod = typeof(DbFirstGameWindowAddonHandler).GetMethod(
                "PersistResolvedGameWindowPayloadAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(persistMethod);
            var completion = Assert.IsAssignableFrom<Task<bool>>(persistMethod.Invoke(handler,
            [
                scope,
                operation,
                CreateTextNodePayload(("3:0", "Dismiss")),
                CreateTextNodePayload(("3:0", "Dispensar")),
                null,
            ]));
            Assert.False(completion.IsCompleted);

            var next = gate.TransitionTo(new TranslationReuseScope("de", "pt-BR", 0, false));
            var now = DateTime.UtcNow;
            Assert.True(gate.TrySetRetry(next, now.AddSeconds(30)));
            write.SetResult("Record saved.");

            Assert.False(await completion.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(gate.IsCoolingDown(next, now.AddSeconds(29)));
            Assert.False(gate.IsCoolingDown(next, now.AddSeconds(31)));
            if (tooltip)
            {
                Assert.NotNull(publishIfCurrent);
                var publications = 0;
                Assert.False(publishIfCurrent(() => publications++));
                Assert.Equal(0, publications);
            }
        }
        finally
        {
            Echoglossian.DManager = originalDataManager;
        }
    }

    /// <summary>
    ///     Ensures handlers that do not own a dedicated store retain the
    ///     generic GameWindow persistence path.
    /// </summary>
    [Fact]
    public async Task PersistResolvedGameWindowPayload_FallsBackToGenericPersistence()
    {
        var handler = new GenericPersistenceProbe();
        var payload = CreateTextNodePayload(
            ("3:0", "Dismiss"));

        Assert.False(await handler.Persist(
            new TranslationReuseScope("en", "pt-BR", 0, false),
            payload));

        Assert.Equal(1, handler.GenericPersistencePreparations);
    }

    /// <summary>
    ///     Ensures a ContextMenu payload projects ordered translations by the
    ///     numeric visible-node ordinal rather than lexical key order.
    /// </summary>
    [Fact]
    public void ContextMenuHandler_ProjectsTranslationsByNumericNodeOrdinal()
    {
        var originalPayload = CreateTextNodePayload(
            ("3:2", "Second"),
            ("3:10", "Tenth"));

        var projected = TryProjectStoredTranslatedPayload(
            originalPayload,
            "[\"Segundo\",\"Decimo\"]",
            out var translatedPayload);

        Assert.True(projected);
        Assert.Equal("Segundo", translatedPayload.TextNodes["3:2"]);
        Assert.Equal("Decimo", translatedPayload.TextNodes["3:10"]);
    }

    /// <summary>
    ///     Ensures a ContextMenu payload refuses blank stored translations
    ///     before the native apply path can write empty labels.
    /// </summary>
    [Fact]
    public void ContextMenuHandler_RejectsBlankStoredTranslations()
    {
        var originalPayload = CreateTextNodePayload(
            ("3:0", "Dismiss"),
            ("3:1", "Emote"));

        var projected = TryProjectStoredTranslatedPayload(
            originalPayload,
            "[\"\",\"Emote\"]",
            out var translatedPayload);

        Assert.False(projected);
        Assert.True(translatedPayload.IsEmpty);
    }

    /// <summary>
    ///     Ensures stored ContextMenu translations are canonicalized before
    ///     the runtime can consider them for native replacement.
    /// </summary>
    [Fact]
    public void ContextMenuHandler_NormalizesStoredDecoratedTranslations()
    {
        var originalPayload = CreateTextNodePayload(
            ("3:0", "Dismiss"));

        var projected = TryProjectStoredTranslatedPayload(
            originalPayload,
            "[\"\\uE03CDispensar\\u0002\"]",
            out var translatedPayload);

        Assert.True(projected);
        Assert.Equal("Dispensar", translatedPayload.TextNodes["3:0"]);
    }

    /// <summary>
    ///     Ensures captured ContextMenu labels are canonicalized before they
    ///     become the dedicated persistence lookup payload.
    /// </summary>
    [Fact]
    public void ContextMenuHandler_NormalizesCapturedDecoratedLabels()
    {
        var handler = new ContextMenuHandler(
            new Config(),
            null!,
            null!,
            static _ => null,
            static _ => Task.FromResult(string.Empty));
        var method = typeof(ContextMenuHandler).GetMethod(
            "NormalizeCapturedTextNodes",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var capturedTextNodes = new SortedDictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["3:0"] = "\uE03C\u0002Dismiss\u0003",
        };

        var normalizedTextNodes = Assert.IsType<SortedDictionary<string, string>>(
            method.Invoke(handler, [capturedTextNodes]));

        Assert.Equal("Dismiss", normalizedTextNodes["3:0"]);
    }

    /// <summary>
    ///     Finds the repository root from the test output directory.
    /// </summary>
    /// <returns>The repository root directory.</returns>
    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Echoglossian.sln")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root.");
    }

    /// <summary>
    ///     Creates one text-node-only payload for handler contract tests.
    /// </summary>
    /// <param name="textNodes">The visible text nodes to include.</param>
    /// <returns>The constructed payload.</returns>
    private static DbFirstGameWindowPayload CreateTextNodePayload(
        params (string Key, string Value)[] textNodes)
    {
        var nodes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in textNodes)
        {
            nodes[key] = value;
        }

        return new DbFirstGameWindowPayload([], [], nodes);
    }

    /// <summary>
    ///     Invokes the private ContextMenu stored-payload projection helper.
    /// </summary>
    /// <param name="originalPayload">The visible original-facing payload.</param>
    /// <param name="translatedTextsAsText">The stored ordered translations.</param>
    /// <param name="translatedPayload">Receives the projected payload.</param>
    /// <returns>Whether the stored payload is usable.</returns>
    private static bool TryProjectStoredTranslatedPayload(
        DbFirstGameWindowPayload originalPayload,
        string translatedTextsAsText,
        out DbFirstGameWindowPayload translatedPayload)
    {
        var method = typeof(ContextMenuHandler).GetMethod(
            "TryProjectStoredTranslatedPayload",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var arguments = new object?[]
        {
            originalPayload,
            translatedTextsAsText,
            null,
        };
        var result = Assert.IsType<bool>(method.Invoke(null, arguments));
        translatedPayload = Assert.IsType<DbFirstGameWindowPayload>(
            arguments[2]);
        return result;
    }

    /// <summary>
    ///     Determines whether one compiled method references another member.
    /// </summary>
    /// <param name="method">The method body to inspect.</param>
    /// <param name="referencedMember">The expected referenced member.</param>
    /// <returns><c>true</c> when the metadata token is referenced.</returns>
    private static bool MethodReferences(
        MethodInfo method,
        MemberInfo referencedMember)
    {
        var methodBody = method.GetMethodBody()?.GetILAsByteArray();
        if (methodBody == null)
        {
            return false;
        }

        var referencedToken = BitConverter.GetBytes(
            referencedMember.MetadataToken);
        return methodBody.AsSpan().IndexOf(referencedToken) >= 0;
    }

    /// <summary>Provides game data with no loaded repositories for native-free persistence tests.</summary>
    private class DataManagerProxy : DispatchProxy
    {
        private readonly object gameData = RuntimeHelpers.GetUninitializedObject(typeof(Lumina.GameData));

        /// <inheritdoc />
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "get_GameData")
            {
                return this.gameData;
            }

            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    /// <summary>
    ///     Probes dedicated persistence dispatch without requiring a live
    ///     addon or database configuration.
    /// </summary>
    private sealed class DedicatedPersistenceProbe : DbFirstGameWindowAddonHandler
    {
        /// <summary>
        ///     Initializes a new instance of the
        ///     <see cref="DedicatedPersistenceProbe" /> class.
        /// </summary>
        public DedicatedPersistenceProbe()
            : base(
                addonName: "ContextMenuTest",
                config: new Config(),
                hoverTooltipManager: null!,
                translationService: null!,
                enabledSelector: static _ => true,
                useAtkValues: false)
        {
        }

        /// <summary>
        ///     Gets the number of dedicated persistence dispatches observed.
        /// </summary>
        public int DedicatedPersistenceCalls { get; private set; }

        /// <summary>Gets the dedicated persistence outcome supplied by the test.</summary>
        public bool PersistenceSucceeds { get; init; } = true;

        /// <summary>
        ///     Invokes the shared resolved-payload persistence path.
        /// </summary>
        /// <param name="scope">The captured reuse scope.</param>
        /// <param name="payload">The resolved payload.</param>
        /// <returns>The observed persistence result.</returns>
        public Task<bool> Persist(
            TranslationReuseScope scope,
            DbFirstGameWindowPayload payload)
        {
            return this.PersistResolvedGameWindowPayload(
                scope,
                payload,
                payload,
                classJobId: null);
        }

        /// <inheritdoc />
        private protected override Task<bool>? PersistDedicatedPayloadAsync(
            TranslationReuseScope scope,
            DbFirstGameWindowPayload originalPayload,
            DbFirstGameWindowPayload translatedPayload,
            Func<Action, bool> publishIfCurrent)
        {
            this.DedicatedPersistenceCalls++;
            return Task.FromResult(this.PersistenceSucceeds);
        }

        /// <inheritdoc />
        private protected override (
            DbFirstGameWindowPayload OriginalPayload,
            DbFirstGameWindowPayload TranslatedPayload)
            PreparePersistedGameWindowPayload(
                TranslationReuseScope scope,
                DbFirstGameWindowPayload originalPayload,
                DbFirstGameWindowPayload translatedPayload)
        {
            throw new InvalidOperationException("A dedicated persistence owner must not fall back to GameWindow.");
        }
    }

    /// <summary>
    ///     Probes generic fallback persistence without writing a database row.
    /// </summary>
    private sealed class GenericPersistenceProbe : DbFirstGameWindowAddonHandler
    {
        /// <summary>
        ///     Initializes a new instance of the
        ///     <see cref="GenericPersistenceProbe" /> class.
        /// </summary>
        public GenericPersistenceProbe()
            : base(
                addonName: "GenericPersistenceTest",
                config: new Config(),
                hoverTooltipManager: null!,
                translationService: null!,
                enabledSelector: static _ => true,
                useAtkValues: false)
        {
        }

        /// <summary>
        ///     Gets the number of generic persistence preparations observed.
        /// </summary>
        public int GenericPersistencePreparations { get; private set; }

        /// <summary>
        ///     Invokes the shared resolved-payload persistence path.
        /// </summary>
        /// <param name="scope">The captured reuse scope.</param>
        /// <param name="payload">The resolved payload.</param>
        /// <returns>The observed persistence result.</returns>
        public Task<bool> Persist(
            TranslationReuseScope scope,
            DbFirstGameWindowPayload payload)
        {
            return this.PersistResolvedGameWindowPayload(
                scope,
                payload,
                payload,
                classJobId: null);
        }

        /// <inheritdoc />
        private protected override (
            DbFirstGameWindowPayload OriginalPayload,
            DbFirstGameWindowPayload TranslatedPayload)
            PreparePersistedGameWindowPayload(
                TranslationReuseScope scope,
                DbFirstGameWindowPayload originalPayload,
                DbFirstGameWindowPayload translatedPayload)
        {
            this.GenericPersistencePreparations++;
            return base.PreparePersistedGameWindowPayload(
                scope,
                originalPayload,
                translatedPayload);
        }
    }
}
