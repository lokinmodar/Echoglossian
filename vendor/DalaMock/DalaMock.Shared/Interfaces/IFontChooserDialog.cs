namespace DalaMock.Shared.Interfaces;

using System;
using System.Numerics;
using System.Threading.Tasks;

using Dalamud.Interface.FontIdentifier;

/// <summary>
/// A dialog that lets the user choose a font (family, style and size) from the game fonts,
/// Dalamud-provided fonts and the system fonts. Abstracts Dalamud's
/// <c>SingleFontChooserDialog</c> so it can be provided either by a real Dalamud implementation
/// or a cross-platform mock implementation.
/// </summary>
public interface IFontChooserDialog : IDisposable
{
    /// <summary>
    /// Called when the selected font spec has changed.
    /// </summary>
    event Action<SingleFontSpec>? SelectedFontSpecChanged;

    /// <summary>
    /// Gets the task that resolves upon choosing a font, or is cancelled when the dialog is dismissed.
    /// </summary>
    Task<SingleFontSpec> ResultTask { get; }

    /// <summary>
    /// Gets or sets the title of this font chooser dialog popup.
    /// </summary>
    string Title { get; set; }

    /// <summary>
    /// Gets or sets the preview text shown in the dialog.
    /// </summary>
    string PreviewText { get; set; }

    /// <summary>
    /// Gets or sets the selected family and font.
    /// </summary>
    SingleFontSpec SelectedFont { get; set; }

    /// <summary>
    /// Gets or sets a predicate that, when it returns <c>true</c> for a font family, excludes that
    /// family from being selectable.
    /// </summary>
    Predicate<IFontFamilyId>? FontFamilyExcludeFilter { get; set; }

    /// <summary>
    /// Draws this dialog. Call once per frame while the dialog is open.
    /// </summary>
    void Draw();

    /// <summary>
    /// Cancels this dialog, completing <see cref="ResultTask"/> as cancelled.
    /// </summary>
    void Cancel();

    /// <summary>
    /// Positions and sizes the popup at the center of the window currently being drawn.
    /// </summary>
    /// <param name="preferredPopupSize">The preferred popup size.</param>
    void SetPopupPositionAndSizeToCurrentWindowCenter(Vector2 preferredPopupSize);

    /// <summary>
    /// Positions and sizes the popup at the center of the window currently being drawn, using a
    /// default popup size.
    /// </summary>
    void SetPopupPositionAndSizeToCurrentWindowCenter();
}