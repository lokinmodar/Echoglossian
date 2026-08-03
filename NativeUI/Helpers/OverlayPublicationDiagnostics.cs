// <copyright file="OverlayPublicationDiagnostics.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
/// Emits temporary, deduplicated DEBUG diagnostics for overlay publication and
/// synchronization boundaries while overlay visibility regressions are under
/// investigation.
/// </summary>
internal static class OverlayPublicationDiagnostics
{
    private static readonly ConcurrentDictionary<string, EmissionState> LastEmissionByKey =
        new(StringComparer.Ordinal);

    private static readonly TimeSpan RepeatInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Logs one deduplicated overlay-publication diagnostic in <c>DEBUG</c>
    /// builds.
    /// </summary>
    /// <param name="scope">The logical diagnostic scope.</param>
    /// <param name="state">The current publication or sync state.</param>
    /// <param name="dedupeKey">The stable deduplication key.</param>
    /// <param name="signature">The event-state signature.</param>
    /// <param name="details">The formatted diagnostic details.</param>
    [Conditional("DEBUG")]
    internal static void Log(
        string scope,
        string state,
        string dedupeKey,
        string signature,
        string details)
    {
        if (!IsScopeEnabled(scope))
        {
            return;
        }

        if (!ShouldEmit(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{scope}|{state}|{dedupeKey}"),
                signature))
        {
            return;
        }

        PluginRuntimeLog.Debug(
            string.Create(
                CultureInfo.InvariantCulture,
                $"[{scope}] state={state} {details}"));
    }

    /// <summary>
    ///     Gets whether one logical diagnostic scope is currently enabled.
    /// </summary>
    /// <param name="scope">The logical diagnostic scope.</param>
    /// <returns>
    ///     <see langword="true" /> when the scope should emit DEBUG
    ///     diagnostics; otherwise, <see langword="false" />.
    /// </returns>
    private static bool IsScopeEnabled(string scope)
    {
        return !string.Equals(
                   scope,
                   "NamePlateOverlayDiag",
                   StringComparison.Ordinal) &&
               !string.Equals(
                   scope,
                   "TooltipAddonOverlayDiag",
                   StringComparison.Ordinal);
    }

    /// <summary>
    /// Builds one short preview suitable for diagnostic logs and dedupe keys.
    /// </summary>
    /// <param name="text">The source text.</param>
    /// <param name="maxLength">The maximum preview length.</param>
    /// <returns>The normalized preview.</returns>
    internal static string BuildPreview(
        string? text,
        int maxLength = 72)
    {
        var normalized = (text ?? string.Empty)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return normalized[..maxLength] + "…";
    }

    /// <summary>
    /// Formats one vector value for diagnostic logs.
    /// </summary>
    /// <param name="value">The vector value.</param>
    /// <returns>The formatted vector.</returns>
    internal static string FormatVector(Vector2 value)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"({value.X:0.0},{value.Y:0.0})");
    }

    /// <summary>
    /// Rounds one vector to a coarse step size for diagnostic signatures.
    /// </summary>
    /// <param name="value">The source vector.</param>
    /// <param name="step">The rounding step.</param>
    /// <returns>The rounded vector.</returns>
    internal static Vector2 RoundVector(
        Vector2 value,
        float step = 16f)
    {
        if (step <= 0f)
        {
            return value;
        }

        return new Vector2(
            MathF.Round(value.X / step) * step,
            MathF.Round(value.Y / step) * step);
    }

    /// <summary>
    /// Determines whether one deduplicated emission should be written.
    /// </summary>
    /// <param name="key">The stable event key.</param>
    /// <param name="signature">The event-state signature.</param>
    /// <returns><see langword="true" /> when the event should be emitted.</returns>
    private static bool ShouldEmit(
        string key,
        string signature)
    {
        var nowUtc = DateTime.UtcNow;
        while (true)
        {
            if (!LastEmissionByKey.TryGetValue(key, out var priorState))
            {
                if (LastEmissionByKey.TryAdd(
                        key,
                        new EmissionState(signature, nowUtc)))
                {
                    return true;
                }

                continue;
            }

            if (string.Equals(
                    priorState.Signature,
                    signature,
                    StringComparison.Ordinal) &&
                nowUtc - priorState.EmittedAtUtc < RepeatInterval)
            {
                return false;
            }

            var nextState = new EmissionState(signature, nowUtc);
            if (LastEmissionByKey.TryUpdate(key, nextState, priorState))
            {
                return true;
            }
        }
    }

    /// <summary>
    /// Captures the last emitted signature and emission time for one
    /// diagnostic key.
    /// </summary>
    /// <param name="Signature">The emitted diagnostic signature.</param>
    /// <param name="EmittedAtUtc">The last emission time in UTC.</param>
    private sealed record EmissionState(
        string Signature,
        DateTime EmittedAtUtc);
}
