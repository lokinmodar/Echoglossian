// <copyright file="TranslationActivationCoordinatorTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Tests;

using Xunit;

/// <summary>
/// Covers global translation activation sequencing and transition gating.
/// </summary>
public sealed class TranslationActivationCoordinatorTests
{
    /// <summary>
    /// Verifies that disabling persists the source of truth before runtime cleanup.
    /// </summary>
    [Fact]
    public async Task Disable_PersistsBeforeRuntimeCleanup()
    {
        var config = new Config { Translate = true };
        var events = new List<string>();
        var coordinator = CreateCoordinator(
            config,
            persist: saved => events.Add($"persist:{saved.Translate}"),
            disable: () => events.Add("disable"));

        var outcome = await coordinator.RequestAsync(
            TranslationActivationRequest.Disable);

        Assert.Equal(TranslationActivationResult.Disabled, outcome.Result);
        Assert.False(config.Translate);
        Assert.Equal(
            new[] { "persist:False", "disable" },
            events);
        Assert.Equal(TranslationActivationState.Disabled, coordinator.State);
    }

    /// <summary>
    /// Verifies that enabling validates first, then persists before runtime reactivation.
    /// </summary>
    [Fact]
    public async Task Enable_ValidatesThenPersistsBeforeRuntimeReactivation()
    {
        var config = new Config { Translate = false };
        var events = new List<string>();
        var coordinator = CreateCoordinator(
            config,
            getBlockReason: () =>
            {
                events.Add("validate");
                return TranslationActivationGuard.BlockReason.None;
            },
            persist: saved => events.Add($"persist:{saved.Translate}"),
            enable: () => events.Add("enable"));

        var outcome = await coordinator.RequestAsync(
            TranslationActivationRequest.Enable);

        Assert.Equal(TranslationActivationResult.Enabled, outcome.Result);
        Assert.True(config.Translate);
        Assert.Equal(
            new[] { "validate", "persist:True", "enable" },
            events);
        Assert.Equal(TranslationActivationState.Enabled, coordinator.State);
    }

    /// <summary>
    /// Verifies that an invalid engine configuration leaves persisted state off.
    /// </summary>
    [Fact]
    public async Task Enable_WhenBlocked_DoesNotPersistOrReactivate()
    {
        var config = new Config { Translate = false };
        var persisted = false;
        var enabled = false;
        var coordinator = CreateCoordinator(
            config,
            getBlockReason: () =>
                TranslationActivationGuard.BlockReason.EngineConfigurationIncomplete,
            persist: _ => persisted = true,
            enable: () => enabled = true);

        var outcome = await coordinator.RequestAsync(
            TranslationActivationRequest.Enable);

        Assert.Equal(TranslationActivationResult.Blocked, outcome.Result);
        Assert.Equal(
            TranslationActivationGuard.BlockReason.EngineConfigurationIncomplete,
            outcome.BlockReason);
        Assert.False(config.Translate);
        Assert.False(persisted);
        Assert.False(enabled);
    }

    /// <summary>
    /// Verifies that a second request is rejected while Framework-thread work is pending.
    /// </summary>
    [Fact]
    public async Task RequestAsync_WhileTransitioning_BlocksReentry()
    {
        var config = new Config { Translate = false };
        var scheduled = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = CreateCoordinator(
            config,
            runOnFrameworkThread: async action =>
            {
                scheduled.SetResult(true);
                await release.Task;
                action();
            });

        var firstRequest = coordinator.RequestAsync(
            TranslationActivationRequest.Enable);
        await scheduled.Task;

        Assert.Equal(
            TranslationActivationState.Transitioning,
            coordinator.State);
        var secondOutcome = await coordinator.RequestAsync(
            TranslationActivationRequest.Toggle);
        Assert.Equal(
            TranslationActivationResult.TransitionInProgress,
            secondOutcome.Result);

        release.SetResult(true);
        var firstOutcome = await firstRequest;
        Assert.Equal(TranslationActivationResult.Enabled, firstOutcome.Result);
    }

    /// <summary>
    /// Verifies a failed enable transition rolls persisted state back to off.
    /// </summary>
    [Fact]
    public async Task Enable_WhenRuntimeFails_RollsBackToDisabled()
    {
        var config = new Config { Translate = false };
        var persistedStates = new List<bool>();
        var disableCount = 0;
        var coordinator = CreateCoordinator(
            config,
            persist: saved => persistedStates.Add(saved.Translate),
            disable: () => disableCount++,
            enable: () => throw new InvalidOperationException("test"));

        var outcome = await coordinator.RequestAsync(
            TranslationActivationRequest.Enable);

        Assert.Equal(TranslationActivationResult.Failed, outcome.Result);
        Assert.False(config.Translate);
        Assert.Equal(new[] { true, false }, persistedStates);
        Assert.Equal(1, disableCount);
        Assert.Equal(TranslationActivationState.Disabled, coordinator.State);
    }

    private static TranslationActivationCoordinator CreateCoordinator(
        Config config,
        Func<TranslationActivationGuard.BlockReason>? getBlockReason = null,
        Action<Config>? persist = null,
        Func<Action, Task>? runOnFrameworkThread = null,
        Action? disable = null,
        Action? enable = null)
    {
        return new TranslationActivationCoordinator(
            config,
            getBlockReason ??
            (() => TranslationActivationGuard.BlockReason.None),
            persist ?? (_ => { }),
            runOnFrameworkThread ??
            (action =>
            {
                action();
                return Task.CompletedTask;
            }),
            disable ?? (() => { }),
            enable ?? (() => { }),
            _ => { });
    }
}
