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
  ///     Tries to resolve a persisted engine key to one concrete runtime engine
  ///     id.
  /// </summary>
  /// <param name="engineKey">The persisted engine key.</param>
  /// <param name="engineId">The resolved concrete engine id when successful.</param>
  /// <returns>
  ///     <see langword="true" /> when the key resolved to one concrete engine;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  internal static bool TryResolveEngineKey(
      string? engineKey,
      out int engineId)
  {
    engineId = (int)Echoglossian.TransEngines.Google;
    if (string.IsNullOrWhiteSpace(engineKey))
    {
      return false;
    }

    if (Enum.TryParse<Echoglossian.TransEngines>(
            engineKey.Trim(),
            true,
            out var parsedEngine) &&
        parsedEngine != Echoglossian.TransEngines.All)
    {
      engineId = (int)parsedEngine;
      return IsConcreteEngineId(engineId);
    }

    var normalizedKey = engineKey.Trim();
    engineId = normalizedKey.ToUpperInvariant() switch
    {
      "OPENAI" => (int)Echoglossian.TransEngines.ChatGPT,
      "AMAZONTRANSLATE" => (int)Echoglossian.TransEngines.Amazon,
      _ => (int)Echoglossian.TransEngines.Google,
    };

    return normalizedKey.Equals(
               "OpenAI",
               StringComparison.OrdinalIgnoreCase) ||
           normalizedKey.Equals(
               "AmazonTranslate",
               StringComparison.OrdinalIgnoreCase);
  }

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
  ///     Normalizes a selected engine id against the currently supported engine
  ///     ids for the target language.
  /// </summary>
  /// <param name="selectedEngineId">The currently selected engine id.</param>
  /// <param name="supportedEngineIds">
  ///     The supported engine ids for the active
  ///     target language.
  /// </param>
  /// <returns>
  ///     The selected engine id when it is supported; otherwise, the first
  ///     supported concrete engine id, or Google when the language exposes no
  ///     concrete engine list.
  /// </returns>
  internal static int ResolveSupportedEngineSelection(
      int selectedEngineId,
      IReadOnlyCollection<int>? supportedEngineIds)
  {
    if (supportedEngineIds == null || supportedEngineIds.Count == 0)
    {
      return (int)Echoglossian.TransEngines.Google;
    }

    if (IsConcreteEngineId(selectedEngineId) &&
        supportedEngineIds.Contains(selectedEngineId))
    {
      return selectedEngineId;
    }

    foreach (var supportedEngineId in supportedEngineIds)
    {
      if (IsConcreteEngineId(supportedEngineId))
      {
        return supportedEngineId;
      }
    }

    return (int)Echoglossian.TransEngines.Google;
  }

  /// <summary>
  ///     Normalizes and synchronizes the persisted translation engine
  ///     selection, preferring the persisted engine key when available and
  ///     keeping the numeric and string forms aligned.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <param name="loadedConfigVersion">
  ///     The config version loaded from disk before
  ///     migrations.
  /// </param>
  /// <param name="supportedEngineIds">
  ///     The supported engine ids for the active target
  ///     language, when that constraint should be applied.
  /// </param>
  /// <returns><see langword="true" /> when the config changed.</returns>
  internal static bool NormalizeAndSyncSelection(
      Config config,
      int loadedConfigVersion,
      IReadOnlyCollection<int>? supportedEngineIds = null)
  {
    var changed = false;

    var normalizedChatGptBaseUrl =
        NormalizeLegacyChatGptBaseUrl(config.ChatGPTBaseUrl);
    if (!string.Equals(
            normalizedChatGptBaseUrl,
            config.ChatGPTBaseUrl,
            StringComparison.Ordinal))
    {
      config.ChatGPTBaseUrl = normalizedChatGptBaseUrl;
      changed = true;
    }

    var resolvedEngineId =
        ResolvePersistedEngineSelection(
            loadedConfigVersion,
            config.ChosenTransEngine,
            config.ChosenTransEngineKey);
    if (supportedEngineIds != null)
    {
      resolvedEngineId = ResolveSupportedEngineSelection(
          resolvedEngineId,
          supportedEngineIds);
    }

    if (config.ChosenTransEngine != resolvedEngineId)
    {
      config.ChosenTransEngine = resolvedEngineId;
      changed = true;
    }

    var normalizedEngineKey =
        ((Echoglossian.TransEngines)resolvedEngineId).ToString();
    if (!string.Equals(
            config.ChosenTransEngineKey,
            normalizedEngineKey,
            StringComparison.Ordinal))
    {
      config.ChosenTransEngineKey = normalizedEngineKey;
      changed = true;
    }

    if (config.Version < TranslationEngineSchemaVersion)
    {
      config.Version = TranslationEngineSchemaVersion;
      changed = true;
    }

    return changed;
  }

  /// <summary>
  ///     Applies an explicit user-selected engine to both persisted selection
  ///     forms so later normalization cannot silently revert the choice.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <param name="selectedEngineId">The explicitly selected concrete engine id.</param>
  internal static void ApplyExplicitSelection(
      Config config,
      int selectedEngineId)
  {
    config.ChosenTransEngine = selectedEngineId;
    config.ChosenTransEngineKey =
        ((Echoglossian.TransEngines)selectedEngineId).ToString();
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
  ///     Resolves the effective persisted engine selection from the available
  ///     persisted forms.
  /// </summary>
  /// <param name="loadedConfigVersion">
  ///     The config version loaded from disk before
  ///     migrations.
  /// </param>
  /// <param name="chosenEngineId">The persisted numeric engine id.</param>
  /// <param name="chosenEngineKey">The persisted engine key.</param>
  /// <returns>The resolved concrete engine id.</returns>
  private static int ResolvePersistedEngineSelection(
      int loadedConfigVersion,
      int chosenEngineId,
      string? chosenEngineKey)
  {
    if (loadedConfigVersion <
        TranslationEngineSchemaVersion &&
        TryResolveEngineKey(chosenEngineKey, out var engineIdFromKey))
    {
      return engineIdFromKey;
    }

    if (TryMigrateLegacyV325Selection(
            loadedConfigVersion,
            chosenEngineId,
            out var migratedEngineId))
    {
      return migratedEngineId;
    }

    if (IsConcreteEngineId(chosenEngineId))
    {
      return chosenEngineId;
    }

    if (TryResolveEngineKey(chosenEngineKey, out var currentEngineIdFromKey))
    {
      return currentEngineIdFromKey;
    }

    return (int)Echoglossian.TransEngines.Google;
  }

}
