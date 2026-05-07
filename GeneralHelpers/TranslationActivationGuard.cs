// <copyright file="TranslationActivationGuard.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
/// Determines whether translation activation must stay blocked for the current
/// language and engine configuration.
/// </summary>
internal static class TranslationActivationGuard
{
  /// <summary>
  /// The reason why translation activation is currently blocked.
  /// </summary>
  internal enum BlockReason
  {
    None,
    UnsupportedLanguage,
    MissingRequiredAssets,
    EngineConfigurationIncomplete,
  }

  /// <summary>
  /// Resolves the current translation activation block reason for the provided
  /// configuration and selected language.
  /// </summary>
  /// <param name="config">The plugin configuration to evaluate.</param>
  /// <param name="selectedLanguage">The currently selected target language.</param>
  /// <returns>The blocking reason, or <see cref="BlockReason.None"/>.</returns>
  public static BlockReason GetBlockReason(
      Config config,
      LanguageInfo selectedLanguage)
  {
    if (config.UnsupportedLanguage)
    {
      return BlockReason.UnsupportedLanguage;
    }

    if (AssetsManager.HasMissingRequiredAssets(selectedLanguage))
    {
      return BlockReason.MissingRequiredAssets;
    }

    if (!TranslationEngineConfigurationHelper.IsConfigured(config))
    {
      return BlockReason.EngineConfigurationIncomplete;
    }

    return BlockReason.None;
  }
}
