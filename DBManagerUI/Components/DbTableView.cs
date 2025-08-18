// <copyright file="DbTableView.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the CC BY-NC-ND 4.0 International Public License.
// </copyright>
namespace Echoglossian.DBManagerUI.Components
{
  /// <summary>
  /// Tabular view with multi-select and reliable double-click to open editor.
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
        ImGui.Text(Resources.NoRecordsLoaded);
        return;
      }

      if (rows.Count == 0)
      {
        ImGui.Text(Resources.NoRecordsFoundInThisTable);
        return;
      }

      if (props == null || props.Count == 0)
      {
        ImGui.Text(Resources.NoScalarPropertiesToDisplay);
        return;
      }

      int totalColumns = 1 + props.Count;

      if (ImGui.BeginTable("##dbTable", totalColumns, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable))
      {
        ImGui.TableSetupScrollFreeze(0, 1);

        ImGui.TableSetupColumn("##sel", ImGuiTableColumnFlags.WidthFixed, 28f);

        foreach (var prop in props)
        {
          ImGui.TableSetupColumn(prop.Name, ImGuiTableColumnFlags.None, 120f);
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

            ImGui.PushID(i * 10000 + c);

            // Draw the selectable cell
            ImGui.Selectable(text, false, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowDoubleClick);

            // Reliable double-click detection on the just-drawn item
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
