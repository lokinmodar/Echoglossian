// <copyright file="TextTextureCache.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.ImageGeneration;


/// <summary>
/// Caches generated text textures to avoid redundant rendering.
/// </summary>
public sealed class TextTextureCache : IDisposable
{
  private readonly ConcurrentDictionary<string, ITextureWrap> cache = new();

  /// <summary>
  /// Gets an existing texture from the cache, or creates and stores it using the provided generator.
  /// </summary>
  /// <param name="key">A unique key representing the cached text and parameters.</param>
  /// <param name="generator">A factory function to create the texture if not cached.</param>
  /// <returns>The ITextureWrap for the text.</returns>
  public ITextureWrap GetOrCreate(string key, Func<ITextureWrap> generator)
  {
    return this.cache.GetOrAdd(key, _ => generator());
  }

  /// <summary>
  /// Clears all cached textures and disposes them.
  /// </summary>
  public void Clear()
  {
    foreach (var wrap in this.cache.Values)
    {
      wrap.Dispose();
    }

    this.cache.Clear();
  }

  /// <inheritdoc/>
  public void Dispose()
  {
    this.Clear();
    GC.SuppressFinalize(this);
  }
}
