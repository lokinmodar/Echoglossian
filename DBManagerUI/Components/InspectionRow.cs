// <copyright file="InspectionRow.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.DBManagerUI.Components
{
  /// <summary>
  /// Represents a single row in a reusable inspection table.
  /// </summary>
  public sealed class InspectionRow
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="InspectionRow"/> class.
    /// </summary>
    /// <param name="cells">Display-ready cells for the row.</param>
    /// <param name="payload">Optional context payload associated with the row.</param>
    public InspectionRow(IReadOnlyList<InspectionCell> cells, object? payload = null)
    {
      this.Cells = cells;
      this.Payload = payload;
    }

    /// <summary>
    /// Gets the display-ready cells for the row.
    /// </summary>
    public IReadOnlyList<InspectionCell> Cells { get; }

    /// <summary>
    /// Gets the optional row payload for callbacks.
    /// </summary>
    public object? Payload { get; }
  }
}
