// <copyright file="ToastGuiCaptureRuntimeTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Game;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text.SeStringHandling;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.LanguagesHandling;
using Echoglossian.NativeUI.AddonHandlers.Toasts;
using Xunit;

using PluginEntry = Echoglossian.Echoglossian;

namespace Echoglossian.Tests;

/// <summary>
///     Covers gating behavior for the legacy ToastGui capture runtime.
/// </summary>
public class ToastGuiCaptureRuntimeTests
{
    /// <summary>
    ///     Ensures an unknown client source cannot consume a stored toast row.
    /// </summary>
    [Fact]
    public void TryCaptureOrQueueToastSource_UnknownClientLanguage_DoesNotReadStoredToast()
    {
        var originalClientState = PluginEntry.ClientStateInterface;
        var originalLanguageInt = PluginEntry.LanguageInt;
        var originalLanguages = PluginEntry.LangDict;

        try
        {
            PluginEntry.ClientStateInterface =
                TranslationReuseScopeTests.CreateClientState((ClientLanguage)99);
            PluginEntry.LanguageInt = 28;
            PluginEntry.LangDict = new Dictionary<int, LanguageInfo>
            {
                [28] = new LanguageInfo(
                    "pt-BR",
                    "Portuguese",
                    string.Empty,
                    string.Empty,
                    []),
            };
            var lookupCalls = 0;
            var handler = new TestAddonTextToastHandler(lookup =>
            {
                lookupCalls++;
                return new ToastMessage(
                    lookup.ToastType,
                    lookup.OriginalToastMessage,
                    lookup.OriginalLang,
                    "Translated toast",
                    lookup.TranslationLang,
                    lookup.TranslationEngine,
                    DateTime.Now,
                    DateTime.Now);
            });

            var handled = handler.TryCaptureSource("Unknown source toast");

            Assert.False(handled);
            Assert.Equal(0, lookupCalls);
        }
        finally
        {
            PluginEntry.ClientStateInterface = originalClientState;
            PluginEntry.LanguageInt = originalLanguageInt;
            PluginEntry.LangDict = originalLanguages;
        }
    }

    /// <summary>
    ///     Ensures the legacy capture-only path stays inactive while supported
    ///     toasts are callback-owned.
    /// </summary>
    [Fact]
    public void HandleNormalToast_DoesNotPrefetch_WhenToastTranslationIsEnabled()
    {
        var config = new Config
        {
            TranslateToast = true,
            TranslateWideTextToast = true,
            UseToastGuiCaptureForSupportedToasts = true,
            UseToastGuiRuntimeForSupportedToasts = false,
        };
        var lookupCalls = 0;
        var runtime = new ToastGuiCaptureRuntime(
            config,
            null!,
            _ =>
            {
                lookupCalls++;
                return null;
            },
            _ => throw new InvalidOperationException("Insert should not run."));
        SeString message = string.Empty;
        ToastOptions options = new();
        var isHandled = false;

        runtime.HandleNormalToast(ref message, ref options, ref isHandled);

        Assert.Equal(0, lookupCalls);
    }

    /// <summary>
    ///     Exposes the shared addon-toast capture operation for source-language
    ///     guard regression coverage.
    /// </summary>
    private sealed class TestAddonTextToastHandler : AddonTextToastHandler
    {
        /// <summary>
        ///     Initializes a new instance of the
        ///     <see cref="TestAddonTextToastHandler" /> class.
        /// </summary>
        /// <param name="findToastMessage">The stored-toast lookup delegate.</param>
        public TestAddonTextToastHandler(
            Func<ToastMessage, ToastMessage?> findToastMessage)
            : base(
                new Config(),
                "_TestToast",
                "Test",
                null!,
                findToastMessage,
                static _ => Task.FromResult(string.Empty),
                static (_, _, _) => { },
                static () => { },
                null!,
                null!,
                static text => text,
                static _ => true,
                static _ => default)
        {
        }

        /// <summary>
        ///     Attempts to consume or queue one toast source line.
        /// </summary>
        /// <param name="originalText">The original toast text.</param>
        /// <returns>Whether the source was handled.</returns>
        public bool TryCaptureSource(string originalText)
        {
            return this.TryCaptureOrQueueToastSource(originalText, "test");
        }
    }
}
