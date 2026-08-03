// <copyright file="OverlayTextureRenderDiagnostics.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

#if DEBUG
using System.Collections.Concurrent;

using Echoglossian.UIOverlays.TextPresentation;

namespace Echoglossian.UIOverlays.TranslationOverlay;

/// <summary>
/// Emits temporary, deduplicated diagnostics for texture-backed overlay render
/// passes while complex-script overlay regressions are under investigation.
/// </summary>
internal static class OverlayTextureRenderDiagnostics
{
    private static readonly ConcurrentDictionary<string, EmissionState> LastEmissionByKey =
        new(StringComparer.Ordinal);

    private static readonly TimeSpan RepeatInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Logs one texture-backed overlay render pass that did not reach a drawn
    /// state because the backing texture was unavailable.
    /// </summary>
    /// <param name="configuration">The active plugin configuration.</param>
    /// <param name="surfaceId">The overlay surface identifier.</param>
    /// <param name="request">The active overlay render request.</param>
    /// <param name="textRequest">The resolved text-layout request.</param>
    /// <param name="outcome">The texture-render decision.</param>
    /// <param name="stats">The current RTL texture backend stats.</param>
    internal static void LogTextureUnavailable(
        Config configuration,
        TranslationOverlaySurfaceId surfaceId,
        TranslationOverlayRenderRequest request,
        TextLayoutRequest textRequest,
        TextureRenderAttemptOutcome outcome,
        (
            int Count,
            long EstimatedMemoryBytes,
            int AdaptiveWidthCount,
            int PendingTextureCount,
            int RetryStateCount,
            int QueuedTextureCount,
            int ActiveTextureWorkerCount) stats)
    {
        if (!ShouldLog(configuration))
        {
            return;
        }

        var previewKey = BuildPreviewKey(textRequest.Text);
        var roundedAddonPosition = RoundVector(request.AddonPosition, 16f);
        var roundedAddonSize = RoundVector(request.AddonSize, 16f);
        var signature = string.Create(
            CultureInfo.InvariantCulture,
            $"{outcome}|{previewKey}|{roundedAddonPosition.X:0},{roundedAddonPosition.Y:0}|{roundedAddonSize.X:0},{roundedAddonSize.Y:0}|{stats.Count}|{stats.PendingTextureCount}|{stats.RetryStateCount}|{stats.QueuedTextureCount}|{stats.ActiveTextureWorkerCount}");
        var key = string.Create(
            CultureInfo.InvariantCulture,
            $"{surfaceId}|unavailable|{previewKey}");
        if (!ShouldEmit(key, signature))
        {
            return;
        }

        PluginRuntimeLog.Debug(
            $"[OverlayTextureDiag] surface={surfaceId} state=texture-unavailable outcome={outcome} " +
            $"lang={textRequest.LanguageCode} textLen={textRequest.Text.Length} preview='{BuildPreview(textRequest.Text)}' " +
            $"addonPos={FormatVector(request.AddonPosition)} addonSize={FormatVector(request.AddonSize)} viewport={FormatVector(request.ViewportSize)} " +
            $"scale={request.ScaleMultiplier:0.##} alpha={request.AlphaMultiplier:0.##} " +
            $"cache={stats.Count} pending={stats.PendingTextureCount} retry={stats.RetryStateCount} queued={stats.QueuedTextureCount} workers={stats.ActiveTextureWorkerCount}");
    }

    /// <summary>
    /// Logs one successful texture-backed overlay draw.
    /// </summary>
    /// <param name="configuration">The active plugin configuration.</param>
    /// <param name="surfaceId">The overlay surface identifier.</param>
    /// <param name="request">The active overlay render request.</param>
    /// <param name="textRequest">The resolved text-layout request.</param>
    /// <param name="renderedPosition">The actual rendered window position.</param>
    /// <param name="renderedSize">The actual rendered window size.</param>
    /// <param name="bodySize">The measured body-text size.</param>
    internal static void LogTextureDrawn(
        Config configuration,
        TranslationOverlaySurfaceId surfaceId,
        TranslationOverlayRenderRequest request,
        TextLayoutRequest textRequest,
        Vector2 renderedPosition,
        Vector2 renderedSize,
        Vector2 bodySize)
    {
        if (!ShouldLog(configuration))
        {
            return;
        }

        var previewKey = BuildPreviewKey(textRequest.Text);
        var roundedRenderedPosition = RoundVector(renderedPosition, 16f);
        var roundedRenderedSize = RoundVector(renderedSize, 16f);
        var signature = string.Create(
            CultureInfo.InvariantCulture,
            $"{previewKey}|{roundedRenderedPosition.X:0},{roundedRenderedPosition.Y:0}|{roundedRenderedSize.X:0},{roundedRenderedSize.Y:0}|{request.ScaleMultiplier:0.##}|{request.AlphaMultiplier:0.##}");
        var key = string.Create(
            CultureInfo.InvariantCulture,
            $"{surfaceId}|drawn|{previewKey}");
        if (!ShouldEmit(key, signature))
        {
            return;
        }

        PluginRuntimeLog.Debug(
            $"[OverlayTextureDiag] surface={surfaceId} state=drawn " +
            $"lang={textRequest.LanguageCode} textLen={textRequest.Text.Length} preview='{BuildPreview(textRequest.Text)}' " +
            $"addonPos={FormatVector(request.AddonPosition)} addonSize={FormatVector(request.AddonSize)} " +
            $"renderedPos={FormatVector(renderedPosition)} renderedSize={FormatVector(renderedSize)} bodySize={FormatVector(bodySize)} " +
            $"scale={request.ScaleMultiplier:0.##} alpha={request.AlphaMultiplier:0.##}");
    }

    /// <summary>
    /// Determines whether texture-overlay diagnostics should run for the current
    /// language mode.
    /// </summary>
    /// <param name="configuration">The active plugin configuration.</param>
    /// <returns><see langword="true" /> when diagnostics should log.</returns>
    private static bool ShouldLog(Config configuration)
    {
        return configuration.OverlayOnlyLanguage &&
               LanguagePresentationPolicy.UsesTexturePresentation(configuration.Lang);
    }

    /// <summary>
    /// Determines whether a deduplicated diagnostic line should be emitted.
    /// </summary>
    /// <param name="key">The stable diagnostic event key.</param>
    /// <param name="signature">The event-state signature.</param>
    /// <returns><see langword="true" /> when the line should be emitted.</returns>
    private static bool ShouldEmit(string key, string signature)
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
    /// Builds the preview key used for deduplication.
    /// </summary>
    /// <param name="text">The source text.</param>
    /// <returns>The stable preview key.</returns>
    private static string BuildPreviewKey(string text)
    {
        return BuildPreview(text)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }

    /// <summary>
    /// Builds a short preview for diagnostic log lines.
    /// </summary>
    /// <param name="text">The source text.</param>
    /// <returns>The shortened preview text.</returns>
    private static string BuildPreview(string text)
    {
        var normalized = (text ?? string.Empty)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        if (normalized.Length <= 72)
        {
            return normalized;
        }

        return normalized[..72] + "…";
    }

    /// <summary>
    /// Rounds a vector to a coarse step size for deduplication.
    /// </summary>
    /// <param name="value">The source vector.</param>
    /// <param name="step">The coarse rounding step.</param>
    /// <returns>The rounded vector.</returns>
    private static Vector2 RoundVector(Vector2 value, float step)
    {
        if (step <= 0f)
        {
            return value;
        }

        return new Vector2(
            RoundToStep(value.X, step),
            RoundToStep(value.Y, step));
    }

    /// <summary>
    /// Rounds a scalar to the requested step size.
    /// </summary>
    /// <param name="value">The source value.</param>
    /// <param name="step">The rounding step.</param>
    /// <returns>The rounded scalar.</returns>
    private static float RoundToStep(float value, float step)
    {
        return step <= 0f
            ? value
            : MathF.Round(value / step) * step;
    }

    /// <summary>
    /// Formats a vector for diagnostic log output.
    /// </summary>
    /// <param name="value">The vector value.</param>
    /// <returns>The formatted vector.</returns>
    private static string FormatVector(Vector2 value)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"({value.X:0.0},{value.Y:0.0})");
    }

    /// <summary>
    /// Captures the last emitted signature and time for one diagnostic key.
    /// </summary>
    /// <param name="Signature">The emitted diagnostic-state signature.</param>
    /// <param name="EmittedAtUtc">The time the signature was emitted.</param>
    private sealed record EmissionState(
        string Signature,
        DateTime EmittedAtUtc);
}
#endif
