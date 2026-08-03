// <copyright file="RichOriginalTextPresentation.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.UIOverlays.TextPresentation;

/// <summary>
/// Holds the plain fallback and an owned copy of an original game
/// <c>SeString</c> payload for plugin-owned presentation.
/// </summary>
internal sealed class RichOriginalTextPresentation
{
  private readonly byte[] seStringPayloadBytes;

  /// <summary>
  /// Initializes a new instance of the <see cref="RichOriginalTextPresentation" /> class.
  /// </summary>
  /// <param name="plainText">The plain-text fallback displayed when rich rendering is unavailable.</param>
  /// <param name="seStringPayload">The original SeString payload to copy and own.</param>
  public RichOriginalTextPresentation(string plainText, ReadOnlySpan<byte> seStringPayload = default)
  {
    ArgumentNullException.ThrowIfNull(plainText);

    this.PlainText = plainText;
    this.seStringPayloadBytes = seStringPayload.ToArray();
  }

  /// <summary>
  /// Gets the plain-text fallback for this presentation.
  /// </summary>
  public string PlainText { get; }

  /// <summary>
  /// Gets a value indicating whether an owned SeString payload is available.
  /// </summary>
  public bool HasSeStringPayload => this.seStringPayloadBytes.Length > 0;

  /// <summary>
  /// Gets the owned SeString payload as read-only memory when it is available.
  /// </summary>
  /// <param name="payload">The copied SeString payload.</param>
  /// <returns><see langword="true" /> when a payload is available; otherwise, <see langword="false" />.</returns>
  public bool TryGetSeStringPayload(out ReadOnlyMemory<byte> payload)
  {
    payload = this.seStringPayloadBytes;
    return this.HasSeStringPayload;
  }
}
