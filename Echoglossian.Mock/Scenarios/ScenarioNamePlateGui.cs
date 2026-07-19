// <copyright file="ScenarioNamePlateGui.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using DalaMock.Core.Mocks;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Plugin.Services;
using System.Collections.Generic;
using System.Linq;

namespace Echoglossian.Mock.Scenarios;

/// <summary>
/// Provides an actionable <see cref="INamePlateGui"/> implementation for
/// hosted DalaMock tests that need to inspect or dispatch nameplate updates.
/// </summary>
public sealed class ScenarioNamePlateGui : INamePlateGui, IMockService
{
    private INamePlateGui.OnPlateUpdateDelegate? onDataUpdate;
    private INamePlateGui.OnPlateUpdateDelegate? onNamePlateUpdate;
    private INamePlateGui.OnPlateUpdateDelegate? onPostDataUpdate;
    private INamePlateGui.OnPlateUpdateDelegate? onPostNamePlateUpdate;

    /// <inheritdoc/>
    public event INamePlateGui.OnPlateUpdateDelegate? OnNamePlateUpdate
    {
        add => this.onNamePlateUpdate += value;
        remove => this.onNamePlateUpdate -= value;
    }

    /// <inheritdoc/>
    public event INamePlateGui.OnPlateUpdateDelegate? OnPostNamePlateUpdate
    {
        add => this.onPostNamePlateUpdate += value;
        remove => this.onPostNamePlateUpdate -= value;
    }

    /// <inheritdoc/>
    public event INamePlateGui.OnPlateUpdateDelegate? OnDataUpdate
    {
        add => this.onDataUpdate += value;
        remove => this.onDataUpdate -= value;
    }

    /// <inheritdoc/>
    public event INamePlateGui.OnPlateUpdateDelegate? OnPostDataUpdate
    {
        add => this.onPostDataUpdate += value;
        remove => this.onPostDataUpdate -= value;
    }

    /// <inheritdoc/>
    public string ServiceName => "Scenario NamePlate GUI";

    /// <summary>
    /// Gets the number of calls to <see cref="RequestRedraw"/>.
    /// </summary>
    public int RedrawRequestCount { get; private set; }

    /// <summary>
    /// Gets the number of subscribers currently attached to <see cref="OnNamePlateUpdate"/>.
    /// </summary>
    public int NamePlateUpdateSubscriberCount => GetSubscriberCount(this.onNamePlateUpdate);

    /// <inheritdoc/>
    public void RequestRedraw()
    {
        this.RedrawRequestCount++;
    }

    /// <summary>
    /// Dispatches <see cref="OnNamePlateUpdate"/> to current subscribers.
    /// </summary>
    /// <param name="context">The nameplate update context.</param>
    /// <param name="handlers">The update handlers supplied to subscribers.</param>
    /// <returns>The number of callbacks invoked.</returns>
    public int RaiseNamePlateUpdate(
        INamePlateUpdateContext context,
        IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        return InvokeSubscribers(this.onNamePlateUpdate, context, handlers);
    }

    /// <summary>
    /// Dispatches <see cref="OnPostNamePlateUpdate"/> to current subscribers.
    /// </summary>
    /// <param name="context">The nameplate update context.</param>
    /// <param name="handlers">The update handlers supplied to subscribers.</param>
    /// <returns>The number of callbacks invoked.</returns>
    public int RaisePostNamePlateUpdate(
        INamePlateUpdateContext context,
        IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        return InvokeSubscribers(this.onPostNamePlateUpdate, context, handlers);
    }

    /// <summary>
    /// Dispatches <see cref="OnDataUpdate"/> to current subscribers.
    /// </summary>
    /// <param name="context">The nameplate update context.</param>
    /// <param name="handlers">The update handlers supplied to subscribers.</param>
    /// <returns>The number of callbacks invoked.</returns>
    public int RaiseDataUpdate(
        INamePlateUpdateContext context,
        IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        return InvokeSubscribers(this.onDataUpdate, context, handlers);
    }

    /// <summary>
    /// Dispatches <see cref="OnPostDataUpdate"/> to current subscribers.
    /// </summary>
    /// <param name="context">The nameplate update context.</param>
    /// <param name="handlers">The update handlers supplied to subscribers.</param>
    /// <returns>The number of callbacks invoked.</returns>
    public int RaisePostDataUpdate(
        INamePlateUpdateContext context,
        IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        return InvokeSubscribers(this.onPostDataUpdate, context, handlers);
    }

    /// <summary>
    /// Counts callbacks attached to a nameplate update event.
    /// </summary>
    /// <param name="subscribers">The event delegate to inspect.</param>
    /// <returns>The number of subscribers.</returns>
    private static int GetSubscriberCount(INamePlateGui.OnPlateUpdateDelegate? subscribers)
    {
        return subscribers?.GetInvocationList().Length ?? 0;
    }

    /// <summary>
    /// Invokes a snapshot of nameplate update subscribers.
    /// </summary>
    /// <param name="subscribers">The event delegate to dispatch.</param>
    /// <param name="context">The nameplate update context.</param>
    /// <param name="handlers">The update handlers supplied to subscribers.</param>
    /// <returns>The number of callbacks invoked.</returns>
    private static int InvokeSubscribers(
        INamePlateGui.OnPlateUpdateDelegate? subscribers,
        INamePlateUpdateContext context,
        IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        var callbacks = subscribers?.GetInvocationList()
            .Cast<INamePlateGui.OnPlateUpdateDelegate>()
            .ToList() ?? [];

        foreach (var callback in callbacks)
        {
            callback(context, handlers);
        }

        return callbacks.Count;
    }
}
