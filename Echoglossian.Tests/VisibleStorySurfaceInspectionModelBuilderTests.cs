// <copyright file="VisibleStorySurfaceInspectionModelBuilderTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;
using Echoglossian.PluginUI.Helpers;
using Echoglossian.Properties;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers debugger-facing story-surface inspection model helpers.
/// </summary>
public class VisibleStorySurfaceInspectionModelBuilderTests
{
  /// <summary>
  ///     Ensures CutSceneSelectString diagnostics resolve to the SelectString table.
  /// </summary>
  [Fact]
  public void Resolve_MapsCutSceneSelectStringToSelectString()
  {
    Assert.Equal(
        "SelectString",
        VisibleStorySurfaceTableMap.Resolve(
            VisibleStorySurfaceKind.CutSceneSelectString));
  }

  /// <summary>
  ///     Ensures unknown story surfaces fail loudly instead of silently
  ///     reusing the TalkMessage table mapping.
  /// </summary>
  [Fact]
  public void Resolve_ThrowsForUnknownSurfaceMapping()
  {
    Assert.Throws<ArgumentOutOfRangeException>(
        () => VisibleStorySurfaceTableMap.Resolve(
            (VisibleStorySurfaceKind)999));
  }

  /// <summary>
  ///     Ensures option payloads become explicit inspection rows for debugger display.
  /// </summary>
  [Fact]
  public void BuildRows_IncludesOptionsWhenPresent()
  {
    var rows = VisibleStorySurfaceInspectionModelBuilder.BuildRows(
        new VisibleStorySurfaceDiagnosticsSnapshot(
            VisibleStorySurfaceKind.CutSceneSelectString,
            VisibleStorySurfaceProvenanceKind.FreshLiveTranslation,
            "SelectString",
            string.Empty,
            "Question",
            "Option A\nOption B",
            string.Empty,
            "Translated question",
            "Translated A\nTranslated B",
            false,
            6,
            new DateTime(2026, 07, 11, 18, 0, 0, DateTimeKind.Utc),
            null,
            null));

    Assert.Contains(
        rows,
        row => row.Cells.Count >= 2 &&
               row.Cells[0].Text ==
               Resources.TranslatorDebuggerVisibleStorySurfaceFieldOriginalOptions &&
               row.Cells[1].Text.StartsWith("Option A", StringComparison.Ordinal));
  }
}
