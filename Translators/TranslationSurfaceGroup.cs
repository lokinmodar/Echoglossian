// <copyright file="TranslationSurfaceGroup.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators;

/// <summary>
///     Classifies one translation request into a coarse surface family so the
///     shared translation service can apply the configured routing policy
///     without creating parallel translation pipelines.
/// </summary>
public enum TranslationSurfaceGroup
{
  /// <summary>
  ///     Uses the global translation-engine path without a surface-specific
  ///     override.
  /// </summary>
  Default = 0,

  /// <summary>
  ///     Represents live dialogue-family surfaces such as Talk and
  ///     BattleTalk.
  /// </summary>
  Dialogue = 1,
}
