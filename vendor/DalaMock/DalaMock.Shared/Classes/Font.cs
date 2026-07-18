namespace DalaMock.Shared.Classes;

using DalaMock.Shared.Interfaces;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

/// <summary>
/// A wrapper around dalamud's default fonts until IUiBuilder provides access to them.
/// </summary>
public class Font : IFont
{
    /// <inheritdoc/>
    public ImFontPtr DefaultFont => UiBuilder.DefaultFont;

    /// <inheritdoc/>
    public ImFontPtr IconFont => UiBuilder.IconFont;

    /// <inheritdoc/>
    public ImFontPtr MonoFont => UiBuilder.MonoFont;

    public ImFontPtr IconFixedWidth => UiBuilder.IconFontFixedWidth;
}
