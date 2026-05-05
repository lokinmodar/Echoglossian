// <copyright file="TranslationEngineSelectionMigrationHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
///     Centralizes compatibility and safety rules for persisted translation
///     engine selections.
/// </summary>
internal static class TranslationEngineSelectionMigrationHelper
{
  internal const string LegacyChatGptCompletionsUrl =
      "https://api.openai.com/v1/chat/completions";

  internal const string NormalizedChatGptBaseUrl =
      "https://api.openai.com/v1";

  /// <summary>
  ///     The first config schema version that explicitly includes the current
  ///     engine ordering contract.
  /// </summary>
  internal const int TranslationEngineSchemaVersion = 15;

  /// <summary>
  ///     Tries to migrate a legacy persisted engine id from the v3.25.x layout
  ///     to the current runtime ordering.
  /// </summary>
  /// <param name="loadedConfigVersion">
  ///     The config version loaded from disk before
  ///     migrations.
  /// </param>
  /// <param name="chosenEngineId">The persisted chosen engine id.</param>
  /// <param name="migratedEngineId">The remapped engine id when migration applies.</param>
  /// <returns>
  ///     <see langword="true" /> when a legacy id was remapped; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  internal static bool TryMigrateLegacyV325Selection(
      int loadedConfigVersion,
      int chosenEngineId,
      out int migratedEngineId)
  {
    migratedEngineId = chosenEngineId;
    if (loadedConfigVersion > 5)
    {
      return false;
    }

    migratedEngineId = chosenEngineId switch
    {
      0 => (int)Echoglossian.TransEngines.Google,
      1 => (int)Echoglossian.TransEngines.Deepl,
      2 => (int)Echoglossian.TransEngines.ChatGPT,
      3 => (int)Echoglossian.TransEngines.Microsoft,
      4 => (int)Echoglossian.TransEngines.YandexCloud,
      5 => (int)Echoglossian.TransEngines.GTranslate,
      6 => (int)Echoglossian.TransEngines.Amazon,
      7 => (int)Echoglossian.TransEngines.Microsoft,
      8 => (int)Echoglossian.TransEngines.Gemini,
      9 => (int)Echoglossian.TransEngines.YandexPublic,
      _ => chosenEngineId,
    };

    return migratedEngineId != chosenEngineId;
  }

  /// <summary>
  ///     Determines whether the current selected engine id is a valid concrete
  ///     runtime engine choice.
  /// </summary>
  /// <param name="engineId">The selected engine id.</param>
  /// <returns>
  ///     <see langword="true" /> when the value is a valid concrete engine id;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  internal static bool IsConcreteEngineId(int engineId)
  {
    return engineId >= (int)Echoglossian.TransEngines.Google &&
           engineId <= (int)Echoglossian.TransEngines.Claude;
  }

  /// <summary>
  ///     Repairs the specific bootstrap failure pattern where a legacy
  ///     v3.25.x selection of YandexPublic was persisted as id 9 and now
  ///     resolves to Amazon in v4.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <returns>
  ///     <see langword="true" /> when the selection was repaired in-place;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  internal static bool TryRepairLikelyLegacyAmazonCollision(Config config)
  {
    if (config.ChosenTransEngine != (int)Echoglossian.TransEngines.Amazon)
    {
      return false;
    }

    if (HasExplicitAmazonConfiguration(config))
    {
      return false;
    }

    config.ChosenTransEngine = (int)Echoglossian.TransEngines.YandexPublic;
    return true;
  }

  /// <summary>
  ///     Normalizes the legacy ChatGPT base URL that pointed directly at the
  ///     completions endpoint instead of the API root.
  /// </summary>
  /// <param name="chatGptBaseUrl">The configured ChatGPT base URL.</param>
  /// <returns>The normalized base URL.</returns>
  internal static string NormalizeLegacyChatGptBaseUrl(string chatGptBaseUrl)
  {
    return string.Equals(
               chatGptBaseUrl,
               LegacyChatGptCompletionsUrl,
               StringComparison.OrdinalIgnoreCase)
        ? NormalizedChatGptBaseUrl
        : chatGptBaseUrl;
  }

  /// <summary>
  ///     Determines whether the user appears to have explicitly configured the
  ///     Amazon translator rather than carrying forward a legacy engine id.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <returns>
  ///     <see langword="true" /> when the config contains explicit Amazon
  ///     translator setup; otherwise, <see langword="false" />.
  /// </returns>
  private static bool HasExplicitAmazonConfiguration(Config config)
  {
    return !string.IsNullOrWhiteSpace(config.AwsAccessKey) ||
           !string.IsNullOrWhiteSpace(config.AwsSecretKey) ||
           !string.IsNullOrWhiteSpace(config.AmazonPrompt);
  }
}
