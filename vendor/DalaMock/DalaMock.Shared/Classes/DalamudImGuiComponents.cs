namespace DalaMock.Shared.Classes;

using System.Numerics;

using DalaMock.Shared.Interfaces;
using Dalamud.Interface;
using Dalamud.Interface.Components;

public class DalamudImGuiComponents : IImGuiComponents
{
    /// <inheritdoc/>
    public bool IconButton(FontAwesomeIcon icon)
    {
        return ImGuiComponents.IconButton(icon);
    }

    /// <inheritdoc/>
    public bool IconButton(FontAwesomeIcon icon, Vector2 size)
    {
        return ImGuiComponents.IconButton(icon, size);
    }

    /// <inheritdoc/>
    public bool IconButton(
        FontAwesomeIcon icon,
        Vector4? defaultColor,
        Vector4? activeColor = null,
        Vector4? hoveredColor = null,
        Vector2? size = null)
    {
        return ImGuiComponents.IconButton(icon, defaultColor, activeColor, hoveredColor, size);
    }

    /// <inheritdoc/>
    public bool IconButton(int id, FontAwesomeIcon icon)
    {
        return ImGuiComponents.IconButton(id, icon);
    }

    /// <inheritdoc/>
    public bool IconButton(int id, FontAwesomeIcon icon, Vector2 size)
    {
        return ImGuiComponents.IconButton(id, icon, size);
    }

    /// <inheritdoc/>
    public bool IconButton(
        int id,
        FontAwesomeIcon icon,
        Vector4? defaultColor,
        Vector4? activeColor = null,
        Vector4? hoveredColor = null,
        Vector2? size = null)
    {
        return ImGuiComponents.IconButton(id, icon, defaultColor, activeColor, hoveredColor, size);
    }

    /// <inheritdoc/>
    public bool IconButton(string id, FontAwesomeIcon icon)
    {
        return ImGuiComponents.IconButton(id, icon);
    }

    /// <inheritdoc/>
    public bool IconButton(string id, FontAwesomeIcon icon, Vector2 size)
    {
        return ImGuiComponents.IconButton(id, icon, size);
    }

    /// <inheritdoc/>
    public bool IconButton(
        string id,
        FontAwesomeIcon icon,
        Vector4? defaultColor,
        Vector4? activeColor = null,
        Vector4? hoveredColor = null,
        Vector2? size = null)
    {
        return  ImGuiComponents.IconButton(id, icon, defaultColor, activeColor, hoveredColor, size);
    }

    /// <inheritdoc/>
    public bool IconButton(string iconText)
    {
        return ImGuiComponents.IconButton(iconText);
    }

    /// <inheritdoc/>
    public bool IconButton(string iconText, Vector2 size)
    {
        return ImGuiComponents.IconButton(iconText, size);
    }

    /// <inheritdoc/>
    public bool IconButton(
        string iconText,
        Vector4? defaultColor,
        Vector4? activeColor = null,
        Vector4? hoveredColor = null,
        Vector2? size = null)
    {
        return ImGuiComponents.IconButton(iconText, defaultColor, activeColor, hoveredColor, size);
    }

    /// <inheritdoc/>
    public bool IconButtonWithText(FontAwesomeIcon icon, string text, Vector2 size)
    {
        return ImGuiComponents.IconButtonWithText(icon, text, size);
    }

    /// <inheritdoc/>
    public bool IconButtonWithText(
        FontAwesomeIcon icon,
        string text,
        Vector4? defaultColor = null,
        Vector4? activeColor = null,
        Vector4? hoveredColor = null,
        Vector2? size = null)
    {
        return ImGuiComponents.IconButtonWithText(icon, text, defaultColor, activeColor, hoveredColor, size);
    }

    /// <inheritdoc/>
    public float GetIconButtonWithTextWidth(FontAwesomeIcon icon, string text)
    {
        return ImGuiComponents.GetIconButtonWithTextWidth(icon, text);
    }

    public void HelpMarker(string helpText, FontAwesomeIcon icon, Vector4? color = null)
    {
        ImGuiComponents.HelpMarker(helpText, icon, color);
    }
}
