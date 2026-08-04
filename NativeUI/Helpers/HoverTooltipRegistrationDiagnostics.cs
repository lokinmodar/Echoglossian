// <copyright file="HoverTooltipRegistrationDiagnostics.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Globalization;
using System.Numerics;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
/// Identifies the runtime anchor used to register a hover-tooltip target.
/// </summary>
internal enum HoverTooltipAnchorKind
{
    /// <summary>
    /// The registration did not identify a more specific anchor kind.
    /// </summary>
    Unspecified,

    /// <summary>
    /// The registration is anchored directly to an <c>AtkTextNode</c>.
    /// </summary>
    TextNode,

    /// <summary>
    /// The registration is anchored directly to a generic <c>AtkResNode</c>.
    /// </summary>
    ResNode,

    /// <summary>
    /// The registration uses the addon root bounds.
    /// </summary>
    AddonRoot,

    /// <summary>
    /// The registration uses explicit screen bounds.
    /// </summary>
    ExplicitBounds,
}

/// <summary>
/// Holds the parsed arguments for the temporary tooltip-registration logging
/// command.
/// </summary>
/// <param name="SurfaceFilter">The requested surface filter.</param>
/// <param name="Duration">The requested watch duration.</param>
/// <param name="IsStop">Whether the command requested a stop action.</param>
internal readonly record struct HoverTooltipRegistrationCommandArguments(
    string SurfaceFilter,
    TimeSpan Duration,
    bool IsStop);

/// <summary>
/// Emits temporary diagnostic log lines for hover-tooltip registration and
/// hover-hit transitions.
/// </summary>
internal sealed class HoverTooltipRegistrationDiagnostics
{
    private const string AllSurfaceToken = "all";
    private readonly Action<string> log;
    private readonly object syncRoot = new();
    private ActiveSession? activeSession;
    private HoverState? lastHoveredState;

    /// <summary>
    /// The default duration used when a logging session omits an explicit watch
    /// length.
    /// </summary>
    internal static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The maximum watch duration accepted by the command parser.
    /// </summary>
    internal static readonly TimeSpan MaxDuration = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Initializes a new instance of the <see cref="HoverTooltipRegistrationDiagnostics" />
    /// class.
    /// </summary>
    /// <param name="log">The runtime log sink.</param>
    internal HoverTooltipRegistrationDiagnostics(Action<string> log)
    {
        this.log = log;
    }

    /// <summary>
    /// Parses the debug command arguments for tooltip-registration logging.
    /// </summary>
    /// <param name="args">The raw command argument text.</param>
    /// <param name="arguments">Receives the parsed command arguments.</param>
    /// <param name="hasInvalidDuration">
    /// Receives whether the failure was caused by a malformed duration token.
    /// </param>
    /// <returns><see langword="true" /> when the command parsed successfully.</returns>
    internal static bool TryParseCommandArguments(
        string args,
        out HoverTooltipRegistrationCommandArguments arguments,
        out bool hasInvalidDuration)
    {
        arguments = default;
        hasInvalidDuration = false;

        var trimmedArgs = args.Trim();
        if (trimmedArgs.Length == 0)
        {
            return false;
        }

        if (trimmedArgs.Equals("stop", StringComparison.OrdinalIgnoreCase) ||
            trimmedArgs.Equals("cancel", StringComparison.OrdinalIgnoreCase))
        {
            arguments = new HoverTooltipRegistrationCommandArguments(
                string.Empty,
                TimeSpan.Zero,
                IsStop: true);
            return true;
        }

        var parts = trimmedArgs.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Length > 2)
        {
            return false;
        }

        var surfaceFilter = parts[0].Trim();
        if (surfaceFilter.Length == 0)
        {
            return false;
        }

        var duration = DefaultDuration;
        if (parts.Length == 2)
        {
            if (TryParseDuration(parts[1], out var parsedDuration))
            {
                duration = parsedDuration;
            }
            else
            {
                hasInvalidDuration = LooksLikeDuration(parts[1]);
                return false;
            }
        }

        arguments = new HoverTooltipRegistrationCommandArguments(
            surfaceFilter,
            duration,
            IsStop: false);
        return true;
    }

    /// <summary>
    /// Determines whether a logging session is currently active.
    /// </summary>
    /// <param name="nowUtc">The current UTC timestamp.</param>
    /// <returns><see langword="true" /> when a session is still active.</returns>
    internal bool IsActive(DateTime nowUtc)
    {
        lock (this.syncRoot)
        {
            return this.TryGetActiveSession(nowUtc, logExpiry: false) != null;
        }
    }

    /// <summary>
    /// Starts a fresh logging session for the requested surface filter.
    /// </summary>
    /// <param name="surfaceFilter">The requested surface filter.</param>
    /// <param name="duration">The requested watch duration.</param>
    /// <param name="nowUtc">The current UTC timestamp.</param>
    internal void Start(string surfaceFilter, TimeSpan duration, DateTime nowUtc)
    {
        lock (this.syncRoot)
        {
            var normalizedSurfaceFilter = NormalizeSurfaceFilter(surfaceFilter);
            var expiresUtc = nowUtc + duration;
            this.activeSession = new ActiveSession(
                normalizedSurfaceFilter,
                string.Equals(
                    normalizedSurfaceFilter,
                    AllSurfaceToken,
                    StringComparison.OrdinalIgnoreCase),
                expiresUtc);
            this.lastHoveredState = null;
            this.log(
                $"[HoverTooltipRegistration] started surface='{normalizedSurfaceFilter}' duration={FormatDuration(duration)} expiresUtc={expiresUtc:O}");
        }
    }

    /// <summary>
    /// Stops the current logging session, if any.
    /// </summary>
    /// <param name="nowUtc">The current UTC timestamp.</param>
    /// <returns><see langword="true" /> when a session was stopped.</returns>
    internal bool Stop(DateTime nowUtc)
    {
        lock (this.syncRoot)
        {
            var session = this.TryGetActiveSession(nowUtc, logExpiry: false);
            if (session == null)
            {
                this.activeSession = null;
                this.lastHoveredState = null;
                return false;
            }

            this.activeSession = null;
            this.lastHoveredState = null;
            this.log(
                $"[HoverTooltipRegistration] stopped surface='{session.SurfaceFilter}' at {nowUtc:O}");
            return true;
        }
    }

    /// <summary>
    /// Logs a matching hover-tooltip registration or update.
    /// </summary>
    /// <param name="key">The registration key.</param>
    /// <param name="anchorKind">The runtime anchor kind.</param>
    /// <param name="topLeft">The registered top-left corner.</param>
    /// <param name="bottomRight">The registered bottom-right corner.</param>
    /// <param name="enabled">Whether the target is enabled.</param>
    /// <param name="displaysOriginalSwapText">
    /// Whether the tooltip shows the original text in swap mode.
    /// </param>
    /// <param name="updated">Whether the entry replaced a prior registration.</param>
    /// <param name="nowUtc">The current UTC timestamp.</param>
    internal void LogRegister(
        string key,
        HoverTooltipAnchorKind anchorKind,
        Vector2 topLeft,
        Vector2 bottomRight,
        bool enabled,
        bool displaysOriginalSwapText,
        bool updated,
        DateTime nowUtc)
    {
        lock (this.syncRoot)
        {
            var session = this.TryGetActiveSession(nowUtc, logExpiry: true);
            if (session == null)
            {
                return;
            }

            var surface = ExtractSurfaceName(key);
            if (!session.Matches(surface))
            {
                return;
            }

            this.log(
                $"[HoverTooltipRegistration] {(updated ? "update" : "register")} surface='{surface}' key='{key}' anchor='{FormatAnchor(anchorKind)}' topLeft={FormatVector(topLeft)} bottomRight={FormatVector(bottomRight)} size={FormatSize(topLeft, bottomRight)} payload='{FormatPayloadMode(displaysOriginalSwapText)}' enabled={enabled}");
        }
    }

    /// <summary>
    /// Logs a matching hover-tooltip removal.
    /// </summary>
    /// <param name="key">The removed registration key.</param>
    /// <param name="reason">The removal reason.</param>
    /// <param name="nowUtc">The current UTC timestamp.</param>
    internal void LogRemove(string key, string reason, DateTime nowUtc)
    {
        lock (this.syncRoot)
        {
            var session = this.TryGetActiveSession(nowUtc, logExpiry: true);
            if (session == null)
            {
                return;
            }

            var surface = ExtractSurfaceName(key);
            if (!session.Matches(surface))
            {
                return;
            }

            this.log(
                $"[HoverTooltipRegistration] remove surface='{surface}' key='{key}' reason='{reason}'");
        }
    }

    /// <summary>
    /// Logs hover-enter and hover-exit transitions for the currently hovered
    /// registration.
    /// </summary>
    /// <param name="hoveredKey">The hovered registration key, if any.</param>
    /// <param name="anchorKind">The hovered anchor kind.</param>
    /// <param name="topLeft">The hovered top-left corner.</param>
    /// <param name="bottomRight">The hovered bottom-right corner.</param>
    /// <param name="displaysOriginalSwapText">
    /// Whether the hovered tooltip shows original swap text.
    /// </param>
    /// <param name="mousePosition">The current cursor position.</param>
    /// <param name="nowUtc">The current UTC timestamp.</param>
    internal void LogHoverChange(
        string? hoveredKey,
        HoverTooltipAnchorKind anchorKind,
        Vector2 topLeft,
        Vector2 bottomRight,
        bool displaysOriginalSwapText,
        Vector2 mousePosition,
        DateTime nowUtc)
    {
        lock (this.syncRoot)
        {
            var session = this.TryGetActiveSession(nowUtc, logExpiry: true);
            if (session == null)
            {
                return;
            }

            HoverState? nextHoveredState = null;
            if (!string.IsNullOrWhiteSpace(hoveredKey))
            {
                var surface = ExtractSurfaceName(hoveredKey);
                if (session.Matches(surface))
                {
                    nextHoveredState = new HoverState(
                        hoveredKey,
                        surface,
                        anchorKind,
                        topLeft,
                        bottomRight,
                        displaysOriginalSwapText);
                }
            }

            if (HoverState.Equals(this.lastHoveredState, nextHoveredState))
            {
                return;
            }

            if (this.lastHoveredState != null)
            {
                this.log(
                    $"[HoverTooltipRegistration] hover-exit surface='{this.lastHoveredState.Surface}' key='{this.lastHoveredState.Key}' mouse={FormatVector(mousePosition)}");
            }

            if (nextHoveredState != null)
            {
                this.log(
                    $"[HoverTooltipRegistration] hover-enter surface='{nextHoveredState.Surface}' key='{nextHoveredState.Key}' anchor='{FormatAnchor(nextHoveredState.AnchorKind)}' mouse={FormatVector(mousePosition)} topLeft={FormatVector(nextHoveredState.TopLeft)} bottomRight={FormatVector(nextHoveredState.BottomRight)} size={FormatSize(nextHoveredState.TopLeft, nextHoveredState.BottomRight)} payload='{FormatPayloadMode(nextHoveredState.DisplaysOriginalSwapText)}'");
            }

            this.lastHoveredState = nextHoveredState;
        }
    }

    /// <summary>
    /// Logs one popup-body geometry decision before registration falls back to
    /// a smaller text-node anchor or succeeds with explicit bounds.
    /// </summary>
    /// <param name="key">The registration key being prepared.</param>
    /// <param name="preferredHoverNodeKind">
    /// The structural node kind resolved for geometry, if any.
    /// </param>
    /// <param name="preferredTopLeft">
    /// The resolved structural node top-left corner, if any.
    /// </param>
    /// <param name="preferredBottomRight">
    /// The resolved structural node bottom-right corner, if any.
    /// </param>
    /// <param name="explicitBoundsBuilt">
    /// Whether explicit popup-body bounds were successfully built.
    /// </param>
    /// <param name="explicitTopLeft">
    /// The explicit bounds top-left corner when one was built.
    /// </param>
    /// <param name="explicitBottomRight">
    /// The explicit bounds bottom-right corner when one was built.
    /// </param>
    /// <param name="finalAnchorKind">
    /// The final anchor kind chosen for registration.
    /// </param>
    /// <param name="nowUtc">The current UTC timestamp.</param>
    internal void LogBodyGeometryDecision(
        string key,
        string preferredHoverNodeKind,
        Vector2 preferredTopLeft,
        Vector2 preferredBottomRight,
        bool explicitBoundsBuilt,
        Vector2 explicitTopLeft,
        Vector2 explicitBottomRight,
        HoverTooltipAnchorKind finalAnchorKind,
        DateTime nowUtc)
    {
        lock (this.syncRoot)
        {
            var session = this.TryGetActiveSession(nowUtc, logExpiry: true);
            if (session == null)
            {
                return;
            }

            var surface = ExtractSurfaceName(key);
            if (!session.Matches(surface))
            {
                return;
            }

            var explicitBoundsSegment = explicitBoundsBuilt
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $" explicitTopLeft={FormatVector(explicitTopLeft)} explicitBottomRight={FormatVector(explicitBottomRight)} explicitSize={FormatSize(explicitTopLeft, explicitBottomRight)}")
                : string.Empty;
            this.log(
                $"[HoverTooltipRegistration] geometry surface='{surface}' key='{key}' preferred='{preferredHoverNodeKind}' preferredTopLeft={FormatVector(preferredTopLeft)} preferredBottomRight={FormatVector(preferredBottomRight)} preferredSize={FormatSize(preferredTopLeft, preferredBottomRight)} explicitBoundsBuilt={explicitBoundsBuilt}{explicitBoundsSegment} finalAnchor='{FormatAnchor(finalAnchorKind)}'");
        }
    }

    /// <summary>
    /// Extracts the leading surface prefix from a registration key.
    /// </summary>
    /// <param name="key">The registration key.</param>
    /// <returns>The extracted surface name.</returns>
    internal static string ExtractSurfaceName(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        var delimiterIndex = key.IndexOf('-');
        if (delimiterIndex <= 0)
        {
            return key.Trim();
        }

        return key[..delimiterIndex].Trim();
    }

    private static bool TryParseDuration(string token, out TimeSpan duration)
    {
        duration = default;
        if (string.IsNullOrWhiteSpace(token) || token.Length < 2)
        {
            return false;
        }

        var unit = char.ToLowerInvariant(token[^1]);
        if (unit != 's' && unit != 'm')
        {
            return false;
        }

        if (!int.TryParse(token[..^1], out var value) || value <= 0)
        {
            return false;
        }

        duration = unit switch
        {
            's' => TimeSpan.FromSeconds(value),
            'm' => TimeSpan.FromMinutes(value),
            _ => default,
        };

        return duration > TimeSpan.Zero && duration <= MaxDuration;
    }

    private static bool LooksLikeDuration(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length < 2)
        {
            return false;
        }

        var unit = char.ToLowerInvariant(token[^1]);
        if (unit != 's' && unit != 'm')
        {
            return false;
        }

        return int.TryParse(token[..^1], out _);
    }

    private static string NormalizeSurfaceFilter(string surfaceFilter)
    {
        var trimmedFilter = surfaceFilter.Trim();
        return string.Equals(
            trimmedFilter,
            AllSurfaceToken,
            StringComparison.OrdinalIgnoreCase)
            ? AllSurfaceToken
            : trimmedFilter;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes >= 1d &&
            Math.Abs(duration.TotalMinutes - Math.Round(duration.TotalMinutes)) < 0.001d)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{Math.Round(duration.TotalMinutes)}m");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Math.Round(duration.TotalSeconds)}s");
    }

    private static string FormatAnchor(HoverTooltipAnchorKind anchorKind)
        => anchorKind switch
        {
            HoverTooltipAnchorKind.TextNode => "text node",
            HoverTooltipAnchorKind.ResNode => "res node",
            HoverTooltipAnchorKind.AddonRoot => "addon root",
            HoverTooltipAnchorKind.ExplicitBounds => "explicit bounds",
            _ => "unspecified",
        };

    private static string FormatPayloadMode(bool displaysOriginalSwapText)
        => displaysOriginalSwapText
            ? "original-swap"
            : "translated";

    private static string FormatVector(Vector2 value)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"({value.X:0.0},{value.Y:0.0})");

    private static string FormatSize(Vector2 topLeft, Vector2 bottomRight)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"({Math.Max(0f, bottomRight.X - topLeft.X):0.0}x{Math.Max(0f, bottomRight.Y - topLeft.Y):0.0})");

    private ActiveSession? TryGetActiveSession(DateTime nowUtc, bool logExpiry)
    {
        if (this.activeSession == null)
        {
            return null;
        }

        if (nowUtc <= this.activeSession.ExpiresUtc)
        {
            return this.activeSession;
        }

        var expiredSession = this.activeSession;
        this.activeSession = null;
        this.lastHoveredState = null;
        if (logExpiry)
        {
            this.log(
                $"[HoverTooltipRegistration] expired surface='{expiredSession.SurfaceFilter}' at {nowUtc:O}");
        }

        return null;
    }

    private sealed record ActiveSession(
        string SurfaceFilter,
        bool MonitorAll,
        DateTime ExpiresUtc)
    {
        /// <summary>
        /// Determines whether the active session should log the requested
        /// surface.
        /// </summary>
        /// <param name="surface">The surface name extracted from the key.</param>
        /// <returns><see langword="true" /> when the session matches it.</returns>
        public bool Matches(string surface)
        {
            return this.MonitorAll ||
                   string.Equals(
                       this.SurfaceFilter,
                       surface,
                       StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed record HoverState(
        string Key,
        string Surface,
        HoverTooltipAnchorKind AnchorKind,
        Vector2 TopLeft,
        Vector2 BottomRight,
        bool DisplaysOriginalSwapText);
}
