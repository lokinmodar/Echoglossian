// <copyright file="DbTableNameMatcher.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.DBManagerUI.Services
{
  /// <summary>
  /// Resolves DB manager table names from caller-supplied requests.
  /// </summary>
  public static class DbTableNameMatcher
  {
    /// <summary>
    /// Finds the exact matching table name from the available DB manager tables.
    /// </summary>
    /// <param name="tables">The available table names.</param>
    /// <param name="requestedTable">The requested table name.</param>
    /// <returns>
    /// The matched table name, or <see langword="null"/> when no exact match exists.
    /// </returns>
    public static string? Match(
      IReadOnlyList<string> tables,
      string requestedTable)
    {
      if (tables.Count == 0 || string.IsNullOrWhiteSpace(requestedTable))
      {
        return null;
      }

      return tables.FirstOrDefault(
        table => string.Equals(
          table,
          requestedTable,
          StringComparison.Ordinal));
    }
  }
}
