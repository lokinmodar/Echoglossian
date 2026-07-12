// <copyright file="InspectionColumnDefinition.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.DBManagerUI.Components
{
  /// <summary>
  /// Represents one display column in a reusable inspection table.
  /// </summary>
  public sealed class InspectionColumnDefinition
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="InspectionColumnDefinition"/> class.
    /// </summary>
    /// <param name="header">Column header label.</param>
    /// <param name="flags">ImGui column sizing flags.</param>
    /// <param name="initialWidth">Initial column width.</param>
    public InspectionColumnDefinition(
      string header,
      ImGuiTableColumnFlags flags = ImGuiTableColumnFlags.WidthFixed,
      float initialWidth = 150f)
    {
      this.Header = header;
      this.Flags = flags;
      this.InitialWidth = initialWidth;
    }

    /// <summary>
    /// Gets the column header label.
    /// </summary>
    public string Header { get; }

    /// <summary>
    /// Gets the ImGui sizing flags for the column.
    /// </summary>
    public ImGuiTableColumnFlags Flags { get; }

    /// <summary>
    /// Gets the initial width for the column.
    /// </summary>
    public float InitialWidth { get; }
  }
}
