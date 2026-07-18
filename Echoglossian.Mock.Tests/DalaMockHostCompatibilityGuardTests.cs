// <copyright file="DalaMockHostCompatibilityGuardTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using FluentAssertions;
using Xunit;

namespace Echoglossian.Mock.Tests;

/// <summary>
/// Covers the local DalaMock host compatibility guard.
/// </summary>
public class DalaMockHostCompatibilityGuardTests
{
    /// <summary>
    /// Verifies the guard flags the known API-15 framework contract drift.
    /// </summary>
    [Fact]
    public void EvaluateKnownContracts_returns_incompatible_when_Dalamud_requires_CreateDebouncer_and_DalaMock_does_not_advertise_it()
    {
        var result = global::Echoglossian.Mock.DalaMockHostCompatibilityGuard.EvaluateKnownContracts(
            dalamudRequiresCreateDebouncer: true,
            dalamudIdentity: "Dalamud 15.0.2.3",
            dalaMockAdvertisesCreateDebouncer: false,
            dalaMockIdentity: "DalaMock.Core 6.1.7");

        result.IsCompatible.Should().BeFalse();
        result.Message.Should().Contain("CreateDebouncer");
        result.Message.Should().Contain("Dalamud 15.0.2.3");
        result.Message.Should().Contain("DalaMock.Core 6.1.7");
    }

    /// <summary>
    /// Verifies the guard stays permissive when the local Dalamud contract does
    /// not require the newer debouncer API.
    /// </summary>
    [Fact]
    public void EvaluateKnownContracts_returns_compatible_when_Dalamud_does_not_require_CreateDebouncer()
    {
        var result = global::Echoglossian.Mock.DalaMockHostCompatibilityGuard.EvaluateKnownContracts(
            dalamudRequiresCreateDebouncer: false,
            dalamudIdentity: "Dalamud 14.x",
            dalaMockAdvertisesCreateDebouncer: false,
            dalaMockIdentity: "DalaMock.Core 6.1.7");

        result.IsCompatible.Should().BeTrue();
        result.Message.Should().BeNull();
    }

    /// <summary>
    /// Verifies the guard stays green when both sides advertise the debouncer contract.
    /// </summary>
    [Fact]
    public void EvaluateKnownContracts_returns_compatible_when_both_sides_advertise_CreateDebouncer()
    {
        var result = global::Echoglossian.Mock.DalaMockHostCompatibilityGuard.EvaluateKnownContracts(
            dalamudRequiresCreateDebouncer: true,
            dalamudIdentity: "Dalamud 15.0.2.3",
            dalaMockAdvertisesCreateDebouncer: true,
            dalaMockIdentity: "DalaMock.Core future");

        result.IsCompatible.Should().BeTrue();
        result.Message.Should().BeNull();
    }
}
