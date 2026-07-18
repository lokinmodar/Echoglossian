namespace DalaMock.Core.Mocks.DalamudServices;

public class MockFileDialogManager : IFileDialogManager
{
    private string? lastPath;

    public void OpenFolderDialog(string title, Action<bool, string> callback)
    {
        this.OpenFolderDialog(title, callback, null, false);
    }

    public void OpenFolderDialog(string title, Action<bool, string> callback, string? startPath, bool isModal = false)
    {
        var result = Dialog.FolderPicker(startPath ?? this.lastPath);
        this.lastPath = result.Path;
        callback(result.IsOk, result.Path);
    }

    public void SaveFolderDialog(string title, string defaultFolderName, Action<bool, string> callback)
    {
        this.SaveFolderDialog(title, defaultFolderName, callback, null, false);
    }

    public void SaveFolderDialog(string title, string defaultFolderName, Action<bool, string> callback, string? startPath, bool isModal = false)
    {
        var result = Dialog.FolderPicker(startPath ?? this.lastPath);
        this.lastPath = result.Path;
        callback(result.IsOk, result.Path);
    }

    public void OpenFileDialog(string title, string filters, Action<bool, string> callback)
    {
        this.OpenFileDialog(title, filters, (success, files) => callback(success, files.Count > 0 ? files[0] : string.Empty), 1, null, false);
    }

    public void OpenFileDialog(
        string title,
        string filters,
        Action<bool, List<string>> callback,
        int selectionCountMax,
        string? startPath = null,
        bool isModal = false)
    {
        var result = selectionCountMax == 1 ? Dialog.FileOpen(filters, startPath ?? this.lastPath) : Dialog.FileOpenMultiple();
        List<string> resultPaths = new();
        if (result.Paths != null)
        {
            this.lastPath = result.Paths.Count > 0 ? System.IO.Path.GetDirectoryName(result.Paths[0]) : this.lastPath;
            resultPaths = result.Paths.ToList();
        }
        else if (result.Path != null)
        {
            this.lastPath = result.Path;
            resultPaths = [result.Path];
        }

        callback(result.IsOk, resultPaths);
    }

    public void SaveFileDialog(string title, string filters, string defaultFileName, string defaultExtension, Action<bool, string> callback)
    {
        this.SaveFileDialog(title, filters, defaultFileName, defaultExtension, callback, null, false);
    }

    public void SaveFileDialog(
        string title,
        string filters,
        string defaultFileName,
        string defaultExtension,
        Action<bool, string> callback,
        string? startPath,
        bool isModal = false)
    {
        var result = Dialog.FileSave(filters, startPath ?? this.lastPath);
        var path = result.Path;
        if (result.IsOk)
        {
            path = string.IsNullOrEmpty(path) ? defaultFileName : path;
            if (!System.IO.Path.HasExtension(path))
            {
                path += defaultExtension;
            }
        }

        this.lastPath = System.IO.Path.GetDirectoryName(path);
        callback(result.IsOk, path);
    }

    public void Draw()
    {
    }

    public void Reset()
    {
        this.lastPath = null;
    }
}
