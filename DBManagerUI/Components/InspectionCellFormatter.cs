// <copyright file="InspectionCellFormatter.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.DBManagerUI.Components
{
  /// <summary>
  /// Formats raw values into display-ready inspection cells.
  /// </summary>
  public static class InspectionCellFormatter
  {
    /// <summary>
    /// The default inline text limit before overflow is moved to a tooltip.
    /// </summary>
    public const int DefaultInlineTextLimit = 256;

    /// <summary>
    /// Formats a raw value into an inspection cell.
    /// </summary>
    /// <param name="value">The raw value to format.</param>
    /// <param name="inlineTextLimit">The maximum inline text length.</param>
    /// <returns>A display-ready cell.</returns>
    public static InspectionCell Format(object? value, int inlineTextLimit = DefaultInlineTextLimit)
    {
      if (value == null)
      {
        return new InspectionCell("(null)");
      }

      if (value is byte[] bytes)
      {
        return new InspectionCell($"[BLOB {bytes.Length} bytes]");
      }

      string text = value.ToString() ?? string.Empty;
      if (text.Length <= inlineTextLimit)
      {
        return new InspectionCell(text);
      }

      return new InspectionCell(
        text.Substring(0, inlineTextLimit) + "…",
        text);
    }
  }
}
