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
    /// Represents a mutation-aware handler that cannot restore its state.
    /// </summary>
    private sealed class ThrowingHandler :
        IAddonTranslationHandler,
        IPluginUnloadAwareAddonHandler
    {
        /// <inheritdoc/>
        public Dictionary<AddonEvent, IAddonLifecycle.AddonEventDelegate>
            GetEventHandlers()
        {
            return [];
        }

        /// <inheritdoc/>
        public void OnPluginUnload()
        {
            throw new InvalidOperationException("Test restoration failure.");
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

    /// <summary>
    /// Verifies one handler restoration failure does not prevent remaining
    /// mutation-aware handlers from restoring their state.
    /// </summary>
    [Fact]
    public void RestoreMutatedAddonHandlers_ContinuesAfterRestorationFailure()
    {
        var mutatingHandler = new MutatingHandler();
        var handlers = new (string AddonName, IAddonTranslationHandler Handler)[]
        {
            ("Throwing", new ThrowingHandler()),
            ("Mutating", mutatingHandler),
        };

        global::Echoglossian.Echoglossian.RestoreMutatedAddonHandlers(handlers);

        Assert.Equal(1, mutatingHandler.RestoreCount);
    }
}
