// <copyright file="TranslationOverlay.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.UIOverlays.TextPresentation;

namespace Echoglossian.UIOverlays.TranslationOverlay;
internal class TranslationOverlay : IDisposable
{
  public bool IsDisposed { get; private set; }

  public bool Display { get; set; }

  public string CurrentText { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the runtime scale multiplier used when rendering the overlay.
  /// </summary>
  public float RenderScale { get; set; } = 1f;

  /// <summary>
  /// Gets or sets the runtime alpha multiplier used when rendering the overlay.
  /// </summary>
  public float RenderAlpha { get; set; } = 1f;

  /// <summary>
  /// Gets whether the current content is original text shown through swap
  /// presentation.
  /// </summary>
  internal bool DisplaysOriginalSwapText { get; private set; }

  /// <summary>
  /// Gets the owned rich original-text payload for the current swap content.
  /// </summary>
  internal RichOriginalTextPresentation? RichOriginalTextPresentation { get; private set; }

  public volatile int CurrentTextId;
  public Vector2 Dimensions = Vector2.Zero;
  public Vector2 ImGuiSize = Vector2.Zero;
  public Vector2 Position = Vector2.Zero;

  public SemaphoreSlim Semaphore { get; }

  public string CurrentName { get; set; } = string.Empty;

  public volatile int CurrentNameId;

  public SemaphoreSlim NameSemaphore { get; }

  public string OriginalName { get; set; } = string.Empty;

  public TranslationOverlay()
  {
    this.Semaphore = new SemaphoreSlim(1, 1);
    this.NameSemaphore = new SemaphoreSlim(1, 1);
  }

  /// <summary>
  /// Updates the optional rich original-text state while the caller owns
  /// <see cref="Semaphore" />.
  /// </summary>
  /// <param name="displaysOriginalSwapText">
  /// Whether the overlay displays original content through swap presentation.
  /// </param>
  /// <param name="presentation">The copied original SeString payload, if available.</param>
  internal void UpdateContentPresentation(
      bool displaysOriginalSwapText,
      RichOriginalTextPresentation? presentation)
  {
    this.DisplaysOriginalSwapText = displaysOriginalSwapText;
    this.RichOriginalTextPresentation = displaysOriginalSwapText
        ? presentation
        : null;
  }

  /// <summary>
  /// Clears the retained original swap presentation while the caller owns
  /// <see cref="Semaphore" />.
  /// </summary>
  internal void ClearContentPresentation()
  {
    this.DisplaysOriginalSwapText = false;
    this.RichOriginalTextPresentation = null;
  }

  /// <summary>
  /// Updates the runtime scale and alpha modifiers for the overlay presentation.
  /// </summary>
  /// <param name="renderScale">The runtime scale multiplier.</param>
  /// <param name="renderAlpha">The runtime alpha multiplier.</param>
  internal void UpdateRuntimePresentation(
      float renderScale,
      float renderAlpha)
  {
    this.RenderScale = Math.Clamp(renderScale, 0.25f, 3f);
    this.RenderAlpha = Math.Clamp(renderAlpha, 0f, 1f);
  }

  /// <summary>
  /// Clears the runtime scale and alpha modifiers for the overlay presentation.
  /// </summary>
  internal void ClearRuntimePresentation()
  {
    this.RenderScale = 1f;
    this.RenderAlpha = 1f;
  }

  public void Dispose()
  {
    this.IsDisposed = true;
    this.Semaphore.Dispose();
    this.NameSemaphore.Dispose();
  }
}
