// <copyright file="ToastTranslationDebugLogTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Toasts;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Verifies the compact toast debug log message format used by surface-aware
///     toast diagnostics.
/// </summary>
public sealed class ToastTranslationDebugLogTests
{
  /// <summary>
  ///     Ensures request logs include the trigger, presentation flags, and a
  ///     single-line source preview.
  /// </summary>
  [Fact]
  public void FormatRequestMessage_IncludesTriggerFlagsAndSourcePreview()
  {
    var message = ToastTranslationDebugLog.FormatRequestMessage(
        "IToastGui.Toast",
        "First line\r\nSecond line",
        usesOverlay: true,
        writesNative: false,
        swapsTexts: true);

    Assert.Equal(
        "trigger=IToastGui.Toast action=request overlay=True native=False swap=True source='First line Second line'",
        message);
  }

  /// <summary>
  ///     Ensures apply logs truncate long preview text to keep the debug output compact.
  /// </summary>
  [Fact]
  public void FormatApplyMessage_TruncatesLongPreviewText()
  {
    var previewText = new string('x', 81);

    var message = ToastTranslationDebugLog.FormatApplyMessage(
        "async-resolve",
        "overlay",
        previewText);

    Assert.Equal(
        $"trigger=async-resolve action=apply target=overlay text='{new string('x', 80)}...'",
        message);
  }
}
