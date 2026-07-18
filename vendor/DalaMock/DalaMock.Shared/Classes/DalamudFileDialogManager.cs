namespace DalaMock.Shared.Classes;

using System;
using System.Collections.Generic;

using DalaMock.Shared.Interfaces;

using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;

/// <summary>
/// The dalamud implementation of file dialog manager, wrapped to avoid font crashes.
/// </summary>
public class DalamudFileDialogManager : IFileDialogManager
{
    private readonly FileDialogManager fileDialogManager;

    public DalamudFileDialogManager(FileDialogManager fileDialogManager)
    {
        this.fileDialogManager = fileDialogManager;
    }

    /// <inheritdoc/>
    public void OpenFolderDialog(string title, Action<bool, string> callback)
    {
        this.fileDialogManager.OpenFolderDialog(title, callback);
    }

    /// <inheritdoc/>
    public void OpenFolderDialog(string title, Action<bool, string> callback, string? startPath, bool isModal = false)
    {
        this.fileDialogManager.OpenFolderDialog(title, callback, startPath, isModal);
    }

    /// <inheritdoc/>
    public void SaveFolderDialog(string title, string defaultFolderName, Action<bool, string> callback)
    {
        this.fileDialogManager.SaveFolderDialog(title, defaultFolderName, callback);
    }

    /// <inheritdoc/>
    public void SaveFolderDialog(string title, string defaultFolderName, Action<bool, string> callback, string? startPath, bool isModal = false)
    {
        this.fileDialogManager.SaveFolderDialog(title, defaultFolderName, callback, startPath, isModal);
    }

    /// <inheritdoc/>
    public void OpenFileDialog(string title, string filters, Action<bool, string> callback)
    {
        this.fileDialogManager.OpenFileDialog(title, filters, callback);
    }

    /// <inheritdoc/>
    public void OpenFileDialog(
        string title,
        string filters,
        Action<bool, List<string>> callback,
        int selectionCountMax,
        string? startPath = null,
        bool isModal = false)
    {
        this.fileDialogManager.OpenFileDialog(title, filters, callback, selectionCountMax, startPath, isModal);
    }

    /// <inheritdoc/>
    public void SaveFileDialog(string title, string filters, string defaultFileName, string defaultExtension, Action<bool, string> callback)
    {
        this.fileDialogManager.SaveFileDialog(title, filters, defaultFileName, defaultExtension, callback);
    }

    /// <inheritdoc/>
    public void SaveFileDialog(
        string title,
        string filters,
        string defaultFileName,
        string defaultExtension,
        Action<bool, string> callback,
        string? startPath,
        bool isModal = false)
    {
        this.fileDialogManager.SaveFileDialog(title, filters, defaultFileName, defaultExtension, callback, startPath, isModal);
    }

    /// <inheritdoc/>
    public void Draw()
    {
        this.fileDialogManager.Draw();
    }

    /// <inheritdoc/>
    public void Reset()
    {
        this.fileDialogManager.Reset();
    }
}
