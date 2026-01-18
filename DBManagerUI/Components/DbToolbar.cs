// <copyright file="DbToolbar.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.DBManagerUI.Components
{
  /// <summary>
  /// Toolbar with paging, export, and delete controls.
  /// </summary>
  public class DbToolbar
  {
    private readonly Action onReload;
    private readonly Action onPrev;
    private readonly Action onNext;
    private readonly Action<int> onPageSizeChange;
    private readonly Action onExportSelected;
    private readonly Action onExportPage;
    private readonly Action onDeleteSelected;

    /// <summary>
    /// Initializes a new instance of the <see cref="DbToolbar"/> class.
    /// </summary>
    public DbToolbar(
      Action onReload,
      Action onPrev,
      Action onNext,
      Action<int> onPageSizeChange,
      Action onExportSelected,
      Action onExportPage,
      Action onDeleteSelected)
    {
      this.onReload = onReload;
      this.onPrev = onPrev;
      this.onNext = onNext;
      this.onPageSizeChange = onPageSizeChange;
      this.onExportSelected = onExportSelected;
      this.onExportPage = onExportPage;
      this.onDeleteSelected = onDeleteSelected;
    }

    /// <summary>
    /// Draws the toolbar.
    /// </summary>
    /// <param name="title">Table title.</param>
    /// <param name="page">Current page.</param>
    /// <param name="pageSize">Current page size.</param>
    public void Draw(string title, int page, int pageSize)
    {
      ImGui.TextUnformatted(title);

      ImGui.SameLine();
      if (ImGui.Button(Resources.Reload))
      {
        this.onReload();
      }

      ImGui.SameLine();
      ImGui.Text(Resources._);

      ImGui.SameLine();
      if (ImGui.Button(Resources.Prev) && page > 0)
      {
        this.onPrev();
      }

      ImGui.SameLine();
      ImGui.Text($"{Resources.Page} {page + 1}");

      ImGui.SameLine();
      if (ImGui.Button(Resources.Next))
      {
        this.onNext();
      }

      ImGui.SameLine();
      ImGui.Text(Resources._);

      ImGui.SameLine();
      ImGui.Text(Resources.PageSize);
      ImGui.SameLine();

      int localSize = pageSize;
      if (ImGui.InputInt("##pagesize", ref localSize, 1))
      {
        if (localSize < 1)
        {
          localSize = 1;
        }

        this.onPageSizeChange(localSize);
      }

      float right = ImGui.GetWindowContentRegionMax().X + ImGui.GetWindowPos().X;
      float buttonWidth = 140f;

      ImGui.SameLine(right - (buttonWidth * 3.0f) - 12.0f);
      if (ImGui.Button(Resources.ExportSelectedCSV))
      {
        this.onExportSelected();
      }

      ImGui.SameLine();
      if (ImGui.Button(Resources.ExportPageCSV))
      {
        this.onExportPage();
      }

      ImGui.SameLine();
      if (ImGui.Button(Resources.DeleteSelected))
      {
        this.onDeleteSelected();
      }
    }
  }
}
