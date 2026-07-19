// <copyright file="ScenarioAddonLifecycle.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using DalaMock.Core.Mocks;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Echoglossian.Mock.Scenarios;

/// <summary>
/// Provides an actionable <see cref="IAddonLifecycle"/> implementation for
/// hosted DalaMock tests that need to inspect or drive registered lifecycle callbacks.
/// </summary>
public sealed class ScenarioAddonLifecycle : IAddonLifecycle, IMockService
{
    private readonly object syncRoot = new();
    private readonly List<ScenarioAddonLifecycleRegistration> listeners = [];

    /// <summary>
    /// Gets a snapshot of listeners currently registered with this lifecycle.
    /// </summary>
    public IReadOnlyList<ScenarioAddonLifecycleRegistration> RegisteredListeners
    {
        get
        {
            lock (this.syncRoot)
            {
                return this.listeners.ToList();
            }
        }
    }

    /// <inheritdoc/>
    public string ServiceName => "Scenario Addon Lifecycle";

    /// <inheritdoc/>
    public void RegisterListener(
        AddonEvent eventType,
        IEnumerable<string> addonNames,
        IAddonLifecycle.AddonEventDelegate handler)
    {
        foreach (var addonName in addonNames)
        {
            this.RegisterListener(eventType, addonName, handler);
        }
    }

    /// <inheritdoc/>
    public void RegisterListener(
        AddonEvent eventType,
        string addonName,
        IAddonLifecycle.AddonEventDelegate handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addonName);
        ArgumentNullException.ThrowIfNull(handler);

        lock (this.syncRoot)
        {
            this.listeners.Add(new ScenarioAddonLifecycleRegistration(eventType, addonName, handler));
        }
    }

    /// <inheritdoc/>
    public void RegisterListener(
        AddonEvent eventType,
        IAddonLifecycle.AddonEventDelegate handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (this.syncRoot)
        {
            this.listeners.Add(new ScenarioAddonLifecycleRegistration(eventType, null, handler));
        }
    }

    /// <inheritdoc/>
    public void UnregisterListener(
        AddonEvent eventType,
        IEnumerable<string> addonNames,
        IAddonLifecycle.AddonEventDelegate? handler = null)
    {
        var names = addonNames.ToHashSet(StringComparer.Ordinal);
        this.RemoveListeners(listener =>
            listener.EventType == eventType &&
            listener.AddonName is not null &&
            names.Contains(listener.AddonName) &&
            MatchesHandler(listener, handler));
    }

    /// <inheritdoc/>
    public void UnregisterListener(
        AddonEvent eventType,
        string addonName,
        IAddonLifecycle.AddonEventDelegate? handler = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addonName);
        this.RemoveListeners(listener =>
            listener.EventType == eventType &&
            string.Equals(listener.AddonName, addonName, StringComparison.Ordinal) &&
            MatchesHandler(listener, handler));
    }

    /// <inheritdoc/>
    public void UnregisterListener(
        AddonEvent eventType,
        IAddonLifecycle.AddonEventDelegate? handler = null)
    {
        this.RemoveListeners(listener =>
            listener.EventType == eventType &&
            MatchesHandler(listener, handler));
    }

    /// <inheritdoc/>
    public void UnregisterListener(params IAddonLifecycle.AddonEventDelegate[] handlers)
    {
        var handlerSet = handlers.ToHashSet();
        this.RemoveListeners(listener => handlerSet.Contains(listener.Handler));
    }

    /// <inheritdoc/>
    public IntPtr GetOriginalVirtualTable(IntPtr virtualTableAddress)
    {
        return IntPtr.Zero;
    }

    /// <summary>
    /// Dispatches a lifecycle event with generic args to matching registered listeners.
    /// </summary>
    /// <param name="eventType">The lifecycle event to dispatch.</param>
    /// <param name="addonName">The addon name used to match addon-scoped listeners.</param>
    /// <returns>The number of callbacks invoked.</returns>
    public int Raise(
        AddonEvent eventType,
        string addonName)
    {
        return this.Raise(eventType, addonName, CreateGenericAddonArgs());
    }

    /// <summary>
    /// Creates generic lifecycle args for tests that only need registration and
    /// dispatch semantics, not native addon payloads.
    /// </summary>
    /// <returns>A generic Dalamud lifecycle args instance.</returns>
    private static AddonArgs CreateGenericAddonArgs()
    {
        return (AddonArgs)RuntimeHelpers.GetUninitializedObject(typeof(AddonArgs));
    }

    /// <summary>
    /// Dispatches a lifecycle event to matching registered listeners.
    /// </summary>
    /// <param name="eventType">The lifecycle event to dispatch.</param>
    /// <param name="addonName">The addon name used to match addon-scoped listeners.</param>
    /// <param name="args">The Dalamud lifecycle args supplied to callbacks.</param>
    /// <returns>The number of callbacks invoked.</returns>
    public int Raise(
        AddonEvent eventType,
        string addonName,
        AddonArgs args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addonName);
        ArgumentNullException.ThrowIfNull(args);

        var targets = this.GetMatchingListeners(eventType, addonName);
        foreach (var target in targets)
        {
            target.Handler(eventType, args);
        }

        return targets.Count;
    }

    /// <summary>
    /// Checks whether a registered listener matches an optional callback filter.
    /// </summary>
    /// <param name="listener">The registered listener.</param>
    /// <param name="handler">The optional callback filter.</param>
    /// <returns><see langword="true"/> when the listener matches.</returns>
    private static bool MatchesHandler(
        ScenarioAddonLifecycleRegistration listener,
        IAddonLifecycle.AddonEventDelegate? handler)
    {
        return handler is null || listener.Handler == handler;
    }

    /// <summary>
    /// Gets a snapshot of listeners matching the supplied event and addon.
    /// </summary>
    /// <param name="eventType">The lifecycle event type.</param>
    /// <param name="addonName">The addon name being dispatched.</param>
    /// <returns>The matching listener snapshot.</returns>
    private List<ScenarioAddonLifecycleRegistration> GetMatchingListeners(
        AddonEvent eventType,
        string addonName)
    {
        lock (this.syncRoot)
        {
            return this.listeners
                .Where(listener => listener.Matches(eventType, addonName))
                .ToList();
        }
    }

    /// <summary>
    /// Removes all listeners matching the supplied predicate.
    /// </summary>
    /// <param name="predicate">The listener predicate.</param>
    private void RemoveListeners(Func<ScenarioAddonLifecycleRegistration, bool> predicate)
    {
        lock (this.syncRoot)
        {
            this.listeners.RemoveAll(listener => predicate(listener));
        }
    }
}

/// <summary>
/// Describes one lifecycle listener registered with <see cref="ScenarioAddonLifecycle"/>.
/// </summary>
/// <param name="EventType">The lifecycle event type.</param>
/// <param name="AddonName">The addon name, or <see langword="null"/> for global listeners.</param>
/// <param name="Handler">The registered lifecycle callback.</param>
public sealed record ScenarioAddonLifecycleRegistration(
    AddonEvent EventType,
    string? AddonName,
    IAddonLifecycle.AddonEventDelegate Handler)
{
    /// <summary>
    /// Checks whether this listener should receive an event for the supplied addon.
    /// </summary>
    /// <param name="eventType">The lifecycle event type.</param>
    /// <param name="addonName">The addon name being dispatched.</param>
    /// <returns><see langword="true"/> when the listener matches.</returns>
    public bool Matches(AddonEvent eventType, string addonName)
    {
        return this.EventType == eventType &&
            (this.AddonName is null ||
             string.Equals(this.AddonName, addonName, StringComparison.Ordinal));
    }
}
