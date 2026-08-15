// <copyright file="PluginCultureLocaleHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Globalization;

using global::Echoglossian.Properties;

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
        ["ca"] = "ca-ES",
        ["ca-ES"] = "ca-ES",
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
        ["nl"] = "nl-NL",
        ["nl-NL"] = "nl-NL",
        ["pt"] = "pt-PT",
        ["pt-PT"] = "pt-PT",
        ["pt-BR"] = "pt-BR",
        ["ru"] = "ru-RU",
        ["ru-RU"] = "ru-RU",
      };

  /// <summary>
  ///     Normalizes and applies the persisted plugin culture to the strongly
  ///     typed resource accessor used throughout the plugin UI.
  /// </summary>
  /// <param name="cultureName">The persisted culture name.</param>
  /// <returns>The normalized culture applied to plugin resources.</returns>
  internal static CultureInfo ApplyPersistedCultureName(string? cultureName)
  {
    var cultureInfo = new CultureInfo(NormalizePersistedCultureName(cultureName));
    Resources.Culture = cultureInfo;
    return cultureInfo;
  }

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
