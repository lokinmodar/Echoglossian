// <copyright file="InspectionCellFormatterTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.DBManagerUI.Components;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers shared inspection-table cell formatting used by DB-backed and
///     debugger-backed views.
/// </summary>
public class InspectionCellFormatterTests
{
  /// <summary>
  ///     Ensures null values are rendered with the DB manager's existing
  ///     placeholder text.
  /// </summary>
  [Fact]
  public void Format_NullValue_UsesNullPlaceholder()
  {
    var cell = InspectionCellFormatter.Format(null);

    Assert.Equal("(null)", cell.Text);
    Assert.Null(cell.TooltipText);
  }

  /// <summary>
  ///     Ensures binary payloads render as size-only placeholders instead of
  ///     dumping raw bytes into inspection tables.
  /// </summary>
  [Fact]
  public void Format_ByteArray_UsesBlobPlaceholder()
  {
    var cell = InspectionCellFormatter.Format(new byte[12]);

    Assert.Equal("[BLOB 12 bytes]", cell.Text);
    Assert.Null(cell.TooltipText);
  }

  /// <summary>
  ///     Ensures long text is truncated inline while preserving the full value
  ///     for hover inspection.
  /// </summary>
  [Fact]
  public void Format_LongString_TruncatesInlineAndPreservesTooltip()
  {
    var sourceText = new string('x', 300);

    var cell = InspectionCellFormatter.Format(sourceText);

    Assert.Equal(new string('x', 256) + "…", cell.Text);
    Assert.Equal(sourceText, cell.TooltipText);
  }

  /// <summary>
  ///     Ensures short values stay unchanged and do not allocate redundant
  ///     tooltip content.
  /// </summary>
  [Fact]
  public void Format_ShortString_PreservesTextWithoutTooltip()
  {
    const string sourceText = "visible story surface";

    var cell = InspectionCellFormatter.Format(sourceText);

    Assert.Equal(sourceText, cell.Text);
    Assert.Null(cell.TooltipText);
  }
}
