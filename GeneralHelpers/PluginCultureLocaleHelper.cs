// <copyright file="PluginCultureLocaleHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
///     Normalizes persisted plugin resource culture names to the locale-
///     specific resource files used by the plugin UI.
/// </summary>
internal static class PluginCultureLocaleHelper
{
  private static readonly IReadOnlyDictionary<string, string> CanonicalCultureNames =
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["da"] = "da-DK",
        ["da-DK"] = "da-DK",
        ["de"] = "de-DE",
        ["de-DE"] = "de-DE",
        ["el"] = "el-GR",
        ["el-GR"] = "el-GR",
        ["es"] = "es-ES",
        ["es-ES"] = "es-ES",
        ["eu"] = "eu-ES",
        ["eu-ES"] = "eu-ES",
        ["fr"] = "fr-FR",
        ["fr-FR"] = "fr-FR",
        ["it"] = "it-IT",
        ["it-IT"] = "it-IT",
        ["pt"] = "pt-PT",
        ["pt-PT"] = "pt-PT",
        ["pt-BR"] = "pt-BR",
        ["ru"] = "ru-RU",
        ["ru-RU"] = "ru-RU",
      };

  /// <summary>
  ///     Promotes legacy neutral culture codes to the canonical locale-
  ///     specific culture names used by localized plugin resources.
  /// </summary>
  /// <param name="cultureName">The persisted culture name.</param>
  /// <returns>The canonical culture name to use for plugin resources.</returns>
  internal static string NormalizePersistedCultureName(string? cultureName)
  {
    if (string.IsNullOrWhiteSpace(cultureName))
    {
      return "en";
    }

    return CanonicalCultureNames.TryGetValue(cultureName, out var normalizedCultureName)
        ? normalizedCultureName
        : cultureName;
  }
}
