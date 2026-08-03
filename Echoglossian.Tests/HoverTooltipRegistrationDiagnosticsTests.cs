// <copyright file="HoverTooltipRegistrationDiagnosticsTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Numerics;

using Echoglossian.NativeUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers temporary debug logging for hover-tooltip registration and hover-hit
/// transitions.
/// </summary>
public sealed class HoverTooltipRegistrationDiagnosticsTests
{
    /// <summary>
    /// Ensures the temporary logging command defaults to a 60-second watch when
    /// only a surface filter is provided.
    /// </summary>
    [Fact]
    public void TryParseCommandArguments_SurfaceOnly_UsesDefaultDuration()
    {
        var parsed = HoverTooltipRegistrationDiagnostics.TryParseCommandArguments(
            "JournalAccept",
            out var arguments,
            out var hasInvalidDuration);

        Assert.True(parsed);
        Assert.False(hasInvalidDuration);
        Assert.False(arguments.IsStop);
        Assert.Equal("JournalAccept", arguments.SurfaceFilter);
        Assert.Equal(TimeSpan.FromSeconds(60), arguments.Duration);
    }

    /// <summary>
    /// Ensures the temporary logging command accepts an explicit duration token.
    /// </summary>
    [Fact]
    public void TryParseCommandArguments_WithDuration_ParsesSurfaceAndDuration()
    {
        var parsed = HoverTooltipRegistrationDiagnostics.TryParseCommandArguments(
            "JournalResult 90s",
            out var arguments,
            out var hasInvalidDuration);

        Assert.True(parsed);
        Assert.False(hasInvalidDuration);
        Assert.False(arguments.IsStop);
        Assert.Equal("JournalResult", arguments.SurfaceFilter);
        Assert.Equal(TimeSpan.FromSeconds(90), arguments.Duration);
    }

    /// <summary>
    /// Ensures the temporary logging command rejects malformed duration tokens
    /// while still distinguishing them from a generic usage error.
    /// </summary>
    [Fact]
    public void TryParseCommandArguments_InvalidDuration_ReturnsUsageAndFlagsDuration()
    {
        var parsed = HoverTooltipRegistrationDiagnostics.TryParseCommandArguments(
            "JournalAccept 31m",
            out _,
            out var hasInvalidDuration);

        Assert.False(parsed);
        Assert.True(hasInvalidDuration);
    }

    /// <summary>
    /// Ensures the temporary logging command recognizes the explicit stop
    /// request.
    /// </summary>
    [Fact]
    public void TryParseCommandArguments_StopCommand_IsRecognized()
    {
        var parsed = HoverTooltipRegistrationDiagnostics.TryParseCommandArguments(
            "stop",
            out var arguments,
            out var hasInvalidDuration);

        Assert.True(parsed);
        Assert.False(hasInvalidDuration);
        Assert.True(arguments.IsStop);
    }

    /// <summary>
    /// Ensures only the requested surface is logged and repeated hover frames do
    /// not spam duplicate hover-enter lines.
    /// </summary>
    [Fact]
    public void SurfaceScopedLogging_LogsMatchingRegistrationsAndHoverTransitionsOnly()
    {
        List<string> lines = [];
        var diagnostics = new HoverTooltipRegistrationDiagnostics(lines.Add);
        var nowUtc = new DateTime(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc);

        diagnostics.Start("JournalAccept", TimeSpan.FromSeconds(60), nowUtc);
        diagnostics.LogRegister(
            "JournalResult-QuestName-1",
            HoverTooltipAnchorKind.TextNode,
            new Vector2(10f, 20f),
            new Vector2(110f, 40f),
            enabled: true,
            displaysOriginalSwapText: false,
            updated: false,
            nowUtc.AddSeconds(1));
        diagnostics.LogRegister(
            "JournalAccept-QuestBody-2",
            HoverTooltipAnchorKind.ExplicitBounds,
            new Vector2(82f, 76f),
            new Vector2(288f, 184f),
            enabled: true,
            displaysOriginalSwapText: true,
            updated: false,
            nowUtc.AddSeconds(2));
        diagnostics.LogHoverChange(
            "JournalAccept-QuestBody-2",
            HoverTooltipAnchorKind.ExplicitBounds,
            new Vector2(82f, 76f),
            new Vector2(288f, 184f),
            displaysOriginalSwapText: true,
            new Vector2(120f, 100f),
            nowUtc.AddSeconds(3));
        diagnostics.LogHoverChange(
            "JournalAccept-QuestBody-2",
            HoverTooltipAnchorKind.ExplicitBounds,
            new Vector2(82f, 76f),
            new Vector2(288f, 184f),
            displaysOriginalSwapText: true,
            new Vector2(126f, 104f),
            nowUtc.AddSeconds(4));
        diagnostics.LogHoverChange(
            null,
            HoverTooltipAnchorKind.Unspecified,
            default,
            default,
            displaysOriginalSwapText: false,
            new Vector2(50f, 50f),
            nowUtc.AddSeconds(5));

        Assert.Collection(
            lines,
            line => Assert.Contains("started surface='JournalAccept'", line, StringComparison.Ordinal),
            line =>
            {
                Assert.Contains("register surface='JournalAccept'", line, StringComparison.Ordinal);
                Assert.Contains("anchor='explicit bounds'", line, StringComparison.Ordinal);
                Assert.Contains("size=(206.0x108.0)", line, StringComparison.Ordinal);
                Assert.Contains("payload='original-swap'", line, StringComparison.Ordinal);
            },
            line => Assert.Contains("hover-enter surface='JournalAccept'", line, StringComparison.Ordinal),
            line => Assert.Contains("hover-exit surface='JournalAccept'", line, StringComparison.Ordinal));
    }

    /// <summary>
    /// Ensures the temporary session expires on the next observed event instead
    /// of continuing to log indefinitely.
    /// </summary>
    [Fact]
    public void ExpiredSession_StopsBeforeLoggingNewRegistration()
    {
        List<string> lines = [];
        var diagnostics = new HoverTooltipRegistrationDiagnostics(lines.Add);
        var nowUtc = new DateTime(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc);

        diagnostics.Start("all", TimeSpan.FromSeconds(1), nowUtc);
        diagnostics.LogRegister(
            "JournalAccept-QuestName-1",
            HoverTooltipAnchorKind.TextNode,
            new Vector2(10f, 20f),
            new Vector2(110f, 40f),
            enabled: true,
            displaysOriginalSwapText: false,
            updated: false,
            nowUtc.AddSeconds(2));

        Assert.Collection(
            lines,
            line => Assert.Contains("started surface='all'", line, StringComparison.Ordinal),
            line => Assert.Contains("expired surface='all'", line, StringComparison.Ordinal));
    }

    /// <summary>
    /// Ensures popup-body geometry fallback diagnostics report the preferred
    /// structural node and the final text-node fallback anchor.
    /// </summary>
    [Fact]
    public void BodyGeometryDecision_LogsPreferredNodeAndFinalFallbackAnchor()
    {
        List<string> lines = [];
        var diagnostics = new HoverTooltipRegistrationDiagnostics(lines.Add);
        var method = typeof(HoverTooltipRegistrationDiagnostics).GetMethod(
            "LogBodyGeometryDecision",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        var nowUtc = new DateTime(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc);

        diagnostics.Start("JournalAccept", TimeSpan.FromSeconds(60), nowUtc);

        Assert.NotNull(method);
        method!.Invoke(
            diagnostics,
            [
                "JournalAccept-QuestBody-2",
                "component",
                new Vector2(90f, 80f),
                new Vector2(280f, 180f),
                false,
                Vector2.Zero,
                Vector2.Zero,
                HoverTooltipAnchorKind.TextNode,
                nowUtc.AddSeconds(1),
            ]);

        Assert.Collection(
            lines,
            line => Assert.Contains("started surface='JournalAccept'", line, StringComparison.Ordinal),
            line =>
            {
                Assert.Contains("geometry surface='JournalAccept'", line, StringComparison.Ordinal);
                Assert.Contains("preferred='component'", line, StringComparison.Ordinal);
                Assert.Contains("preferredSize=(190.0x100.0)", line, StringComparison.Ordinal);
                Assert.Contains("explicitBoundsBuilt=False", line, StringComparison.Ordinal);
                Assert.Contains("finalAnchor='text node'", line, StringComparison.Ordinal);
            });
    }
}
