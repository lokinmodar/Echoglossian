// <copyright file="CachedTextureEntry.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.ImageGeneration;

/// <summary>
/// Represents a cached texture entry with last access time and memory info.
/// </summary>
internal sealed class CachedTextureEntry
{
  /// <summary>
  /// Initializes a new instance of the <see cref="CachedTextureEntry"/> class.
  /// </summary>
  /// <param name="texture">The cached texture.</param>
  public CachedTextureEntry(IDalamudTextureWrap texture)
  {
    this.Texture = texture;
    this.LastAccessed = DateTime.UtcNow;
  }

  /// <summary>
  /// Gets the wrapped texture.
  /// </summary>
  public IDalamudTextureWrap Texture { get; }

  /// <summary>
  /// Gets or sets the last time this texture was accessed.
  /// </summary>
  public DateTime LastAccessed { get; set; }

  /// <summary>
  /// Estimates the memory usage of the texture in bytes.
  /// </summary>
  /// <returns>Estimated memory footprint in bytes (width × height × 4).</returns>
  public long EstimateMemoryBytes()
  {
    return this.Texture.Width * this.Texture.Height * 4L;
  }
}
