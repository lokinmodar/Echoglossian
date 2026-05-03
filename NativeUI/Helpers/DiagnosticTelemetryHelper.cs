// <copyright file="DiagnosticTelemetryHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Provides a narrow, reusable helper for investigation telemetry that
///     should remain silent unless a caller explicitly opts in.
/// </summary>
/// <remarks>
///     This helper is intentionally small: it only handles per-category
///     throttling keyed by one stable signature and standardized log prefixes.
///     It does not decide which state is worth observing or when diagnostics
///     should be enabled for one feature.
/// </remarks>
internal sealed class DiagnosticTelemetryHelper
{
    private readonly TimeSpan defaultCooldown;
    private readonly Dictionary<string, TelemetryEmissionState> emissionStates =
        new(StringComparer.Ordinal);
    private readonly string scope;

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="DiagnosticTelemetryHelper" /> class.
    /// </summary>
    /// <param name="scope">
    ///     The stable scope name that prefixes emitted log lines.
    /// </param>
    /// <param name="defaultCooldown">
    ///     The default suppression interval applied when callers do not supply
    ///     one explicitly.
    /// </param>
    public DiagnosticTelemetryHelper(
        string scope,
        TimeSpan defaultCooldown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        this.scope = scope;
        this.defaultCooldown = defaultCooldown > TimeSpan.Zero
            ? defaultCooldown
            : TimeSpan.FromSeconds(2);
    }

    /// <summary>
    ///     Emits one throttled debug log line for the specified category.
    /// </summary>
    /// <param name="category">The logical diagnostic category.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="signature">
    ///     The stable signature used for suppression. When omitted, the
    ///     message itself becomes the signature.
    /// </param>
    /// <param name="cooldown">
    ///     The optional suppression interval for repeated identical
    ///     signatures.
    /// </param>
    public void Debug(
        string category,
        string message,
        string? signature = null,
        TimeSpan? cooldown = null)
    {
        this.Emit(
            category,
            message,
            signature,
            cooldown,
            logLine => PluginRuntimeLog.Debug(logLine));
    }

    /// <summary>
    ///     Emits one throttled information log line for the specified
    ///     category.
    /// </summary>
    /// <param name="category">The logical diagnostic category.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="signature">
    ///     The stable signature used for suppression. When omitted, the
    ///     message itself becomes the signature.
    /// </param>
    /// <param name="cooldown">
    ///     The optional suppression interval for repeated identical
    ///     signatures.
    /// </param>
    public void Information(
        string category,
        string message,
        string? signature = null,
        TimeSpan? cooldown = null)
    {
        this.Emit(
            category,
            message,
            signature,
            cooldown,
            logLine => PluginRuntimeLog.Information(logLine));
    }

    /// <summary>
    ///     Emits one throttled warning log line for the specified category.
    /// </summary>
    /// <param name="category">The logical diagnostic category.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="signature">
    ///     The stable signature used for suppression. When omitted, the
    ///     message itself becomes the signature.
    /// </param>
    /// <param name="cooldown">
    ///     The optional suppression interval for repeated identical
    ///     signatures.
    /// </param>
    public void Warning(
        string category,
        string message,
        string? signature = null,
        TimeSpan? cooldown = null)
    {
        this.Emit(
            category,
            message,
            signature,
            cooldown,
            logLine => PluginRuntimeLog.Warning(logLine));
    }

    /// <summary>
    ///     Determines whether one signature may be emitted now for the
    ///     specified category and records the new suppression window when it
    ///     may.
    /// </summary>
    /// <param name="category">The logical diagnostic category.</param>
    /// <param name="signature">The stable signature to compare.</param>
    /// <param name="observedAtUtc">The current UTC timestamp.</param>
    /// <param name="cooldown">
    ///     The optional suppression interval for repeated identical
    ///     signatures.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when the caller may emit the line now;
    ///     otherwise <see langword="false" />.
    /// </returns>
    public bool TryBeginEmit(
        string category,
        string signature,
        DateTime observedAtUtc,
        TimeSpan? cooldown = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(signature);

        var effectiveCooldown = this.GetEffectiveCooldown(cooldown);
        if (this.emissionStates.TryGetValue(category, out var currentState) &&
            string.Equals(
                currentState.Signature,
                signature,
                StringComparison.Ordinal) &&
            observedAtUtc < currentState.NextAllowedUtc)
        {
            return false;
        }

        this.emissionStates[category] = new TelemetryEmissionState(
            signature,
            observedAtUtc + effectiveCooldown);
        return true;
    }

    /// <summary>
    ///     Clears tracked suppression state for one category or for all
    ///     categories when none is specified.
    /// </summary>
    /// <param name="category">
    ///     The optional category to clear. When omitted, all categories are
    ///     reset.
    /// </param>
    public void Reset(string? category = null)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            this.emissionStates.Clear();
            return;
        }

        this.emissionStates.Remove(category);
    }

    /// <summary>
    ///     Emits one throttled log line through the provided sink.
    /// </summary>
    /// <param name="category">The logical diagnostic category.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="signature">
    ///     The stable signature used for suppression.
    /// </param>
    /// <param name="cooldown">
    ///     The optional suppression interval for repeated identical
    ///     signatures.
    /// </param>
    /// <param name="writer">The log sink to use.</param>
    private void Emit(
        string category,
        string message,
        string? signature,
        TimeSpan? cooldown,
        Action<string> writer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var effectiveSignature = string.IsNullOrWhiteSpace(signature)
            ? message
            : signature;
        if (!this.TryBeginEmit(
                category,
                effectiveSignature,
                DateTime.UtcNow,
                cooldown))
        {
            return;
        }

        writer($"[{this.scope}:{category}] {message}");
    }

    /// <summary>
    ///     Resolves the effective cooldown for one telemetry emission.
    /// </summary>
    /// <param name="cooldown">The optional caller-supplied cooldown.</param>
    /// <returns>The effective cooldown to apply.</returns>
    private TimeSpan GetEffectiveCooldown(TimeSpan? cooldown)
    {
        return cooldown is { } explicitCooldown &&
               explicitCooldown > TimeSpan.Zero
            ? explicitCooldown
            : this.defaultCooldown;
    }

    /// <summary>
    ///     Stores the last emitted signature and the next allowed timestamp for
    ///     one diagnostic category.
    /// </summary>
    /// <param name="Signature">The last emitted signature.</param>
    /// <param name="NextAllowedUtc">The next UTC timestamp that may emit it.</param>
    private readonly record struct TelemetryEmissionState(
        string Signature,
        DateTime NextAllowedUtc);
}


