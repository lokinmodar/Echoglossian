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
    private readonly InspectionTableView inspectionTableView;

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
      this.inspectionTableView = new InspectionTableView(
        tableId: "dbTable",
        getColumns: this.BuildColumns,
        getRows: this.BuildRows,
        noRecordsLoadedMessage: Resources.NoRecordsLoaded,
        noRecordsFoundMessage: Resources.NoRecordsFoundInThisTable,
        noColumnsMessage: Resources.NoScalarPropertiesToDisplay,
        getSelection: getSelection,
        onRowDoubleClick: (row) =>
        {
          if (row.Payload != null)
          {
            onRowDoubleClick(row.Payload);
          }
        });
    }

    /// <summary>
    /// Draws the table and handles selection and double-click.
    /// </summary>
    public void Draw()
    {
      this.inspectionTableView.Draw();
    }

    private IReadOnlyList<InspectionColumnDefinition>? BuildColumns()
    {
      var props = this.getScalarProps();
      if (props == null)
      {
        return null;
      }

      return props
        .Select(prop => new InspectionColumnDefinition(prop.Name))
        .ToList();
    }

    private IReadOnlyList<InspectionRow>? BuildRows()
    {
      var props = this.getScalarProps();
      var rows = this.getRows();
      if (rows == null)
      {
        return null;
      }

      if (props == null || props.Count == 0)
      {
        return new List<InspectionRow>();
      }

      return rows
        .Select(row => new InspectionRow(
          props
            .Select(prop => InspectionCellFormatter.Format(this.SafeGetValue(row, prop.PropertyInfo!)))
            .ToList(),
          row))
        .ToList();
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
  }
}
