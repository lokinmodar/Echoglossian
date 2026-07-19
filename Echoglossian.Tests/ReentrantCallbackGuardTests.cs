// <copyright file="ReentrantCallbackGuardTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the callback guard used to avoid recursive Dalamud event
///     processing when a handler mutates the surface that raised the event.
/// </summary>
public sealed class ReentrantCallbackGuardTests
{
    /// <summary>
    ///     Ensures nested callback attempts are rejected until the outer
    ///     callback leaves the protected section.
    /// </summary>
    [Fact]
    public void TryEnter_rejects_nested_entry_until_lease_is_disposed()
    {
        var guard = new ReentrantCallbackGuard();

        var outer = guard.TryEnter();
        Assert.NotNull(outer);

        Assert.Null(guard.TryEnter());

        outer.Dispose();

        var next = guard.TryEnter();
        Assert.NotNull(next);
        next.Dispose();
    }
}
