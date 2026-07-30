// <copyright file="ContextMenuHandlerContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;
using System.Reflection;

using Echoglossian.NativeUI.AddonHandlers.Common;
using Echoglossian.NativeUI.AddonHandlers.MainMenu;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Guards the dedicated ContextMenu runtime wiring contract.
/// </summary>
public sealed class ContextMenuHandlerContractTests
{
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
    ///     Ensures translation completion invokes an addon-local dedicated
    ///     persistence seam before the generic GameWindow path.
    /// </summary>
    [Fact]
    public void PersistResolvedGameWindowPayload_UsesDedicatedPersistenceSeam()
    {
        var handler = new DedicatedPersistenceProbe();
        var payload = CreateTextNodePayload(
            ("3:0", "Dismiss"));

        handler.Persist(
            new TranslationReuseScope("en", "pt-BR", 0, false),
            payload);

        Assert.Equal(1, handler.DedicatedPersistenceCalls);
    }

    /// <summary>
    ///     Ensures handlers that do not own a dedicated store retain the
    ///     generic GameWindow persistence path.
    /// </summary>
    [Fact]
    public void PersistResolvedGameWindowPayload_FallsBackToGenericPersistence()
    {
        var handler = new GenericPersistenceProbe();
        var payload = CreateTextNodePayload(
            ("3:0", "Dismiss"));

        handler.Persist(
            new TranslationReuseScope("en", "pt-BR", 0, false),
            payload);

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

        /// <summary>
        ///     Invokes the shared resolved-payload persistence path.
        /// </summary>
        /// <param name="scope">The captured reuse scope.</param>
        /// <param name="payload">The resolved payload.</param>
        public void Persist(
            TranslationReuseScope scope,
            DbFirstGameWindowPayload payload)
        {
            this.PersistResolvedGameWindowPayload(
                scope,
                payload,
                payload,
                classJobId: null);
        }

        /// <inheritdoc />
        private protected override bool TryPersistDedicatedPayload(
            TranslationReuseScope scope,
            DbFirstGameWindowPayload originalPayload,
            DbFirstGameWindowPayload translatedPayload)
        {
            this.DedicatedPersistenceCalls++;
            return true;
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
        public void Persist(
            TranslationReuseScope scope,
            DbFirstGameWindowPayload payload)
        {
            this.PersistResolvedGameWindowPayload(
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
