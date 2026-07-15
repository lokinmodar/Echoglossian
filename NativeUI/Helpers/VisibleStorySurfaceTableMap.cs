// <copyright file="VisibleStorySurfaceTableMap.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Maps visible story surfaces to their DB manager table names.
/// </summary>
public static class VisibleStorySurfaceTableMap
{
  /// <summary>
  /// Resolves the DB manager table name for one visible story surface.
  /// </summary>
  /// <param name="surface">The visible story surface.</param>
  /// <returns>The matching DB manager table name.</returns>
  public static string Resolve(VisibleStorySurfaceKind surface)
  {
    return surface switch
    {
      VisibleStorySurfaceKind.Talk => nameof(TalkMessage),
      VisibleStorySurfaceKind.BattleTalk => nameof(BattleTalkMessage),
      VisibleStorySurfaceKind.TalkSubtitle => nameof(TalkSubtitleMessage),
      VisibleStorySurfaceKind.CutSceneSelectString => nameof(SelectString),
      VisibleStorySurfaceKind.TextGimmickHint => nameof(TextGimmickHintMessage),
      _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, null),
    };
  }
}
