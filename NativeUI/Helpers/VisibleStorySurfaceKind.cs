// <copyright file="VisibleStorySurfaceKind.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Identifies a player-visible story-facing surface that can expose
///     provenance and explicit retranslation diagnostics.
/// </summary>
public enum VisibleStorySurfaceKind
{
  /// <summary>
  /// The standard Talk addon.
  /// </summary>
  Talk,

  /// <summary>
  /// The BattleTalk addon.
  /// </summary>
  BattleTalk,

  /// <summary>
  /// The TalkSubtitle addon.
  /// </summary>
  TalkSubtitle,

  /// <summary>
  /// The CutSceneSelectString addon.
  /// </summary>
  CutSceneSelectString,

  /// <summary>
  /// The TextGimmickHint addon.
  /// </summary>
  TextGimmickHint,
}
