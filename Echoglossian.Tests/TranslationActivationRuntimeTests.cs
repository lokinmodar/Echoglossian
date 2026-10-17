// <copyright file="TranslationActivationRuntimeTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Tests;

using global::Dalamud.Game.Addon.Lifecycle;
using global::Dalamud.Plugin.Services;

using global::Echoglossian.NativeUI.Handlers;

using Xunit;

/// <summary>
/// Covers mutation-aware restoration during global translation disable.
/// </summary>
public sealed class TranslationActivationRuntimeTests
{
    private sealed class ReadOnlyHandler : IAddonTranslationHandler
    {
        public Dictionary<AddonEvent, IAddonLifecycle.AddonEventDelegate>
            GetEventHandlers()
        {
            return [];
        }
    }

    private sealed class MutatingHandler :
        IAddonTranslationHandler,
        IPluginUnloadAwareAddonHandler
    {
        public int RestoreCount { get; private set; }

        public Dictionary<AddonEvent, IAddonLifecycle.AddonEventDelegate>
            GetEventHandlers()
        {
            return [];
        }

        public void OnPluginUnload()
        {
            this.RestoreCount++;
        }
    }

    /// <summary>
    /// Verifies disable restoration invokes only handlers that declare mutation
    /// ownership.
    /// </summary>
    [Fact]
    public void RestoreMutatedAddonHandlers_RestoresOnlyMutationAwareHandlers()
    {
        var mutatingHandler = new MutatingHandler();
        var handlers = new (string AddonName, IAddonTranslationHandler Handler)[]
        {
            ("ReadOnly", new ReadOnlyHandler()),
            ("Mutating", mutatingHandler),
        };

        global::Echoglossian.Echoglossian.RestoreMutatedAddonHandlers(handlers);

        Assert.Equal(1, mutatingHandler.RestoreCount);
    }
}
