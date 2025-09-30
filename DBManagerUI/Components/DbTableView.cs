// <copyright file="DbTableView.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.DBManagerUI.Components
{
  /// <summary>
  /// Tabular view with multi-select and reliable double-click to open editor.
  /// Long text cells are wrapped (with optional tooltip for very long content).
  /// </summary>
  public class DbTableView
  {
    private readonly Func<IReadOnlyList<IProperty>?> getScalarProps;
    private readonly Func<IList<object>?> getRows;
    private readonly Func<HashSet<int>> getSelection;
    private readonly Action<object> onRowDoubleClick;

    /// <summary>
    /// Initializes a new instance of the <see cref="DbTableView"/> class.
    /// </summary>
    /// <param name="getScalarProps">Accessor for current scalar properties.</param>
    /// <param name="getRows">Accessor for current page rows.</param>
    /// <param name="getSelection">Accessor for current selection hash set.</param>
    /// <param name="onRowDoubleClick">Callback when a row is double-clicked.</param>
    public DbTableView(
      Func<IReadOnlyList<IProperty>?> getScalarProps,
      Func<IList<object>?> getRows,
      Func<HashSet<int>> getSelection,
      Action<object> onRowDoubleClick)
    {
      this.getScalarProps = getScalarProps;
      this.getRows = getRows;
      this.getSelection = getSelection;
      this.onRowDoubleClick = onRowDoubleClick;
    }

    /// <summary>
    /// Draws the table and handles selection and double-click.
    /// </summary>
    public void Draw()
    {
      var props = this.getScalarProps();
      var rows = this.getRows();

      if (rows == null)
      {
        ImGui.Text("No records loaded.");
        return;
      }

      if (rows.Count == 0)
      {
        ImGui.Text("No records found in this table.");
        return;
      }

      if (props == null || props.Count == 0)
      {
        ImGui.Text("No scalar properties to display.");
        return;
      }

      int totalColumns = 1 + props.Count;

      if (ImGui.BeginTable("##dbTable", totalColumns, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable))
      {
        ImGui.TableSetupScrollFreeze(0, 1);

        ImGui.TableSetupColumn("##sel", ImGuiTableColumnFlags.WidthFixed, 28f);

        foreach (var prop in props)
        {
          ImGui.TableSetupColumn(prop.Name, ImGuiTableColumnFlags.None, 150f);
        }

        ImGui.TableHeadersRow();

        var selection = this.getSelection();

        for (int i = 0; i < rows.Count; i++)
        {
          var row = rows[i];

          ImGui.TableNextRow();

          // Selection checkbox column
          ImGui.TableSetColumnIndex(0);
          bool isSelected = selection.Contains(i);
          if (ImGui.Checkbox($"##chk{i}", ref isSelected))
          {
            if (isSelected)
            {
              selection.Add(i);
            }
            else
            {
              selection.Remove(i);
            }
          }

          // Data columns
          for (int c = 0; c < props.Count; c++)
          {
            var prop = props[c];
            ImGui.TableSetColumnIndex(c + 1);

            object? val = this.SafeGetValue(row, prop.PropertyInfo!);
            string text = this.RenderCellValue(val);

            ImGui.PushID((i * 10000) + c);

            // compute a wrap width equal to the current column content width
            float wrapAt = ImGui.GetCursorPosX() + ImGui.GetColumnWidth();

            // draw wrapped text item
            ImGui.PushTextWrapPos(wrapAt);
            ImGui.TextUnformatted(text);
            ImGui.PopTextWrapPos();

            // tooltip for very long strings
            if (text.Length > 256 && ImGui.IsItemHovered())
            {
              ImGui.BeginTooltip();
              ImGui.PushTextWrapPos(ImGui.GetFontSize() * 60.0f);
              ImGui.TextUnformatted(text);
              ImGui.PopTextWrapPos();
              ImGui.EndTooltip();
            }

            // reliable double-click detection on this item
            if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
              this.onRowDoubleClick(row);
            }

            ImGui.PopID();
          }
        }

        ImGui.EndTable();
      }
    }

    private object? SafeGetValue(object obj, PropertyInfo pi)
    {
      try
      {
        return pi.GetValue(obj);
      }
      catch
      {
        return null;
      }
    }

    private string RenderCellValue(object? val)
    {
      if (val == null)
      {
        return "(null)";
      }

      if (val is byte[] bytes)
      {
        return $"[BLOB {bytes.Length} bytes]";
      }

      string s = val.ToString() ?? string.Empty;
      if (s.Length > 256)
      {
        s = s.Substring(0, 256) + "…";
      }

      return s;
    }
  }
}
