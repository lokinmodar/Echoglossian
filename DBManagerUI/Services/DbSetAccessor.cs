// <copyright file="DbSetAccessor.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.DBManagerUI.Services
{
  /// <summary>
  /// Provides reflection-based access to DbContext.Set&lt;TEntity&gt;().
  /// </summary>
  public class DbSetAccessor
  {
    /// <summary>
    /// Returns a non-generic DbSet via reflection for the given entity CLR type.
    /// </summary>
    /// <param name="db">DbContext instance.</param>
    /// <param name="entityClrType">Entity CLR type.</param>
    /// <returns>DbSet instance as object.</returns>
    public object GetDbSet(DbContext db, Type entityClrType)
    {
      var setMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes);
      var generic = setMethod!.MakeGenericMethod(entityClrType);
      var set = generic.Invoke(db, null);
      return set!;
    }
  }
}
