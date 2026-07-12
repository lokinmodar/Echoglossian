// <copyright file="InspectionTableView.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.DBManagerUI.Components
{
  /// <summary>
  /// Renders a reusable wrapped-text inspection table with optional selection
  /// and double-click callbacks.
  /// </summary>
  public sealed class InspectionTableView
  {
    private readonly string tableId;
    private readonly Func<IReadOnlyList<InspectionColumnDefinition>?> getColumns;
    private readonly Func<IReadOnlyList<InspectionRow>?> getRows;
    private readonly Func<HashSet<int>>? getSelection;
    private readonly Action<InspectionRow>? onRowDoubleClick;
    private readonly string noRecordsLoadedMessage;
    private readonly string noRecordsFoundMessage;
    private readonly string noColumnsMessage;

    /// <summary>
    /// Initializes a new instance of the <see cref="InspectionTableView"/> class.
    /// </summary>
    /// <param name="tableId">Stable ImGui table identifier.</param>
    /// <param name="getColumns">Accessor for current column definitions.</param>
    /// <param name="getRows">Accessor for current rows.</param>
    /// <param name="noRecordsLoadedMessage">Message shown when rows are unavailable.</param>
    /// <param name="noRecordsFoundMessage">Message shown when the table is empty.</param>
    /// <param name="noColumnsMessage">Message shown when no columns are available.</param>
    /// <param name="getSelection">Optional selection state accessor.</param>
    /// <param name="onRowDoubleClick">Optional double-click callback.</param>
    public InspectionTableView(
      string tableId,
      Func<IReadOnlyList<InspectionColumnDefinition>?> getColumns,
      Func<IReadOnlyList<InspectionRow>?> getRows,
      string noRecordsLoadedMessage,
      string noRecordsFoundMessage,
      string noColumnsMessage,
      Func<HashSet<int>>? getSelection = null,
      Action<InspectionRow>? onRowDoubleClick = null)
    {
      this.tableId = tableId;
      this.getColumns = getColumns;
      this.getRows = getRows;
      this.noRecordsLoadedMessage = noRecordsLoadedMessage;
      this.noRecordsFoundMessage = noRecordsFoundMessage;
      this.noColumnsMessage = noColumnsMessage;
      this.getSelection = getSelection;
      this.onRowDoubleClick = onRowDoubleClick;
    }

    /// <summary>
    /// Draws the inspection table.
    /// </summary>
    public void Draw()
    {
      var columns = this.getColumns();
      var rows = this.getRows();

      if (rows == null)
      {
        ImGui.Text(this.noRecordsLoadedMessage);
        return;
      }

      if (rows.Count == 0)
      {
        ImGui.Text(this.noRecordsFoundMessage);
        return;
      }

      if (columns == null || columns.Count == 0)
      {
        ImGui.Text(this.noColumnsMessage);
        return;
      }

      bool supportsSelection = this.getSelection != null;
      int totalColumns = columns.Count + (supportsSelection ? 1 : 0);

      if (!ImGui.BeginTable(
            $"##{this.tableId}",
            totalColumns,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable))
      {
        return;
      }

      ImGui.TableSetupScrollFreeze(0, 1);

      if (supportsSelection)
      {
        ImGui.TableSetupColumn("##sel", ImGuiTableColumnFlags.WidthFixed, 28f);
      }

      foreach (var column in columns)
      {
        ImGui.TableSetupColumn(column.Header, column.Flags, column.InitialWidth);
      }

      ImGui.TableHeadersRow();

      var selection = supportsSelection ? this.getSelection!() : null;

      for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
      {
        var row = rows[rowIndex];
        ImGui.TableNextRow();

        if (selection != null)
        {
          this.DrawSelectionCell(selection, rowIndex);
        }

        for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
        {
          ImGui.TableSetColumnIndex(columnIndex + (selection != null ? 1 : 0));
          this.DrawDataCell(rowIndex, columnIndex, row);
        }
      }

      ImGui.EndTable();
    }

    /// <summary>
    /// Draws the optional selection checkbox cell for a row.
    /// </summary>
    /// <param name="selection">Selection state store.</param>
    /// <param name="rowIndex">The row index.</param>
    private void DrawSelectionCell(HashSet<int> selection, int rowIndex)
    {
      ImGui.TableSetColumnIndex(0);

      bool isSelected = selection.Contains(rowIndex);
      if (!ImGui.Checkbox($"##chk{rowIndex}", ref isSelected))
      {
        return;
      }

      if (isSelected)
      {
        selection.Add(rowIndex);
      }
      else
      {
        selection.Remove(rowIndex);
      }
    }

    /// <summary>
    /// Draws a single wrapped-text data cell.
    /// </summary>
    /// <param name="rowIndex">The row index.</param>
    /// <param name="columnIndex">The zero-based data column index.</param>
    /// <param name="row">The row being rendered.</param>
    private void DrawDataCell(int rowIndex, int columnIndex, InspectionRow row)
    {
      InspectionCell cell = columnIndex < row.Cells.Count
        ? row.Cells[columnIndex]
        : new InspectionCell(string.Empty);

      ImGui.PushID($"{this.tableId}-{rowIndex}-{columnIndex}");

      float wrapAt = ImGui.GetCursorPosX() + ImGui.GetColumnWidth();
      ImGui.PushTextWrapPos(wrapAt);
      ImGui.TextUnformatted(cell.Text);
      ImGui.PopTextWrapPos();

      if (!string.IsNullOrEmpty(cell.TooltipText) && ImGui.IsItemHovered())
      {
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 60.0f);
        ImGui.TextUnformatted(cell.TooltipText);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
      }

      if (this.onRowDoubleClick != null &&
          ImGui.IsItemHovered() &&
          ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
      {
        this.onRowDoubleClick(row);
      }

      ImGui.PopID();
    }
  }
}
