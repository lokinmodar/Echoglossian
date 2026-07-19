// <copyright file="ToastTranslationDebugLog.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Toasts;

/// <summary>
///     Formats concise surface-aware toast diagnostics for debug builds so
///     translation activity can be traced back to the owning runtime without
///     leaving permanent hot-path noise in release builds.
/// </summary>
internal static class ToastTranslationDebugLog
{
  /// <summary>
  ///     Logs one translation request with the active presentation path.
  /// </summary>
  public static void Request(
      string surfaceIdentity,
      string trigger,
      string originalText,
      bool usesOverlay,
      bool writesNative,
      bool swapsTexts)
  {
    PluginRuntimeLog.Debug(
        surfaceIdentity,
        FormatRequestMessage(
            trigger,
            originalText,
            usesOverlay,
            writesNative,
            swapsTexts));
  }

  /// <summary>
  ///     Logs one translation reuse from cache or persisted storage.
  /// </summary>
  public static void Reuse(
      string surfaceIdentity,
      string trigger,
      string reuseSource)
  {
    PluginRuntimeLog.Debug(
        surfaceIdentity,
        FormatReuseMessage(trigger, reuseSource));
  }

  /// <summary>
  ///     Logs one queued translation request.
  /// </summary>
  public static void Queued(
      string surfaceIdentity,
      string trigger,
      int requestId)
  {
    PluginRuntimeLog.Debug(
        surfaceIdentity,
        FormatQueuedMessage(trigger, requestId));
  }

  /// <summary>
  ///     Logs one application target such as overlay or native replacement.
  /// </summary>
  public static void Apply(
      string surfaceIdentity,
      string trigger,
      string target,
      string? previewText = null)
  {
    PluginRuntimeLog.Debug(
        surfaceIdentity,
        FormatApplyMessage(trigger, target, previewText));
  }

  /// <summary>
  ///     Logs one intentional skip path.
  /// </summary>
  public static void Skip(
      string surfaceIdentity,
      string trigger,
      string reason)
  {
    PluginRuntimeLog.Debug(
        surfaceIdentity,
        FormatSkipMessage(trigger, reason));
  }

  /// <summary>
  ///     Logs one failed translation path.
  /// </summary>
  public static void Failure(
      string surfaceIdentity,
      string trigger,
      string reason)
  {
    PluginRuntimeLog.Debug(
        surfaceIdentity,
        FormatFailureMessage(trigger, reason));
  }

  /// <summary>
  ///     Formats one request log payload without the outer surface scope.
  /// </summary>
  /// <param name="trigger">The runtime trigger label.</param>
  /// <param name="originalText">The captured source text.</param>
  /// <param name="usesOverlay">Whether the overlay presentation path is active.</param>
  /// <param name="writesNative">Whether native replacement is active.</param>
  /// <param name="swapsTexts">Whether swap mode is active.</param>
  /// <returns>The formatted request payload.</returns>
  internal static string FormatRequestMessage(
      string trigger,
      string originalText,
      bool usesOverlay,
      bool writesNative,
      bool swapsTexts)
  {
    return $"trigger={trigger} action=request overlay={usesOverlay} native={writesNative} swap={swapsTexts} source='{Preview(originalText)}'";
  }

  /// <summary>
  ///     Formats one reuse log payload without the outer surface scope.
  /// </summary>
  /// <param name="trigger">The runtime trigger label.</param>
  /// <param name="reuseSource">The cache or persistence source label.</param>
  /// <returns>The formatted reuse payload.</returns>
  internal static string FormatReuseMessage(
      string trigger,
      string reuseSource)
  {
    return $"trigger={trigger} action=reuse source={reuseSource}";
  }

  /// <summary>
  ///     Formats one queued-request log payload without the outer surface scope.
  /// </summary>
  /// <param name="trigger">The runtime trigger label.</param>
  /// <param name="requestId">The queued request identifier.</param>
  /// <returns>The formatted queued payload.</returns>
  internal static string FormatQueuedMessage(
      string trigger,
      int requestId)
  {
    return $"trigger={trigger} action=queued request={requestId}";
  }

  /// <summary>
  ///     Formats one apply log payload without the outer surface scope.
  /// </summary>
  /// <param name="trigger">The runtime trigger label.</param>
  /// <param name="target">The apply target label.</param>
  /// <param name="previewText">Optional preview text for the target payload.</param>
  /// <returns>The formatted apply payload.</returns>
  internal static string FormatApplyMessage(
      string trigger,
      string target,
      string? previewText = null)
  {
    var previewSuffix = string.IsNullOrWhiteSpace(previewText)
        ? string.Empty
        : $" text='{Preview(previewText)}'";
    return $"trigger={trigger} action=apply target={target}{previewSuffix}";
  }

  /// <summary>
  ///     Formats one skip log payload without the outer surface scope.
  /// </summary>
  /// <param name="trigger">The runtime trigger label.</param>
  /// <param name="reason">The reason the path was skipped.</param>
  /// <returns>The formatted skip payload.</returns>
  internal static string FormatSkipMessage(
      string trigger,
      string reason)
  {
    return $"trigger={trigger} action=skip reason={reason}";
  }

  /// <summary>
  ///     Formats one failure log payload without the outer surface scope.
  /// </summary>
  /// <param name="trigger">The runtime trigger label.</param>
  /// <param name="reason">The failure reason.</param>
  /// <returns>The formatted failure payload.</returns>
  internal static string FormatFailureMessage(
      string trigger,
      string reason)
  {
    return $"trigger={trigger} action=failure reason={reason}";
  }

  /// <summary>
  ///     Produces one short single-line preview for text-bearing diagnostics.
  /// </summary>
  /// <param name="text">The text to preview.</param>
  /// <returns>The normalized preview text.</returns>
  private static string Preview(string text)
  {
    var preview = Regex.Replace(text ?? string.Empty, @"\s+", " ").Trim();
    const int maxLength = 80;
    return preview.Length <= maxLength
        ? preview
        : preview[..maxLength] + "...";
  }
}
