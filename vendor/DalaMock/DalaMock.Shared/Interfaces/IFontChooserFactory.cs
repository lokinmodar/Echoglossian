namespace DalaMock.Shared.Interfaces;

/// <summary>
/// Creates <see cref="IFontChooserDialog"/> instances with their dependencies (the active
/// <c>UiBuilder</c>/font atlas factory, configuration and system-font source) injected
/// automatically. Provided either by a real Dalamud implementation or a mock implementation.
/// </summary>
public interface IFontChooserFactory
{
    /// <summary>
    /// Creates a new font chooser dialog. The caller is responsible for calling
    /// <see cref="IFontChooserDialog.Draw"/> each frame and disposing the dialog when done.
    /// </summary>
    /// <returns>The new dialog.</returns>
    IFontChooserDialog Create();

    /// <summary>
    /// Creates a new font chooser dialog that automatically draws itself on the UI draw loop and
    /// disposes itself once <see cref="IFontChooserDialog.ResultTask"/> completes.
    /// </summary>
    /// <returns>The new dialog.</returns>
    IFontChooserDialog CreateAuto();
}
