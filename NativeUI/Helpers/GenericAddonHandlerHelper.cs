// <copyright file="GenericAddonHandlerHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.NativeUI.AddonHandlers.Common;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Helper for performing async translation logic outside unsafe context.
/// </summary>
public static class GenericAddonHandlerHelper
{
  private const int MaxChunkLength = 4000;

  /// <summary>
  /// Performs async translation for one DB-first payload without persisting it.
  /// </summary>
  /// <param name="atkValues">The ATK values to translate.</param>
  /// <param name="stringArray">The StringArrayData values to translate.</param>
  /// <param name="textNodes">The text-node values to translate.</param>
  /// <param name="originalAtkSnapshot">The original ATK snapshot.</param>
  /// <param name="originalArraySnapshot">The original StringArray snapshot.</param>
  /// <param name="originalTextNodeSnapshot">The original text-node snapshot.</param>
  /// <param name="sourceLanguage">The source language.</param>
  /// <param name="targetLanguage">The target language.</param>
  /// <param name="service">The translation service.</param>
  /// <returns>
  /// One translated payload when coverage is complete; otherwise
  /// <see langword="null" />.
  /// </returns>
  internal static async Task<DbFirstGameWindowPayload?> TranslatePayloadAsync(
      IReadOnlyDictionary<int, string> atkValues,
      IReadOnlyDictionary<int, string> stringArray,
      IReadOnlyDictionary<string, string> textNodes,
      IReadOnlyDictionary<int, string> originalAtkSnapshot,
      IReadOnlyDictionary<int, string> originalArraySnapshot,
      IReadOnlyDictionary<string, string> originalTextNodeSnapshot,
      string sourceLanguage,
      string targetLanguage,
      TranslationService service)
  {
    var allPairs = new List<string>();
    allPairs.AddRange(atkValues.Select(kvp => $"a{kvp.Key}|{kvp.Value}"));
    allPairs.AddRange(stringArray.Select(kvp => $"s{kvp.Key}|{kvp.Value}"));
    allPairs.AddRange(textNodes.Select(kvp => $"t{kvp.Key}|{kvp.Value}"));

    var builder = new StringBuilder();
    var translatedMap = new Dictionary<string, string>();

    foreach (var pair in allPairs)
    {
      if (builder.Length + pair.Length + 1 > MaxChunkLength)
      {
        await TranslateAndMergeAsync(service, builder.ToString(), translatedMap, sourceLanguage, targetLanguage);
        builder.Clear();
      }

      if (builder.Length > 0)
      {
        builder.Append('|');
      }

      builder.Append(pair);
    }

    if (builder.Length > 0)
    {
      await TranslateAndMergeAsync(service, builder.ToString(), translatedMap, sourceLanguage, targetLanguage);
    }

    var updatedAtk = new Dictionary<int, string>();
    var updatedArray = new Dictionary<int, string>();
    var updatedTextNodes = new Dictionary<string, string>(
        StringComparer.Ordinal);

    foreach (var (key, val) in translatedMap)
    {
      if (key.StartsWith('a') && int.TryParse(key[1..], out var a))
      {
        updatedAtk[a] = val;
      }
      else if (key.StartsWith('s') && int.TryParse(key[1..], out var s))
      {
        updatedArray[s] = val;
      }
      else if (key.StartsWith('t'))
      {
        updatedTextNodes[key[1..]] = val;
      }
    }

    if (!HasCompleteTranslationCoverage(
            updatedAtk,
            updatedArray,
            updatedTextNodes,
            originalAtkSnapshot,
            originalArraySnapshot,
            originalTextNodeSnapshot))
    {
      await TranslateMissingEntriesIndividuallyAsync(
          service,
          updatedAtk,
          updatedArray,
          updatedTextNodes,
          originalAtkSnapshot,
          originalArraySnapshot,
          originalTextNodeSnapshot,
          sourceLanguage,
          targetLanguage);
    }

    if (!HasCompleteTranslationCoverage(
            updatedAtk,
            updatedArray,
            updatedTextNodes,
            originalAtkSnapshot,
            originalArraySnapshot,
            originalTextNodeSnapshot))
    {
      return null;
    }

    return new DbFirstGameWindowPayload(
        new SortedDictionary<int, string>(updatedAtk),
        new SortedDictionary<int, string>(updatedArray),
        new SortedDictionary<string, string>(
            updatedTextNodes,
            StringComparer.Ordinal));
  }

  /// <summary>
  /// Performs async translation and saves result to DB. Supports both generic and multi-text entities.
  /// </summary>
  /// <typeparam name="T">Entity type implementing IGenericEntity or IMultiTextEntity.</typeparam>
  /// <param name="addonName">The name of the addon.</param>
  /// <param name="atkValues">The filtered AtkValues to translate.</param>
  /// <param name="stringArray">The filtered StringArrayData to translate.</param>
  /// <param name="textNodes">The filtered visible text nodes to translate.</param>
  /// <param name="originalAtkSnapshot">The original snapshot of AtkValues before translation.</param>
  /// <param name="originalArraySnapshot">The original snapshot of StringArrayData before translation.</param>
  /// <param name="originalTextNodeSnapshot">The original snapshot of text nodes before translation.</param>
  /// <param name="config">The plugin configuration.</param>
  /// <param name="service">The translation service instance.</param>
  /// <returns> </returns>
  public static async Task<bool> PerformTranslationAndSaveAsync<T>(
      string addonName,
      Dictionary<int, string> atkValues,
      Dictionary<int, string> stringArray,
      Dictionary<string, string> textNodes,
      Dictionary<int, string> originalAtkSnapshot,
      Dictionary<int, string> originalArraySnapshot,
      Dictionary<string, string> originalTextNodeSnapshot,
      Config config,
      TranslationService service)
      where T : class, IGenericEntity, new()
  {
    PluginRuntimeLog.Debug($"[{addonName}] [Async] Starting translation...");
    Type entityType = typeof(T);
    PluginRuntimeLog.Debug($"[{addonName}] Entity type resolved as {entityType.Name}");

    try
    {
      var sourceLang = ClientStateInterface.ClientLanguage.Humanize();
      var targetLang = RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
          config.Lang);

      var translatedPayloadResult = await TranslatePayloadAsync(
          atkValues,
          stringArray,
          textNodes,
          originalAtkSnapshot,
          originalArraySnapshot,
          originalTextNodeSnapshot,
          sourceLang,
          targetLang,
          service);
      if (!translatedPayloadResult.HasValue)
      {
        PluginRuntimeLog.Debug(
            $"[{addonName}] [Async] Skipping persistence because the translated payload is empty or incomplete.");
        return false;
      }

      var translatedPayload = translatedPayloadResult.Value;
      var entity = new T();
      PluginRuntimeLog.Debug($"[{addonName}] [Async] Creating entity of type {entityType.Name}...");

      if (entity is IMultiTextEntity multi)
      {
        PluginRuntimeLog.Debug($"[{addonName}] [Async] Saving IMultiTextEntity...");

        var messageEntry = translatedPayload.AtkValues
            .FirstOrDefault(kvp => kvp.Key == 0);
        var senderEntry = translatedPayload.AtkValues
            .FirstOrDefault(kvp => kvp.Key == 1);

        var originalMessage = originalAtkSnapshot.TryGetValue(0, out var origMsg) ? origMsg : messageEntry.Value;
        var originalSender = originalAtkSnapshot.TryGetValue(1, out var origSender) ? origSender : senderEntry.Value;

        multi.SetOriginalSecondaryText(originalMessage);
        multi.SetOriginalText(originalSender);
        multi.SetOriginalLang(sourceLang);

        multi.SetTranslatedSecondaryText(messageEntry.Value);
        multi.SetTranslatedText(senderEntry.Value);
        multi.SetTranslationLang(targetLang);
        multi.SetTranslationEngine(config.ChosenTransEngine);
      }
      else
      {
        PluginRuntimeLog.Debug($"[{addonName}] [Async] Saving IGenericEntity...");

        var translatedJson = translatedPayload.Serialize();

        var originalJson = JsonConvert.SerializeObject(new
        {
          atkValues = originalAtkSnapshot.Count > 0 ? originalAtkSnapshot : null,
          stringArrayData = originalArraySnapshot.Count > 0 ? originalArraySnapshot : null,
          textNodes = originalTextNodeSnapshot.Count > 0
              ? originalTextNodeSnapshot
              : null,
        });

        entity.SetOriginalText(originalJson);
        entity.SetTranslatedText(translatedJson);
        entity.SetOriginalLang(sourceLang);
        entity.SetTranslationLang(targetLang);
        entity.SetTranslationEngine(config.ChosenTransEngine);
      }

      entity.SetEntityKey(addonName);

      if (entity is GameWindow gw)
      {
        PluginRuntimeLog.Debug($"[{addonName}] [Async] Saving GameWindow...");
        gw.GameVersion = GetGameVersion();
        InsertGameWindow(gw);
        GameWindowCacheManager.Update(gw);
      }
      else
      {
        if (entity.GetGameVersion() is null && entity is not IMultiTextEntity)
        {
          var gameVersion = GetGameVersion();
          if (!string.IsNullOrWhiteSpace(gameVersion))
          {
            entity.SetGameVersion(gameVersion);
          }
        }

        await InsertEntity(entity);
      }

      PluginRuntimeLog.Debug($"[{addonName}] [Async] Translation saved successfully.");
      return true;
    }
    catch (Exception ex)
    {
      PluginRuntimeLog.Error($"[{addonName}] [Async] Error during translation: {ex}");
      return false;
    }
  }

  private static async Task TranslateAndMergeAsync(
      TranslationService service,
      string chunk,
      Dictionary<string, string> result,
      string sourceLang,
      string targetLang)
  {
    PluginRuntimeLog.Debug($"Translating chunk of length {chunk.Length} characters.");
    var translated = await service.TranslateAsync(chunk, sourceLang, targetLang);
    if (string.IsNullOrWhiteSpace(translated))
    {
      return;
    }

    var parts = translated.Split('|');
    for (int i = 0; i < parts.Length - 1; i += 2)
    {
      var key = parts[i];
      var val = parts[i + 1];
      result[key] = val;
    }
  }

  private static bool HasCompleteTranslationCoverage(
      IReadOnlyDictionary<int, string> translatedAtkValues,
      IReadOnlyDictionary<int, string> translatedStringArrayValues,
      IReadOnlyDictionary<string, string> translatedTextNodes,
      IReadOnlyDictionary<int, string> originalAtkSnapshot,
      IReadOnlyDictionary<int, string> originalArraySnapshot,
      IReadOnlyDictionary<string, string> originalTextNodeSnapshot)
  {
    if (translatedAtkValues.Count == 0 &&
        translatedStringArrayValues.Count == 0 &&
        translatedTextNodes.Count == 0)
    {
      return false;
    }

    foreach (var key in originalAtkSnapshot.Keys)
    {
      if (!translatedAtkValues.ContainsKey(key))
      {
        return false;
      }
    }

    foreach (var key in originalArraySnapshot.Keys)
    {
      if (!translatedStringArrayValues.ContainsKey(key))
      {
        return false;
      }
    }

    foreach (var key in originalTextNodeSnapshot.Keys)
    {
      if (!translatedTextNodes.ContainsKey(key))
      {
        return false;
      }
    }

    return true;
  }

  /// <summary>
  ///     Translates any entries that were missing from the batched response one
  ///     by one so sparse parser failures do not discard an otherwise valid
  ///     payload.
  /// </summary>
  /// <param name="service">The translation service.</param>
  /// <param name="translatedAtkValues">The translated ATK values map to fill.</param>
  /// <param name="translatedStringArrayValues">
  ///     The translated StringArrayData map to fill.
  /// </param>
  /// <param name="originalAtkSnapshot">The original ATK snapshot.</param>
  /// <param name="originalArraySnapshot">The original StringArrayData snapshot.</param>
  /// <param name="sourceLanguage">The source language.</param>
  /// <param name="targetLanguage">The target language.</param>
  private static async Task TranslateMissingEntriesIndividuallyAsync(
      TranslationService service,
      IDictionary<int, string> translatedAtkValues,
      IDictionary<int, string> translatedStringArrayValues,
      IDictionary<string, string> translatedTextNodes,
      IReadOnlyDictionary<int, string> originalAtkSnapshot,
      IReadOnlyDictionary<int, string> originalArraySnapshot,
      IReadOnlyDictionary<string, string> originalTextNodeSnapshot,
      string sourceLanguage,
      string targetLanguage)
  {
    foreach (var (index, originalText) in originalAtkSnapshot)
    {
      if (translatedAtkValues.ContainsKey(index))
      {
        continue;
      }

      var translatedText = await service.TranslateAsync(
          originalText,
          sourceLanguage,
          targetLanguage);
      if (string.IsNullOrWhiteSpace(translatedText))
      {
        continue;
      }

      translatedAtkValues[index] = translatedText;
    }

    foreach (var (index, originalText) in originalArraySnapshot)
    {
      if (translatedStringArrayValues.ContainsKey(index))
      {
        continue;
      }

      var translatedText = await service.TranslateAsync(
          originalText,
          sourceLanguage,
          targetLanguage);
      if (string.IsNullOrWhiteSpace(translatedText))
      {
        continue;
      }

      translatedStringArrayValues[index] = translatedText;
    }

    foreach (var (key, originalText) in originalTextNodeSnapshot)
    {
      if (translatedTextNodes.ContainsKey(key))
      {
        continue;
      }

      var translatedText = await service.TranslateAsync(
          originalText,
          sourceLanguage,
          targetLanguage);
      if (string.IsNullOrWhiteSpace(translatedText))
      {
        continue;
      }

      translatedTextNodes[key] = translatedText;
    }
  }
}


