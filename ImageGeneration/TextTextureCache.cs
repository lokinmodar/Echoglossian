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
  private readonly long softByteBudget;
  private readonly long hardByteBudget;
  private long estimatedMemoryBytes;

  /// <summary>
  /// Initializes a new instance of the <see cref="TextTextureCache"/> class.
  /// </summary>
  /// <param name="maxCapacity">Maximum number of textures allowed.</param>
  /// <param name="inactivityTimeoutSeconds">Seconds after which inactive entries are evicted.</param>
  /// <param name="softByteBudget">
  /// Soft in-memory budget that triggers eviction of older entries.
  /// </param>
  /// <param name="hardByteBudget">
  /// Hard in-memory budget for any single cached texture.
  /// </param>
  public TextTextureCache(
      int maxCapacity = 128,
      int inactivityTimeoutSeconds = 60,
      long softByteBudget = long.MaxValue,
      long hardByteBudget = long.MaxValue)
  {
    this.maxCapacity = maxCapacity;
    this.inactivityThreshold = TimeSpan.FromSeconds(inactivityTimeoutSeconds);
    this.softByteBudget = softByteBudget;
    this.hardByteBudget = hardByteBudget;
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
      var newEntryBytes = newEntry.EstimateMemoryBytes();

      if (newEntryBytes > this.hardByteBudget)
      {
        texture.Dispose();
        throw new InvalidOperationException(
            $"Texture entry '{key}' exceeds the hard cache budget.");
      }

      if (this.cache.Count >= this.maxCapacity)
      {
        this.EvictLeastRecentlyUsed();
      }

      while (this.cache.Count > 0 &&
             this.estimatedMemoryBytes + newEntryBytes > this.softByteBudget)
      {
        this.EvictLeastRecentlyUsed();
      }

      this.cache[key] = newEntry;
      this.accessOrder.AddLast(key);
      this.estimatedMemoryBytes += newEntryBytes;

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
      this.estimatedMemoryBytes -= entry.EstimateMemoryBytes();
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
        this.estimatedMemoryBytes -= entry.EstimateMemoryBytes();
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
      this.estimatedMemoryBytes = 0L;
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
      return (this.cache.Count, this.estimatedMemoryBytes);
    }
  }
}
