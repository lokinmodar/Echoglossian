// <copyright file="RenderedTextBlock.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.UIOverlays.TextPresentation;

/// <summary>
/// Represents one measured block of rendered text content.
/// </summary>
internal sealed record RenderedTextBlock
{
  /// <summary>
  /// Initializes a new instance of the <see cref="RenderedTextBlock"/> class.
  /// </summary>
  /// <param name="backendKind">The backend used to render the block.</param>
  /// <param name="measuredSize">The measured block size in pixels.</param>
  /// <param name="texture">
  /// The backing texture when the block was rasterized.
  /// </param>
  /// <param name="lines">
  /// The plain text lines when the block uses the standard ImGui path.
  /// </param>
  /// <param name="rightAligned">
  /// Whether the rendered block should align to the right edge by default.
  /// </param>
  public RenderedTextBlock(
      TextPresentationBackendKind backendKind,
      Vector2 measuredSize,
      IDalamudTextureWrap? texture = null,
      IReadOnlyList<string>? lines = null,
      bool rightAligned = false)
  {
    this.BackendKind = backendKind;
    this.MeasuredSize = measuredSize;
    this.Texture = texture;
    this.Lines = lines ?? Array.Empty<string>();
    this.RightAligned = rightAligned;
  }

  /// <summary>
  /// Gets the backend used to render the block.
  /// </summary>
  public TextPresentationBackendKind BackendKind { get; }

  /// <summary>
  /// Gets the measured block size in pixels.
  /// </summary>
  public Vector2 MeasuredSize { get; }

  /// <summary>
  /// Gets the backing texture when the block was rasterized.
  /// </summary>
  public IDalamudTextureWrap? Texture { get; }

  /// <summary>
  /// Gets the logical lines when the block uses plain ImGui text.
  /// </summary>
  public IReadOnlyList<string> Lines { get; }

  /// <summary>
  /// Gets a value indicating whether the block should align to the right edge.
  /// </summary>
  public bool RightAligned { get; }
}
