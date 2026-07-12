// <copyright file="InspectionCell.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.DBManagerUI.Components
{
  /// <summary>
  /// Represents a single display-ready inspection cell.
  /// </summary>
  public sealed class InspectionCell
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="InspectionCell"/> class.
    /// </summary>
    /// <param name="text">Inline text shown in the table cell.</param>
    /// <param name="tooltipText">Optional full text shown on hover.</param>
    public InspectionCell(string text, string? tooltipText = null)
    {
      this.Text = text;
      this.TooltipText = tooltipText;
    }

    /// <summary>
    /// Gets the inline text shown in the table cell.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the optional full text shown on hover.
    /// </summary>
    public string? TooltipText { get; }
  }
}
