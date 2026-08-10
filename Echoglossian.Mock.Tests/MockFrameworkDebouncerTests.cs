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

        onTimerElapsed!.Invoke(debouncer, new object[] { 1L });
        framework.FireUpdate();

        actionCount.Should().Be(0);
    }
}
