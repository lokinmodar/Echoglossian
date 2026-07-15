// <copyright file="CharacterWindowHandlerTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Character;

using System.Reflection;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers narrow runtime rules for the main Character window.
/// </summary>
public class CharacterWindowHandlerTests
{
    /// <summary>
    ///     Ensures the root Character handler uses a bounded post-lifecycle
    ///     settling window instead of permanent pre-draw polling.
    /// </summary>
    [Fact]
    public void GetRootCharacterAppliedStateRefreshWindow_ReturnsExpectedDuration()
    {
        var refreshWindow =
            CharacterWindowHandler.GetRootCharacterAppliedStateRefreshWindow();

        Assert.Equal(TimeSpan.FromSeconds(1), refreshWindow);
    }

    /// <summary>
    ///     Ensures the shared Character-family runtime owns its complete
    ///     readable-text traversal instead of inheriting the single-text
    ///     MiniTalk bubble resolver from the generic GameWindow base.
    /// </summary>
    [Fact]
    public void CharacterTextNodeWindowHandlerBase_OverridesTextNodeResolver()
    {
        var resolver = typeof(CharacterTextNodeWindowHandlerBase).GetMethod(
            "ResolveTextNodeAddresses",
            BindingFlags.Instance |
            BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly);

        Assert.NotNull(resolver);
    }
}
