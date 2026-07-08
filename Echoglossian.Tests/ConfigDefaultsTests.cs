// <copyright file="ConfigDefaultsTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers default values for hidden configuration toggles.
/// </summary>
public class ConfigDefaultsTests
{
    /// <summary>
    ///     Ensures the debug login addon-probe path stays opt-in by default.
    /// </summary>
    [Fact]
    public void EnableDebugLoginAddonProbe_DefaultsToFalse()
    {
        var config = new Config();

        Assert.False(config.EnableDebugLoginAddonProbe);
    }

    /// <summary>
    ///     Ensures the full ToastGui runtime path for supported toasts stays
    ///     opt-in by default.
    /// </summary>
    [Fact]
    public void UseToastGuiRuntimeForSupportedToasts_DefaultsToFalse()
    {
        var config = new Config();

        Assert.False(config.UseToastGuiRuntimeForSupportedToasts);
    }
}
