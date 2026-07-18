namespace DalaMock.Shared.Interfaces;

using System;
using System.Collections.Generic;

public interface IFileDialogManager
{
    /// <summary>
    /// Create a dialog which selects an already existing folder.
    /// </summary>
    /// <param name="title">The header title of the dialog.</param>
    /// <param name="callback">The action to execute when the dialog is finished.</param>
    void OpenFolderDialog(string title, Action<bool, string> callback);

    /// <summary>
    /// Create a dialog which selects an already existing folder.
    /// </summary>
    /// <param name="title">The header title of the dialog.</param>
    /// <param name="callback">The action to execute when the dialog is finished.</param>
    /// <param name="startPath">The directory which the dialog should start inside of. The last path this manager was in is used if this is null.</param>
    /// <param name="isModal">Whether the dialog should be a modal popup.</param>
    void OpenFolderDialog(string title, Action<bool, string> callback, string? startPath, bool isModal = false);

    /// <summary>
    /// Create a dialog which selects an already existing folder or new folder.
    /// </summary>
    /// <param name="title">The header title of the dialog.</param>
    /// <param name="defaultFolderName">The default name to use when creating a new folder.</param>
    /// <param name="callback">The action to execute when the dialog is finished.</param>
    void SaveFolderDialog(string title, string defaultFolderName, Action<bool, string> callback);

    /// <summary>
    /// Create a dialog which selects an already existing folder or new folder.
    /// </summary>
    /// <param name="title">The header title of the dialog.</param>
    /// <param name="defaultFolderName">The default name to use when creating a new folder.</param>
    /// <param name="callback">The action to execute when the dialog is finished.</param>
    /// <param name="startPath">The directory which the dialog should start inside of. The last path this manager was in is used if this is null.</param>
    /// <param name="isModal">Whether the dialog should be a modal popup.</param>
    void SaveFolderDialog(string title, string defaultFolderName, Action<bool, string> callback, string? startPath, bool isModal = false);

    /// <summary>
    /// Create a dialog which selects a single, already existing file.
    /// </summary>
    /// <param name="title">The header title of the dialog.</param>
    /// <param name="filters">Which files to show in the dialog.</param>
    /// <param name="callback">The action to execute when the dialog is finished.</param>
    void OpenFileDialog(string title, string filters, Action<bool, string> callback);

    /// <summary>
    /// Create a dialog which selects already existing files.
    /// </summary>
    /// <param name="title">The header title of the dialog.</param>
    /// <param name="filters">Which files to show in the dialog.</param>
    /// <param name="callback">The action to execute when the dialog is finished.</param>
    /// <param name="selectionCountMax">The maximum amount of files or directories which can be selected. Set to 0 for an infinite number.</param>
    /// <param name="startPath">The directory which the dialog should start inside of. The last path this manager was in is used if this is null.</param>
    /// <param name="isModal">Whether the dialog should be a modal popup.</param>
    void OpenFileDialog(
        string title,
        string filters,
        Action<bool, List<string>> callback,
        int selectionCountMax,
        string? startPath = null,
        bool isModal = false);

    /// <summary>
    /// Create a dialog which selects an already existing folder or new file.
    /// </summary>
    /// <param name="title">The header title of the dialog.</param>
    /// <param name="filters">Which files to show in the dialog.</param>
    /// <param name="defaultFileName">The default name to use when creating a new file.</param>
    /// <param name="defaultExtension">The extension to use when creating a new file.</param>
    /// <param name="callback">The action to execute when the dialog is finished.</param>
    void SaveFileDialog(
        string title,
        string filters,
        string defaultFileName,
        string defaultExtension,
        Action<bool, string> callback);

    /// <summary>
    /// Create a dialog which selects an already existing folder or new file.
    /// </summary>
    /// <param name="title">The header title of the dialog.</param>
    /// <param name="filters">Which files to show in the dialog.</param>
    /// <param name="defaultFileName">The default name to use when creating a new file.</param>
    /// <param name="defaultExtension">The extension to use when creating a new file.</param>
    /// <param name="callback">The action to execute when the dialog is finished.</param>
    /// <param name="startPath">The directory which the dialog should start inside of. The last path this manager was in is used if this is null.</param>
    /// <param name="isModal">Whether the dialog should be a modal popup.</param>
    void SaveFileDialog(
        string title,
        string filters,
        string defaultFileName,
        string defaultExtension,
        Action<bool, string> callback,
        string? startPath,
        bool isModal = false);

    /// <summary>
    /// Draws the current dialog, if any, and executes the callback if it is finished.
    /// </summary>
    void Draw();

    /// <summary>
    /// Removes the current dialog, if any.
    /// </summary>
    void Reset();
}
