// <copyright file="OverlayPublicationDiagnosticsTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;

using Echoglossian.NativeUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers temporary scope toggles for overlay-publication diagnostics.
/// </summary>
public sealed class OverlayPublicationDiagnosticsTests
{
    /// <summary>
    ///     Ensures stabilized overlay diagnostics stay muted once the
    ///     investigation finishes.
    /// </summary>
    [Fact]
    public void IsScopeEnabled_DisablesNoisyOverlayDiagnostics()
    {
        var method = typeof(OverlayPublicationDiagnostics).GetMethod(
            "IsScopeEnabled",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        Assert.False(Assert.IsType<bool>(method.Invoke(null, ["NamePlateOverlayDiag"])));
        Assert.False(Assert.IsType<bool>(method.Invoke(null, ["TooltipAddonOverlayDiag"])));
    }
}
