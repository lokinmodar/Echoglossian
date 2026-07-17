// <copyright file="PluginConfigSaveScopeTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers scoped configuration-save redirection.
/// </summary>
public sealed class PluginConfigSaveScopeTests
{
    /// <summary>
    /// Ensures an active scope receives saves instead of the live plugin path.
    /// </summary>
    [Fact]
    public void SaveConfig_UsesScopedOverride_WhenPresent()
    {
        var config = new Config();
        var calls = 0;

        using var scope = PluginConfigSaveScope.Push(_ => calls++);

        Echoglossian.SaveConfig(config);

        Assert.Equal(1, calls);
    }

    /// <summary>
    /// Ensures nested scopes use the innermost override and restore the outer
    /// override afterward.
    /// </summary>
    [Fact]
    public void SaveConfig_UsesInnermostScope_AndRestoresOuterScope()
    {
        var config = new Config();
        var calls = new List<string>();

        using var outerScope = PluginConfigSaveScope.Push(
            _ => calls.Add("outer"));
        Echoglossian.SaveConfig(config);

        using (PluginConfigSaveScope.Push(_ => calls.Add("inner")))
        {
            Echoglossian.SaveConfig(config);
        }

        Echoglossian.SaveConfig(config);

        Assert.Equal(new[] { "outer", "inner", "outer" }, calls);
    }

    /// <summary>
    /// Ensures disposing a scope more than once does not corrupt scope state.
    /// </summary>
    [Fact]
    public void ScopeDisposal_IsIdempotent()
    {
        var scope = PluginConfigSaveScope.Push(_ => { });

        scope.Dispose();
        scope.Dispose();

        Assert.False(PluginConfigSaveScope.TrySave(new Config()));
    }

    /// <summary>
    /// Ensures scopes cannot be disposed out of nesting order.
    /// </summary>
    [Fact]
    public async Task ScopeDisposal_RejectsOutOfOrderDisposal()
    {
        await Task.Run(() =>
        {
            var outerScope = PluginConfigSaveScope.Push(_ => { });
            var innerScope = PluginConfigSaveScope.Push(_ => { });

            Assert.Throws<InvalidOperationException>(outerScope.Dispose);

            innerScope.Dispose();
            outerScope.Dispose();
        });
    }

    /// <summary>
    /// Ensures a child flow's override cannot redirect saves in its parent flow.
    /// </summary>
    [Fact]
    public async Task SaveConfig_IsolatesOverridesAcrossAsyncFlows()
    {
        var config = new Config();
        var parentCalls = 0;
        var childCalls = 0;
        var childScopeEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseChild = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var parentScope = PluginConfigSaveScope.Push(
            _ => Interlocked.Increment(ref parentCalls));
        var childTask = Task.Run(async () =>
        {
            using var childScope = PluginConfigSaveScope.Push(
                _ => Interlocked.Increment(ref childCalls));
            childScopeEntered.SetResult(true);
            await releaseChild.Task;
            Echoglossian.SaveConfig(config);
        });

        await childScopeEntered.Task;
        Echoglossian.SaveConfig(config);
        releaseChild.SetResult(true);
        await childTask;

        Assert.Equal(1, parentCalls);
        Assert.Equal(1, childCalls);
    }
}
