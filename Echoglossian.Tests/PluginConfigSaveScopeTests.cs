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
}
