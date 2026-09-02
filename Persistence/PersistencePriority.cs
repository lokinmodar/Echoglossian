// <copyright file="PersistencePriority.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Persistence;

/// <summary>
///     Specifies the bounded persistence lane that accepts work.
/// </summary>
internal enum PersistencePriority
{
  /// <summary>
  ///     Specifies work required by an active interactive surface.
  /// </summary>
  Interactive,

  /// <summary>
  ///     Specifies opportunistic background work.
  /// </summary>
  Background,
}
