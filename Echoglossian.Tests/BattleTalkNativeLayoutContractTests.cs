// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Guards the BattleTalk native-layout restore and sizing contract for
///     regression fixes such as issue #264.
/// </summary>
public sealed class BattleTalkNativeLayoutContractTests
{
    /// <summary>
    ///     Ensures BattleTalk falls back to layout-only cleanup when the game
    ///     repaints live text before the plugin can restore its replacement.
    /// </summary>
    [Fact]
    public void BattleTalkHandler_UsesLayoutFallbackCleanupForGameRepaintedText()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot().FullName,
            "NativeUI",
            "AddonHandlers",
            "Talk",
            "BattleTalkHandler.cs"));

        Assert.Contains(
            "NativeMutationOwnership.TryRestoreWithLayoutFallback(",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures BattleTalk measures wrapped native text from the clean
    ///     text-node width instead of deriving a replacement wrap width from
    ///     the parent or background containers.
    /// </summary>
    [Fact]
    public void BattleTalkHandler_UsesTextNodeBaselineWidthForNativeWrapMeasurement()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot().FullName,
            "NativeUI",
            "AddonHandlers",
            "Talk",
            "BattleTalkHandler.cs"));

        Assert.Contains(
            "var preferredWrapWidth = NativeTextNodeLayoutHelper.ResolvePreferredWrapWidth(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "        textNode);",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "var preferredWrapWidth = NativeTextNodeLayoutHelper.ResolvePreferredWrapWidth(\r\n        textNode,\r\n        parentNode,\r\n        backgroundNode);",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures BattleTalk opts into an explicit fixed-horizontal geometry
    ///     policy so timer anchors and game-owned widths stay untouched while
    ///     translated text grows only vertically.
    /// </summary>
    [Fact]
    public void BattleTalkHandler_UsesFixedHorizontalGeometryPolicyForNativeResize()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot().FullName,
            "NativeUI",
            "AddonHandlers",
            "Talk",
            "BattleTalkHandler.cs"));

        Assert.Contains(
            "preserveHorizontalGeometry: true",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Locates the repository root from the test assembly output path.
    /// </summary>
    /// <returns>The repository root directory.</returns>
    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null &&
               !File.Exists(Path.Combine(directory.FullName, "Echoglossian.sln")))
        {
            directory = directory.Parent;
        }

        return directory ?? throw new DirectoryNotFoundException(
            "Could not locate Echoglossian.sln from the current test output path.");
    }
}
