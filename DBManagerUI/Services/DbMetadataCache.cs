// <copyright file="DbMetadataCache.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.DBManagerUI.Services
{
  /// <summary>
  /// Caches scalar properties and PK names for the currently selected entity type.
  /// </summary>
  public class DbMetadataCache
  {
    /// <summary>
    /// Gets the cached scalar properties.
    /// </summary>
    public IReadOnlyList<IProperty>? CurrentScalarProps { get; private set; }

    /// <summary>
    /// Gets the cached primary key property names.
    /// </summary>
    public HashSet<string>? CurrentPkNames { get; private set; }

    /// <summary>
    /// Clears the cache.
    /// </summary>
    public void Clear()
    {
      this.CurrentScalarProps = null;
      this.CurrentPkNames = null;
    }

    /// <summary>
    /// Cache metadata for the given entity type.
    /// </summary>
    /// <param name="entityType">EF runtime entity type.</param>
    public void Cache(IEntityType entityType)
    {
      var scalars = entityType
        .GetProperties()
        .Where(p => p.PropertyInfo != null)
        .ToList();

      this.CurrentScalarProps = scalars;

      var key = entityType.FindPrimaryKey();
      if (key != null)
      {
        this.CurrentPkNames = key.Properties.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
      }
      else
      {
        this.CurrentPkNames = new HashSet<string>(StringComparer.Ordinal);
      }
    }
  }
}
