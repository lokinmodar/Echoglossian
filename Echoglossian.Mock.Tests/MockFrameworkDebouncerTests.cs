// <copyright file="MockFrameworkDebouncerTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Reflection;
using DalaMock.Core.Mocks.DalamudServices;
using FluentAssertions;
using Xunit;

namespace Echoglossian.Mock.Tests;

/// <summary>
/// Covers the DalaMock implementation of Dalamud framework debouncing.
/// </summary>
public sealed class MockFrameworkDebouncerTests
{
    /// <summary>
    /// Ensures a timer callback from a superseded debounce generation does not
    /// queue the action on the next framework tick.
    /// </summary>
    [Fact]
    public void Superseded_timer_generation_does_not_queue_action()
    {
        var framework = new MockFramework();
        var actionCount = 0;
        using var debouncer = framework.CreateDebouncer(
            TimeSpan.FromHours(1),
            () => actionCount++);

        debouncer.Debounce();
        debouncer.Debounce();

        InvokeTimerElapsed(debouncer, 1L);
        framework.FireUpdate();

        actionCount.Should().Be(0);
    }

    /// <summary>
    /// Ensures cancellation still invalidates an action after timer expiry but
    /// before the framework executes the queued callback.
    /// </summary>
    [Fact]
    public void Cancel_after_timer_expiry_invalidates_queued_action()
    {
        var framework = new MockFramework();
        var actionCount = 0;
        using var debouncer = framework.CreateDebouncer(
            TimeSpan.FromHours(1),
            () => actionCount++);

        debouncer.Debounce();
        InvokeTimerElapsed(debouncer, 1L);
        debouncer.Cancel();
        framework.FireUpdate();

        actionCount.Should().Be(0);
    }

    /// <summary>
    /// Ensures re-debouncing after timer expiry invalidates the already queued
    /// callback while retaining the replacement generation.
    /// </summary>
    [Fact]
    public void Redebounce_after_timer_expiry_invalidates_old_queued_action()
    {
        var framework = new MockFramework();
        var actionCount = 0;
        using var debouncer = framework.CreateDebouncer(
            TimeSpan.FromHours(1),
            () => actionCount++);

        debouncer.Debounce();
        InvokeTimerElapsed(debouncer, 1L);
        debouncer.Debounce();
        framework.FireUpdate();

        actionCount.Should().Be(0);

        InvokeTimerElapsed(debouncer, 2L);
        framework.FireUpdate();

        actionCount.Should().Be(1);
    }

    private static void InvokeTimerElapsed(object debouncer, long generation)
    {
        var debouncerType = typeof(MockFramework).GetNestedType(
            "MockDebouncer",
            BindingFlags.NonPublic);
        debouncerType.Should().NotBeNull();
        var onTimerElapsed = debouncerType!.GetMethod(
            "OnTimerElapsed",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(long) },
            modifiers: null);
        onTimerElapsed.Should().NotBeNull();

        onTimerElapsed!.Invoke(debouncer, new object[] { generation });
    }
}
