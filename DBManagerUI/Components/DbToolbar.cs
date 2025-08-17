// <copyright file="DbToolbar.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the CC BY-NC-ND 4.0 International Public License.
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
      if (ImGui.Button("Reload"))
      {
        this.onReload();
      }

      ImGui.SameLine();
      ImGui.Text("|");

      ImGui.SameLine();
      if (ImGui.Button("Prev") && page > 0)
      {
        this.onPrev();
      }

      ImGui.SameLine();
      ImGui.Text($"Page {page + 1}");

      ImGui.SameLine();
      if (ImGui.Button("Next"))
      {
        this.onNext();
      }

      ImGui.SameLine();
      ImGui.Text("|");

      ImGui.SameLine();
      ImGui.Text("Page size:");
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
      if (ImGui.Button("Export Selected CSV"))
      {
        this.onExportSelected();
      }

      ImGui.SameLine();
      if (ImGui.Button("Export Page CSV"))
      {
        this.onExportPage();
      }

      ImGui.SameLine();
      if (ImGui.Button("Delete Selected"))
      {
        this.onDeleteSelected();
      }
    }
  }
}
