// <copyright file="MockRuntimeScenarioTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Autofac;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Gui;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Plugin.Services;
using Echoglossian.Mock.Hosting;
using Echoglossian.Mock.Scenarios;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Echoglossian.Mock.Tests;

/// <summary>
/// Covers test-scenario services that make DalaMock hosted sessions actionable.
/// </summary>
public sealed class MockRuntimeScenarioTests
{
    /// <summary>
    /// Verifies that hosted startup can use a lifecycle replacement that records
    /// plugin registrations and can dispatch callbacks in tests.
    /// </summary>
    /// <returns>A task that completes after the hosted session has been exercised.</returns>
    [Fact]
    public async Task StartAsync_can_drive_registered_addon_lifecycle_events()
    {
        using var fixture = PreviewOwnedHostedSessionFixture.Create(
            config: new global::Echoglossian.Config
            {
                TranslateJournalAccept = true,
            });

        await using var session = await HostedPreviewPluginSessionFactory.StartAsync(
            fixture.Options with
            {
                ServiceReplacements = new Dictionary<Type, Type>
                {
                    [typeof(IAddonLifecycle)] = typeof(ScenarioAddonLifecycle),
                },
            });

        var lifecycle = session.Container.GetContainer().Resolve<ScenarioAddonLifecycle>();
        lifecycle.RegisteredListeners.Should().Contain(listener =>
            listener.EventType == AddonEvent.PreSetup &&
            listener.AddonName == "JournalAccept");

        var received = false;
        lifecycle.RegisterListener(
            AddonEvent.PreDraw,
            "MockSurface",
            (evt, args) =>
            {
                evt.Should().Be(AddonEvent.PreDraw);
                args.Should().NotBeNull();
                received = true;
            });

        lifecycle.Raise(AddonEvent.PreDraw, "MockSurface");

        received.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that hosted startup can use a nameplate replacement that records
    /// plugin subscriptions and can dispatch update callbacks in tests.
    /// </summary>
    /// <returns>A task that completes after nameplate callbacks have been exercised.</returns>
    [Fact]
    public async Task StartAsync_can_drive_nameplate_update_events()
    {
        using var fixture = PreviewOwnedHostedSessionFixture.Create(
            config: new global::Echoglossian.Config
            {
                Translate = true,
                TranslateNamePlates = true,
            });

        await using var session = await HostedPreviewPluginSessionFactory.StartAsync(
            fixture.Options with
            {
                ServiceReplacements = new Dictionary<Type, Type>
                {
                    [typeof(INamePlateGui)] = typeof(ScenarioNamePlateGui),
                },
            });

        var namePlateGui = session.Container.GetContainer().Resolve<ScenarioNamePlateGui>();
        namePlateGui.NamePlateUpdateSubscriberCount.Should().BeGreaterThan(0);

        var received = false;
        namePlateGui.OnNamePlateUpdate += (_, handlers) =>
        {
            handlers.Should().BeEmpty();
            received = true;
        };

        namePlateGui.RaiseNamePlateUpdate(
            context: null!,
            Array.Empty<INamePlateUpdateHandler>());

        received.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that hosted startup can use a game GUI replacement that resolves
    /// addon pointers registered by a test scenario.
    /// </summary>
    /// <returns>A task that completes after the game GUI replacement has been exercised.</returns>
    [Fact]
    public async Task StartAsync_can_resolve_registered_game_gui_addons()
    {
        using var fixture = PreviewOwnedHostedSessionFixture.Create();

        await using var session = await HostedPreviewPluginSessionFactory.StartAsync(
            fixture.Options with
            {
                ServiceReplacements = new Dictionary<Type, Type>
                {
                    [typeof(IGameGui)] = typeof(ScenarioGameGui),
                },
            });

        var gameGui = session.Container.GetContainer().Resolve<ScenarioGameGui>();
        var addonAddress = new IntPtr(0x230);

        gameGui.RegisterAddon("JournalAccept", addonAddress, index: 1);

        gameGui.GetAddonByName("JournalAccept", 1).Should().Be(addonAddress);
        gameGui.GetAddonByName("JournalAccept", 2).Should().Be(IntPtr.Zero);
    }

    /// <summary>
    /// Verifies that a scenario can reproduce the raw and adjusted action ids
    /// that the live game reports for one ActionDetail hover.
    /// </summary>
    [Fact]
    public void ScenarioGameGui_can_publish_distinct_raw_and_resolved_action_ids()
    {
        var gameGui = new ScenarioGameGui();
        HoveredAction? received = null;
        gameGui.HoveredActionChanged += (_, action) => received = action;

        gameGui.SetHoveredAction(
            baseActionId: 20,
            resolvedActionId: 1695,
            detailKind: DetailKind.GeneralAction);

        gameGui.HoveredAction.BaseActionId.Should().Be(20);
        gameGui.HoveredAction.ActionId.Should().Be(1695);
        gameGui.HoveredAction.DetailKind.Should().Be(DetailKind.GeneralAction);
        received.Should().NotBeNull();
        received!.BaseActionId.Should().Be(20);
        received.ActionId.Should().Be(1695);
    }
}
