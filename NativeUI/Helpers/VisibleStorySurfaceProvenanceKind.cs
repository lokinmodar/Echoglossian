// <copyright file="VisibleStorySurfaceProvenanceKind.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Identifies where the currently visible story-surface translation came
///     from for debugger provenance display.
/// </summary>
public enum VisibleStorySurfaceProvenanceKind
{
  /// <summary>
  /// The visible translation came from an existing DB row.
  /// </summary>
  DbReuse,

  /// <summary>
  /// The visible translation came from a fresh live translation.
  /// </summary>
  FreshLiveTranslation,

  /// <summary>
  /// The visible translation came from a fresh live translation that used
  /// runtime-only dialogue context and therefore should not be persisted as a
  /// canonical DB row.
  /// </summary>
  FreshLiveTranslationRuntimeOnlyDialogueContext,
}
