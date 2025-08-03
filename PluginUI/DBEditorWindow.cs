// <copyright file="DbEditorWindow.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI
{
  /// <summary>
  /// ImGui window for browsing and editing the Echoglossian EFCore SQLite database.
  /// </summary>
  public class DbEditorWindow
  {
    private readonly EchoglossianDbContext dbContext;

    private string? selectedTable;
    private int page = 0;
    private int pageSize = 20;

    private IList<object>? currentRows;
    private List<string>? tableNames;

    /// <summary>
    /// Controls whether the window is open and visible.
    /// </summary>
    public bool IsOpen = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="DbEditorWindow"/> class.
    /// </summary>
    /// <param name="dbContext">The EFCore DB context.</param>
    public DbEditorWindow(EchoglossianDbContext dbContext)
    {
      this.dbContext = dbContext;
      this.InitializeTableNames();
    }

    /// <summary>
    /// Draws the DB editor ImGui window.
    /// </summary>
    public void Draw()
    {
      if (!this.IsOpen)
        return;

      // You may want to use ImGui.SetNextWindowSize for default sizing:
      ImGui.SetNextWindowSize(new System.Numerics.Vector2(1100, 700), ImGuiCond.FirstUseEver);

      if (!ImGui.Begin("Echoglossian DB Editor", ref this.IsOpen, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.MenuBar))
      {
        ImGui.End();
        return;
      }

      ImGui.Columns(2);

      // Sidebar: Table/entity list
      ImGui.BeginChild("##DbTableList", new System.Numerics.Vector2(210, -1), true);
      if (this.tableNames is not null)
      {
        foreach (var table in this.tableNames)
        {
          if (ImGui.Selectable(table, this.selectedTable == table))
          {
            if (this.selectedTable != table)
            {
              this.selectedTable = table;
              this.page = 0;
              this.LoadRows();
            }
          }
        }
      }
      ImGui.EndChild();
      ImGui.NextColumn();

      // Main content: Records view for the selected table
      ImGui.BeginChild("##DbTableContent", new System.Numerics.Vector2(-1, -1), true);
      if (this.selectedTable == null)
      {
        ImGui.Text("Select a table to view its records.");
      }
      else
      {
        ImGui.TextUnformatted($"Table: {this.selectedTable}");

        // Paging controls
        ImGui.Spacing();
        if (ImGui.Button("Previous") && this.page > 0)
        {
          this.page--;
          this.LoadRows();
        }
        ImGui.SameLine();
        ImGui.Text($"Page {this.page + 1}");
        ImGui.SameLine();
        if (ImGui.Button("Next"))
        {
          this.page++;
          this.LoadRows();
        }
        ImGui.Separator();

        // Table view
        if (this.currentRows == null)
        {
          ImGui.Text("No records loaded.");
        }
        else if (this.currentRows.Count == 0)
        {
          ImGui.Text("No records found in this table.");
        }
        else
        {
          ImGui.Text($"Loaded {this.currentRows.Count} records.");
          // TODO: Draw table rows/columns here.
        }
      }
      ImGui.EndChild();
      ImGui.Columns(1);

      ImGui.End(); // End main window
    }

    /// <summary>
    /// Loads the list of table/entity names from the DbContext model.
    /// </summary>
    private void InitializeTableNames()
    {
      try
      {
        this.tableNames = this.dbContext.Model.GetEntityTypes()
            .Select(t => t.ClrType.Name)
            .OrderBy(n => n)
            .ToList();
        PluginLog.Debug($"[DbEditorWindow] Table names initialized: {string.Join(", ", this.tableNames)}");
      }
      catch (Exception ex)
      {
        PluginLog.Error($"[DbEditorWindow] Failed to get table names: {ex}");
        this.tableNames = new List<string>();
      }
    }

    /// <summary>
    /// Loads records for the currently selected table and page.
    /// </summary>
    private void LoadRows()
    {
      if (this.selectedTable == null)
      {
        this.currentRows = null;
        return;
      }

      try
      {
        var entityType = this.dbContext.Model.GetEntityTypes()
            .FirstOrDefault(t => t.ClrType.Name == this.selectedTable);
        if (entityType == null)
        {
          PluginLog.Error($"[DbEditorWindow] Entity type not found for table: {this.selectedTable}");
          this.currentRows = null;
          return;
        }

        var setMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes);
        var genericSetMethod = setMethod!.MakeGenericMethod(entityType.ClrType);
        var set = genericSetMethod.Invoke(this.dbContext, null);

        var rows = ((IQueryable<object>)set)
            .Skip(this.page * this.pageSize)
            .Take(this.pageSize)
            .ToList();

        this.currentRows = rows;
        PluginLog.Debug($"[DbEditorWindow] Loaded {rows.Count} records from {this.selectedTable}, page {this.page}");
      }
      catch (Exception ex)
      {
        PluginLog.Error($"[DbEditorWindow] Failed to load rows for table {this.selectedTable}: {ex}");
        this.currentRows = null;
      }
    }
  }
}
