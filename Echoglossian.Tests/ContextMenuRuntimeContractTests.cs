// <copyright file="ContextMenuRuntimeContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;
using System.Reflection;

using Echoglossian.NativeUI.AddonHandlers.MainMenu;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Guards ContextMenu runtime capture and hover registration contracts.
/// </summary>
public sealed class ContextMenuRuntimeContractTests
{
    /// <summary>
    ///     Ensures ContextMenu captures the full visible row chain rather than
    ///     a fixed number of menu slots.
    /// </summary>
    [Fact]
    public void ContextMenu_CapturesVariableVisibleRowChain()
    {
        var source = ReadContextMenuHandlerSource();

        Assert.Contains("ResolveContextMenuRowTextNodes", source);
        Assert.Contains("ComponentNodes[3]", source);
        Assert.Contains("Next", source);
        Assert.DoesNotContain("for (var i = 0; i < 5;", source, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures ContextMenu registers each hover target from its visible
    ///     row collision bounds.
    /// </summary>
    [Fact]
    public void ContextMenu_RegistersHoverTargetsFromRowCollisionBounds()
    {
        var source = ReadContextMenuHandlerSource();

        Assert.Contains("TryRegisterCustomHoverTooltips", source);
        Assert.Contains("RegisterTranslatedHoverTooltip", source);
        Assert.Contains("ComponentRoot", source);
        Assert.Contains("DbFirstTextNodeKeyAllocator.ConsumeVisibleNode", source);
        Assert.Contains("originalPayload.TextNodes.TryGetValue", source);
        Assert.Contains("translatedPayload.TextNodes.TryGetValue", source);
        Assert.Contains(
            "!IsNativeContextMenuReplacementSafe(liveText, sourceText)",
            source);
        Assert.DoesNotContain("Select((row, index)", source, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures ContextMenu canonicalizes decorated labels for persistence
    ///     and leaves labels with unrecomposable decorations out of native
    ///     replacement.
    /// </summary>
    [Fact]
    public void ContextMenu_NormalizesDecoratedLabelsAndGuardsNativeReplacement()
    {
        Assert.Equal(
            "Dismiss",
            InvokeStaticStringMethod(
                "NormalizeContextMenuLabel",
                "\uE03C\u0002Dismiss\u0003"));
        Assert.True(InvokeStaticBooleanMethod(
            "IsNativeContextMenuReplacementSafe",
            "Dismiss",
            "Dismiss"));
        Assert.False(InvokeStaticBooleanMethod(
            "IsNativeContextMenuReplacementSafe",
            "\uE03CDismiss",
            "Dismiss"));
    }

    /// <summary>
    ///     Ensures a decorated row that cannot be replaced natively falls back
    ///     to translated hover text in both native-writing modes.
    /// </summary>
    [Theory]
    [InlineData(JournalTranslationDisplayMode.NativeUiTranslation)]
    [InlineData(
        JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips)]
    public void ContextMenu_DecoratedNativeRowsUseTranslatedTooltipFallback(
        JournalTranslationDisplayMode displayMode)
    {
        Assert.Equal(
            JournalTranslationDisplayMode.TooltipTranslation,
            InvokeTooltipDisplayMode(
                "\uE03CDismiss",
                "Dismiss",
                displayMode));
        Assert.Null(InvokeTooltipDisplayMode(
            "Dismiss",
            "Dismiss",
            JournalTranslationDisplayMode.NativeUiTranslation));
    }

    /// <summary>
    ///     Reads the ContextMenu handler source.
    /// </summary>
    /// <returns>The handler source text.</returns>
    private static string ReadContextMenuHandlerSource()
    {
        var root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "MainMenu",
            "ContextMenuHandler.cs"));
    }

    /// <summary>
    ///     Invokes one private static ContextMenu string helper.
    /// </summary>
    /// <param name="methodName">The helper method name.</param>
    /// <param name="value">The input label.</param>
    /// <returns>The helper result.</returns>
    private static string InvokeStaticStringMethod(string methodName, string value)
    {
        var method = typeof(ContextMenuHandler).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<string>(method.Invoke(null, [value]));
    }

    /// <summary>
    ///     Invokes one private static ContextMenu native-replacement guard.
    /// </summary>
    /// <param name="methodName">The helper method name.</param>
    /// <param name="liveLabel">The raw label currently in the UI.</param>
    /// <param name="canonicalLabel">The canonical persisted label.</param>
    /// <returns>The guard result.</returns>
    private static bool InvokeStaticBooleanMethod(
        string methodName,
        string liveLabel,
        string canonicalLabel)
    {
        var method = typeof(ContextMenuHandler).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<bool>(method.Invoke(null, [liveLabel, canonicalLabel]));
    }

    /// <summary>
    ///     Invokes the ContextMenu tooltip fallback policy.
    /// </summary>
    /// <param name="liveLabel">The raw visible label.</param>
    /// <param name="canonicalLabel">The canonical source label.</param>
    /// <param name="displayMode">The configured display mode.</param>
    /// <returns>The tooltip display mode, or <see langword="null" />.</returns>
    private static JournalTranslationDisplayMode? InvokeTooltipDisplayMode(
        string liveLabel,
        string canonicalLabel,
        JournalTranslationDisplayMode displayMode)
    {
        var method = typeof(ContextMenuHandler).GetMethod(
            "ResolveContextMenuTooltipDisplayMode",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (JournalTranslationDisplayMode?)method.Invoke(
            null,
            [liveLabel, canonicalLabel, displayMode]);
    }

    /// <summary>
    ///     Finds the repository root from the test output directory.
    /// </summary>
    /// <returns>The repository root directory.</returns>
    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Echoglossian.sln")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root.");
    }
}
