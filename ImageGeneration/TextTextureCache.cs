// <copyright file="TextTextureCache.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.ImageGeneration;

/// <summary>
/// LRU cache for storing generated text textures with timeout and memory tracking.
/// </summary>
public sealed class TextTextureCache : IDisposable
{
  private readonly ConcurrentDictionary<string, CachedTextureEntry> cache = new();
  private readonly LinkedList<string> accessOrder = new();

  private readonly object syncLock = new();
  private readonly int maxCapacity;
  private readonly TimeSpan inactivityThreshold;

  /// <summary>
  /// Initializes a new instance of the <see cref="TextTextureCache"/> class.
  /// </summary>
  /// <param name="maxCapacity">Maximum number of textures allowed.</param>
  /// <param name="inactivityTimeoutSeconds">Seconds after which inactive entries are evicted.</param>
  public TextTextureCache(int maxCapacity = 128, int inactivityTimeoutSeconds = 60)
  {
    this.maxCapacity = maxCapacity;
    this.inactivityThreshold = TimeSpan.FromSeconds(inactivityTimeoutSeconds);
  }

  /// <summary>
  /// Gets or creates a texture by key using the provided generator function.
  /// </summary>
  /// <param name="key"></param>
  /// <param name="generator"></param>
  /// <returns></returns>
  public IDalamudTextureWrap GetOrCreate(string key, Func<IDalamudTextureWrap> generator)
  {
    lock (this.syncLock)
    {
      this.PruneStaleEntries();

      if (this.cache.TryGetValue(key, out var entry))
      {
        entry.LastAccessed = DateTime.UtcNow;

        this.accessOrder.Remove(key);
        this.accessOrder.AddLast(key);

        return entry.Texture;
      }

      IDalamudTextureWrap texture = generator();
      var newEntry = new CachedTextureEntry(texture);

      if (this.cache.Count >= this.maxCapacity)
      {
        this.EvictLeastRecentlyUsed();
      }

      this.cache[key] = newEntry;
      this.accessOrder.AddLast(key);

      return texture;
    }
  }

  /// <summary>
  /// Evicts the least recently used texture from the cache if it exceeds capacity.
  /// </summary>
  private void EvictLeastRecentlyUsed()
  {
    if (this.accessOrder.First is not { } oldestKey)
    {
      return;
    }

    if (this.cache.TryRemove(oldestKey.Value, out var entry))
    {
      entry.Texture.Dispose();
    }

    this.accessOrder.RemoveFirst();
  }

  /// <summary>
  /// Prunes stale entries that have not been accessed within the inactivity threshold.
  /// </summary>
  private void PruneStaleEntries()
  {
    var now = DateTime.UtcNow;
    var toRemove = this.cache
      .Where(pair => now - pair.Value.LastAccessed > this.inactivityThreshold)
      .Select(pair => pair.Key)
      .ToList();

    foreach (string key in toRemove)
    {
      if (this.cache.TryRemove(key, out var entry))
      {
        entry.Texture.Dispose();
      }

      this.accessOrder.Remove(key);
    }
  }

  /// <summary>
  /// Clears the cache completely.
  /// </summary>
  public void Clear()
  {
    lock (this.syncLock)
    {
      foreach (var entry in this.cache.Values)
      {
        entry.Texture.Dispose();
      }

      this.cache.Clear();
      this.accessOrder.Clear();
    }
  }

  /// <inheritdoc/>
  public void Dispose()
  {
    this.Clear();
    GC.SuppressFinalize(this);
  }

  /// <summary>
  /// Gets debug statistics about the cache.
  /// </summary>
  /// <returns></returns>
  public (int Count, long EstimatedMemoryBytes) GetDebugStats()
  {
    lock (this.syncLock)
    {
      int count = this.cache.Count;
      long bytes = this.cache.Values.Sum(e => e.EstimateMemoryBytes());
      return (count, bytes);
    }
  }
}
