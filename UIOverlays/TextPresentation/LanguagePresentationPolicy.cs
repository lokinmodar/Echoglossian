// <copyright file="LanguagePresentationPolicy.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.UIOverlays.TextPresentation;

/// <summary>
/// Centralizes per-language presentation and activation capabilities for
/// translated ImGui surfaces.
/// </summary>
internal static class LanguagePresentationPolicy
{
  private static readonly HashSet<int> RtlLanguageIds =
  [
    2,
    42,
    78,
    82,
    106,
    108,
    116,
    129,
    131,
    141,
    144,
    149,
    150,
    153,
    161,
    169,
  ];

  private static readonly HashSet<int> TexturePresentationLanguageIds =
  [
    2,
    6,
    40,
    42,
    57,
    78,
    82,
    106,
    108,
    116,
    129,
    131,
    141,
    144,
    149,
    150,
    153,
    161,
    169,
  ];

  private static readonly HashSet<int> OverlayOnlyLanguageIds =
  [
    4,
    8,
    9,
    10,
    12,
    14,
    15,
    16,
    18,
    19,
    21,
    22,
    29,
    35,
    37,
    38,
    41,
    43,
    45,
    46,
    51,
    52,
    53,
    55,
    56,
    58,
    64,
    67,
    69,
    70,
    71,
    72,
    76,
    77,
    85,
    86,
    89,
    90,
    92,
    99,
    100,
    101,
    102,
    103,
    107,
  ];

  // Legacy hard blocks are intentionally empty. Engine support and presentation
  // capability are now modeled separately, and activation is driven by the
  // current engine matrix plus overlay/texture presentation rules.
  private static readonly HashSet<int> UnsupportedLanguageIds = [];

  /// <summary>
  /// Gets whether the provided language uses the approved RTL presentation
  /// path.
  /// </summary>
  /// <param name="languageId">The selected language identifier.</param>
  /// <returns>
  /// <see langword="true"/> when the language should use the RTL path;
  /// otherwise, <see langword="false"/>.
  /// </returns>
  public static bool IsRtlLanguage(int languageId)
  {
    return RtlLanguageIds.Contains(languageId);
  }

  /// <summary>
  /// Gets whether translated plugin-owned text for the provided language
  /// should align to the right edge by default.
  /// </summary>
  /// <param name="languageId">The selected language identifier.</param>
  /// <returns>
  /// <see langword="true"/> when the language uses RTL alignment semantics;
  /// otherwise, <see langword="false"/>.
  /// </returns>
  public static bool ShouldRightAlign(int languageId)
  {
    return IsRtlLanguage(languageId);
  }

  /// <summary>
  /// Gets whether the language should be rendered through the texture-backed
  /// complex-text presentation path for plugin-owned ImGui surfaces.
  /// </summary>
  /// <param name="languageId">The selected language identifier.</param>
  /// <returns>
  /// <see langword="true"/> when the language should avoid plain ImGui text;
  /// otherwise, <see langword="false"/>.
  /// </returns>
  public static bool UsesTexturePresentation(int languageId)
  {
    return TexturePresentationLanguageIds.Contains(languageId);
  }

  /// <summary>
  /// Gets whether the language must remain overlay-only in the plugin.
  /// </summary>
  /// <param name="languageId">The selected language identifier.</param>
  /// <returns>
  /// <see langword="true"/> when native mutation must stay disabled;
  /// otherwise, <see langword="false"/>.
  /// </returns>
  public static bool RequiresOverlayOnly(int languageId)
  {
    return UsesTexturePresentation(languageId) ||
           OverlayOnlyLanguageIds.Contains(languageId);
  }

  /// <summary>
  /// Gets whether the language remains unsupported by the current plugin
  /// runtime.
  /// </summary>
  /// <param name="languageId">The selected language identifier.</param>
  /// <returns>
  /// <see langword="true"/> when activation must stay blocked; otherwise,
  /// <see langword="false"/>.
  /// </returns>
  public static bool IsUnsupportedLanguage(int languageId)
  {
    return UnsupportedLanguageIds.Contains(languageId);
  }

  /// <summary>
  /// Applies the current language presentation flags to the live config.
  /// </summary>
  /// <param name="config">The configuration to update.</param>
  public static void ApplyLanguageFlags(Config config)
  {
    config.UnsupportedLanguage = IsUnsupportedLanguage(config.Lang);
    config.OverlayOnlyLanguage = RequiresOverlayOnly(config.Lang);
  }
}
