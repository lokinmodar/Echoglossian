// <copyright file="ScenarioGameGui.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using DalaMock.Core.Mocks;
using Dalamud.Game;
using Dalamud.Game.Gui;
using Dalamud.Game.Inventory;
using Dalamud.Game.NativeWrapper;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Echoglossian.Mock.Scenarios;

/// <summary>
/// Provides an actionable <see cref="IGameGui"/> implementation for hosted
/// DalaMock tests that need deterministic addon lookup by name and index.
/// </summary>
public sealed class ScenarioGameGui : IGameGui, IMockService
{
    private readonly Dictionary<(string Name, int Index), nint> addonAddresses = [];
    private ulong hoveredItemId;

    /// <inheritdoc/>
    public event EventHandler<bool>? UiHideToggled;

    /// <inheritdoc/>
    public event EventHandler<ulong>? HoveredItemChanged;

    /// <inheritdoc/>
    public event EventHandler<HoveredAction>? HoveredActionChanged;

    /// <inheritdoc/>
    public event Action<AgentUpdateFlag>? AgentUpdate;

    /// <inheritdoc/>
    public string ServiceName => "Scenario Game GUI";

    /// <inheritdoc/>
    public bool GameUiHidden { get; private set; }

    /// <inheritdoc/>
    public ulong HoveredItem
    {
        get => this.hoveredItemId;
        set
        {
            if (this.hoveredItemId == value)
            {
                return;
            }

            this.hoveredItemId = value;
            this.HoveredItemChanged?.Invoke(this, value);
        }
    }

    /// <inheritdoc/>
    public HoveredAction HoveredAction { get; private set; } = new();

    /// <summary>
    /// Registers an addon address for later lookup through <see cref="GetAddonByName(string, int)"/>.
    /// </summary>
    /// <param name="addonName">The addon name.</param>
    /// <param name="addonAddress">The native addon address to return.</param>
    /// <param name="index">The addon instance index.</param>
    public void RegisterAddon(string addonName, nint addonAddress, int index = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addonName);
        this.addonAddresses[(addonName, index)] = addonAddress;
    }

    /// <summary>
    /// Removes all registered addon addresses.
    /// </summary>
    public void ClearAddons()
    {
        this.addonAddresses.Clear();
    }

    /// <inheritdoc/>
    public bool OpenMapWithMapLink(MapLinkPayload mapLink)
    {
        return false;
    }

    /// <inheritdoc/>
    public bool OpenMapWithMapLink(uint territory, uint map, Vector3 worldPos)
    {
        return false;
    }

    /// <inheritdoc/>
    public bool WorldToScreen(Vector3 worldPos, out Vector2 screenPos)
    {
        screenPos = default;
        return false;
    }

    /// <inheritdoc/>
    public bool WorldToScreen(Vector3 worldPos, out Vector2 screenPos, out bool inView)
    {
        screenPos = default;
        inView = false;
        return false;
    }

    /// <inheritdoc/>
    public bool ScreenToWorld(Vector2 screenPos, out Vector3 worldPos, float rayDistance = 100000)
    {
        worldPos = default;
        return false;
    }

    /// <inheritdoc/>
    UIModulePtr IGameGui.GetUIModule()
    {
        return this.GetUIModule();
    }

    /// <inheritdoc/>
    AtkUnitBasePtr IGameGui.GetAddonByName(string name, int index)
    {
        return this.GetAddonByName(name, index);
    }

    /// <inheritdoc/>
    public unsafe T* GetAddonByName<T>(string name, int index = 1)
        where T : unmanaged
    {
        return (T*)this.GetAddonByName(name, index);
    }

    /// <inheritdoc/>
    public AgentInterfacePtr GetAgentById(int id)
    {
        return default;
    }

    /// <inheritdoc/>
    AgentInterfacePtr IGameGui.FindAgentInterface(string addonName)
    {
        return this.FindAgentInterface(addonName);
    }

    /// <inheritdoc/>
    public AgentInterfacePtr FindAgentInterface(AtkUnitBasePtr addon)
    {
        return default;
    }

    /// <inheritdoc/>
    public nint GetUIModule()
    {
        return 0;
    }

    /// <inheritdoc/>
    public nint GetAddonByName(string name, int index = 1)
    {
        return this.addonAddresses.TryGetValue((name, index), out var addonAddress)
            ? addonAddress
            : 0;
    }

    /// <inheritdoc/>
    public nint FindAgentInterface(string addonName)
    {
        return 0;
    }

    /// <inheritdoc/>
    public unsafe nint FindAgentInterface(void* addon)
    {
        return 0;
    }

    /// <inheritdoc/>
    public nint FindAgentInterface(nint addonPtr)
    {
        return 0;
    }

    /// <inheritdoc/>
    public void SetHoveredItem(uint itemId, InventoryItem.ItemFlags flags)
    {
        this.HoveredItem = flags == InventoryItem.ItemFlags.HighQuality
            ? itemId + 1_000_000UL
            : itemId;
    }

    /// <summary>
    /// Sets the current game UI hidden state and raises the corresponding event.
    /// </summary>
    /// <param name="hidden">Whether the game UI should be treated as hidden.</param>
    public void SetGameUiHidden(bool hidden)
    {
        if (this.GameUiHidden == hidden)
        {
            return;
        }

        this.GameUiHidden = hidden;
        this.UiHideToggled?.Invoke(this, hidden);
    }

    /// <summary>
    /// Sets the hovered action and raises the corresponding event.
    /// </summary>
    /// <param name="hoveredAction">The hovered action value.</param>
    public void SetHoveredAction(HoveredAction hoveredAction)
    {
        if (this.HoveredAction.Equals(hoveredAction))
        {
            return;
        }

        this.HoveredAction = hoveredAction;
        this.HoveredActionChanged?.Invoke(this, hoveredAction);
    }

    /// <summary>
    /// Sets one hovered action using both the raw source identifier received by
    /// the game hover handler and its resolved upgrade identifier.
    /// </summary>
    /// <param name="baseActionId">The raw action identifier supplied by the game.</param>
    /// <param name="resolvedActionId">The automatically adjusted action identifier.</param>
    /// <param name="detailKind">The action family rendered by the detail surface.</param>
    public void SetHoveredAction(
        uint baseActionId,
        uint resolvedActionId,
        DetailKind detailKind)
    {
        this.SetHoveredAction(
            new HoveredAction
            {
                BaseActionId = baseActionId,
                ActionId = resolvedActionId,
                DetailKind = detailKind,
            });
    }

    /// <summary>
    /// Raises the agent update event.
    /// </summary>
    /// <param name="flags">The update flags to dispatch.</param>
    public void RaiseAgentUpdate(AgentUpdateFlag flags)
    {
        this.AgentUpdate?.Invoke(flags);
    }
}
