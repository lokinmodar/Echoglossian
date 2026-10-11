// <copyright file="MockFrameworkDebouncerTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Threading;
using DalaMock.Core.Mocks.DalamudServices;
using FluentAssertions;
using Xunit;

namespace Echoglossian.Mock.Tests;

/// <summary>
/// Covers the public DalaMock implementation of Dalamud framework debouncing.
/// </summary>
public sealed class MockFrameworkDebouncerTests
{
    /// <summary>
    /// Ensures cancellation prevents a pending debounced action from running.
    /// </summary>
    [Fact]
    public void Cancel_prevents_pending_action_from_running()
    {
        var framework = new MockFramework();
        var actionCount = 0;
        var debouncer = framework.CreateDebouncer(
            TimeSpan.FromMilliseconds(20),
            () => actionCount++);

        debouncer.Debounce();
        debouncer.Cancel();
        Thread.Sleep(30);
        framework.FireUpdate();

        actionCount.Should().Be(0);
        debouncer.IsPending.Should().BeFalse();
    }

    /// <summary>
    /// Ensures a pending action runs once after its debounce interval.
    /// </summary>
    [Fact]
    public void Pending_action_runs_once_after_debounce_interval()
    {
        var framework = new MockFramework();
        var actionCount = 0;
        var debouncer = framework.CreateDebouncer(
            TimeSpan.FromMilliseconds(20),
            () => actionCount++);

        debouncer.Debounce();
        Thread.Sleep(30);
        framework.FireUpdate();

        actionCount.Should().Be(1);
        debouncer.IsPending.Should().BeFalse();
    }

    /// <summary>
    /// Ensures a repeated request postpones the action until the final debounce interval elapses.
    /// </summary>
    [Fact]
    public void Repeated_request_postpones_action_until_final_interval()
    {
        var framework = new MockFramework();
        var actionCount = 0;
        var debouncer = framework.CreateDebouncer(
            TimeSpan.FromMilliseconds(50),
            () => actionCount++);

        debouncer.Debounce();
        Thread.Sleep(30);
        debouncer.Debounce();
        Thread.Sleep(30);
        framework.FireUpdate();

        actionCount.Should().Be(0);
        debouncer.IsPending.Should().BeTrue();

        Thread.Sleep(30);
        framework.FireUpdate();

        actionCount.Should().Be(1);
        debouncer.IsPending.Should().BeFalse();
    }
}
