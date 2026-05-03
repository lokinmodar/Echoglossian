// <copyright file="DbEditorWindow.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.DBManagerUI
{
  /// <summary>
  /// Main DB editor window orchestrating components.
  /// </summary>
  public class DbEditorWindow
  {
    private readonly EchoglossianDbContext dbContext;
    private readonly DbMetadataCache metadata;
    private readonly DbSetAccessor setAccessor;
    private readonly CsvExporter csvExporter;
    private readonly DbToolbar toolbar;
    private readonly DbTableView tableView;
    private readonly EditModal editModal;

    private string? selectedTable;
    private int page = 0;
    private int pageSize = 20;

    private IList<object>? currentRows;
    private List<string>? tableNames;

    private readonly HashSet<int> selectedRowIndices = new();

    /// <summary>
    /// Controls whether the window is open and visible.
    /// </summary>
    public bool IsOpen = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="DbEditorWindow"/> class.
    /// </summary>
    /// <param name="dbContext">EF Core DbContext.</param>
    public DbEditorWindow(EchoglossianDbContext dbContext)
    {
      this.dbContext = dbContext;
      this.metadata = new DbMetadataCache();
      this.setAccessor = new DbSetAccessor();
      this.csvExporter = new CsvExporter();

      this.toolbar = new DbToolbar(
        onReload: this.LoadRows,
        onPrev: () =>
        {
          this.page = Math.Max(0, this.page - 1);
          this.LoadRows();
        },
        onNext: () =>
        {
          this.page += 1;
          this.LoadRows();
        },
        onPageSizeChange: (sz) =>
        {
          this.pageSize = Math.Max(1, sz);
          this.page = 0;
          this.LoadRows();
        },
        onExportSelected: () => this.ExportCsv(selectedOnly: true),
        onExportPage: () => this.ExportCsv(selectedOnly: false),
        onDeleteSelected: this.BatchDeleteSelected);

      this.tableView = new DbTableView(
        getScalarProps: () => this.metadata.CurrentScalarProps,
        getRows: () => this.currentRows,
        getSelection: () => this.selectedRowIndices,
        onRowDoubleClick: this.OpenEditor);

      this.editModal = new EditModal(
        getScalarProps: () => this.metadata.CurrentScalarProps,
        getPkNames: () => this.metadata.CurrentPkNames,
        onSave: this.OnSaveEdit,
        onDelete: this.OnDeleteEdit);

      this.InitializeTableNames();
    }

    /// <summary>
    /// Draws the window and its components.
    /// </summary>
    public void Draw()
    {
      if (!this.IsOpen)
      {
        return;
      }

      // Make first open fill the viewport work area (prevents wasted space).
      var vp = ImGui.GetMainViewport();
      ImGui.SetNextWindowPos(vp.WorkPos, ImGuiCond.FirstUseEver);
      ImGui.SetNextWindowSize(vp.WorkSize, ImGuiCond.FirstUseEver);

      if (!ImGui.Begin(Resources.EchoglossianDBEditor, ref this.IsOpen, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.MenuBar))
      {
        ImGui.End();
        return;
      }

      this.DrawMenuBar();

      ImGui.Columns(2);

      // Sidebar list
      ImGui.BeginChild("##DbTableList", new Vector2(240, -1), true);
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

      // Content
      ImGui.BeginChild("##DbTableContent", new Vector2(-1, -1), true);
      if (this.selectedTable == null)
      {
        ImGui.Text(Resources.SelectATableToViewItsRecords);
      }
      else
      {
        this.toolbar.Draw(title: $"{Resources.Table}: {this.selectedTable}", page: this.page, pageSize: this.pageSize);

        ImGui.Separator();

        this.tableView.Draw();
      }

      ImGui.EndChild();
      ImGui.Columns(1);

      this.editModal.Draw();

      ImGui.End();
    }

    /// <summary>
    /// Initializes the entity (table) name list.
    /// </summary>
    private void InitializeTableNames()
    {
      try
      {
        this.tableNames = this.dbContext.Model.GetEntityTypes()
          .Select(t => t.ClrType.Name)
          .OrderBy(n => n)
          .ToList();
        PluginRuntimeLog.Debug($"[DbEditorWindow] Tables: {string.Join(", ", this.tableNames)}");
      }
      catch (Exception ex)
      {
        PluginRuntimeLog.Error($"[DbEditorWindow] Failed to get tables: {ex}");
        this.tableNames = new List<string>();
      }
    }

    /// <summary>
    /// Loads current page rows and entity metadata.
    /// </summary>
    private void LoadRows()
    {
      this.selectedRowIndices.Clear();
      this.metadata.Clear();

      if (this.selectedTable == null)
      {
        this.currentRows = null;
        return;
      }

      try
      {
        var et = this.dbContext.Model.GetEntityTypes()
          .FirstOrDefault(t => t.ClrType.Name == this.selectedTable);
        if (et == null)
        {
          PluginRuntimeLog.Error($"[DbEditorWindow] Entity type not found: {this.selectedTable}");
          this.currentRows = null;
          return;
        }

        this.metadata.Cache(et);

        var set = this.setAccessor.GetDbSet(this.dbContext, et.ClrType);
        var query = (set as IQueryable)!;

        this.currentRows = query
          .Cast<object>()
          .Skip(this.page * this.pageSize)
          .Take(this.pageSize)
          .ToList();

        if (this.currentRows.Count == 0)
        {
          PluginRuntimeLog.Debug($"[DbEditorWindow] No records found in {this.selectedTable}.");
          NotificationManager.AddNotification(new Notification
          {
            Content = Resources.NoRecordsFoundInThisTable,
            Title = Resources.Name,
            Icon = NotificationUtilities.ToNotificationIcon(FontAwesomeIcon.Database),
          });
        }
        else
        {
          PluginRuntimeLog.Debug($"[DbEditorWindow] Loaded {this.currentRows.Count} row(s) from {this.selectedTable} page {this.page} size {this.pageSize}");
          NotificationManager.AddNotification(new Notification
          {
            Content = this.SafeFormat(Resources.LoadedNRecords, this.currentRows.Count),
            Title = Resources.Name,
            Icon = NotificationUtilities.ToNotificationIcon(FontAwesomeIcon.Database),
          });
        }
      }
      catch (Exception ex)
      {
        PluginRuntimeLog.Error($"[DbEditorWindow] LoadRows failed: {ex}");
        this.currentRows = null;
      }
    }

    /// <summary>
    /// Opens the edit modal for a given entity.
    /// </summary>
    /// <param name="entity">Entity instance.</param>
    private void OpenEditor(object entity)
    {
      PluginRuntimeLog.Debug($"[DbEditorWindow] Opening editor for type={entity.GetType().Name}");
      this.editModal.Open(entity);
    }

    /// <summary>
    /// Save handler for the edit modal.
    /// </summary>
    /// <param name="updatedEntity">Entity with updated values already set by the modal.</param>
    private void OnSaveEdit(object updatedEntity)
    {
      try
      {
        this.dbContext.Update(updatedEntity);
        this.dbContext.SaveChanges();
        PluginRuntimeLog.Debug("[DbEditorWindow] Record saved.");
        NotificationManager.AddNotification(new Notification
        {
          Content = Resources.RecordSaved,
          Title = Resources.Name,
          Icon = NotificationUtilities.ToNotificationIcon(FontAwesomeIcon.Database),
        });
        this.editModal.Close();
        this.LoadRows();
      }
      catch (Exception ex)
      {
        PluginRuntimeLog.Error($"[DbEditorWindow] Save failed: {ex}");
      }
    }

    /// <summary>
    /// Delete handler for the edit modal.
    /// </summary>
    /// <param name="entity">Entity to delete.</param>
    private void OnDeleteEdit(object entity)
    {
      try
      {
        this.dbContext.Remove(entity);
        this.dbContext.SaveChanges();
        PluginRuntimeLog.Debug("[DbEditorWindow] Record deleted.");
        NotificationManager.AddNotification(new Notification
        {
          Content = Resources.RecordDeleted,
          Title = Resources.Name,
          Icon = NotificationUtilities.ToNotificationIcon(FontAwesomeIcon.Database),
        });
        this.editModal.Close();
        this.LoadRows();
      }
      catch (Exception ex)
      {
        PluginRuntimeLog.Error($"[DbEditorWindow] Delete failed: {ex}");
      }
    }

    /// <summary>
    /// Deletes selected items in the current page.
    /// </summary>
    private void BatchDeleteSelected()
    {
      if (this.currentRows == null || this.currentRows.Count == 0 || this.selectedRowIndices.Count == 0)
      {
        return;
      }

      try
      {
        var toDelete = this.selectedRowIndices
          .Where(i => i >= 0 && i < this.currentRows.Count)
          .Select(i => this.currentRows[i])
          .ToList();

        if (toDelete.Count == 0)
        {
          return;
        }

        this.dbContext.RemoveRange(toDelete);
        this.dbContext.SaveChanges();

        PluginRuntimeLog.Debug($"[DbEditorWindow] Deleted {toDelete.Count} record(s).");
        NotificationManager.AddNotification(new Notification
        {
          Content = this.SafeFormat(Resources.DeletedNRecords, toDelete.Count),
          Title = Resources.Name,
          Icon = NotificationUtilities.ToNotificationIcon(FontAwesomeIcon.Database),
        });
        this.selectedRowIndices.Clear();
        this.LoadRows();
      }
      catch (Exception ex)
      {
        PluginRuntimeLog.Error($"[DbEditorWindow] Batch delete failed: {ex}");
      }
    }

    /// <summary>
    /// Exports CSV (selected or page) to clipboard.
    /// </summary>
    /// <param name="selectedOnly">True for selected, false for page.</param>
    private void ExportCsv(bool selectedOnly)
    {
      if (this.currentRows == null || this.currentRows.Count == 0 || this.metadata.CurrentScalarProps == null)
      {
        return;
      }

      List<object> rows = selectedOnly
        ? this.selectedRowIndices.Where(i => i >= 0 && i < this.currentRows.Count).Select(i => this.currentRows[i]).ToList()
        : this.currentRows.ToList();

      if (rows.Count == 0)
      {
        return;
      }

      try
      {
        string csv = this.csvExporter.BuildCsv(rows, this.metadata.CurrentScalarProps);
        ImGui.SetClipboardText(csv);
        PluginRuntimeLog.Debug($"[DbEditorWindow] CSV copied to clipboard ({rows.Count} row(s)).");
        NotificationManager.AddNotification(new Notification
        {
          Content = this.SafeFormat(Resources.CopiedNRecordsToClipboard, rows.Count),
          Title = Resources.Name,
          Icon = NotificationUtilities.ToNotificationIcon(FontAwesomeIcon.Clipboard),
        });
      }
      catch (Exception ex)
      {
        PluginRuntimeLog.Error($"[DbEditorWindow] CSV export failed: {ex}");
      }
    }

    /// <summary>
    /// Draws menu bar (Help).
    /// </summary>
    private void DrawMenuBar()
    {
      if (ImGui.BeginMenuBar())
      {
        if (ImGui.BeginMenu(Resources.Help))
        {
          ImGui.MenuItem(Resources.TipDoubleClickARowToEdit);
          ImGui.MenuItem(Resources.CSVExportCopiesToClipboard);
          ImGui.EndMenu();
        }

        ImGui.EndMenuBar();
      }
    }

    /// <summary>
    /// Formats a resource string safely; falls back to raw text on error.
    /// </summary>
    /// <param name="format">Resource format string.</param>
    /// <param name="args">Arguments.</param>
    /// <returns>Formatted string, or the original format on error.</returns>
    private string SafeFormat(string format, params object[] args)
    {
      try
      {
        return string.Format(CultureInfo.CurrentUICulture, format, args);
      }
      catch (FormatException ex)
      {
        PluginRuntimeLog.Error($"[DbEditorWindow] Bad resource format: \"{format}\". {ex}");
        return format;
      }
    }
  }
}


