// <copyright file="GenericAddonHandlerHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Helper for performing async translation logic outside unsafe context.
/// </summary>
public static class GenericAddonHandlerHelper
{
  private const int MaxChunkLength = 4000;

  /// <summary>
  /// Performs async translation and saves result to DB. Supports both generic and multi-text entities.
  /// </summary>
  /// <typeparam name="T">Entity type implementing IGenericEntity or IMultiTextEntity.</typeparam>
  /// <param name="addonName">The name of the addon.</param>
  /// <param name="atkValues">The filtered AtkValues to translate.</param>
  /// <param name="stringArray">The filtered StringArrayData to translate.</param>
  /// <param name="originalAtkSnapshot">The original snapshot of AtkValues before translation.</param>
  /// <param name="originalArraySnapshot">The original snapshot of StringArrayData before translation.</param>
  /// <param name="config">The plugin configuration.</param>
  /// <param name="service">The translation service instance.</param>
  /// <returns> </returns>
  public static async Task PerformTranslationAndSaveAsync<T>(
      string addonName,
      Dictionary<int, string> atkValues,
      Dictionary<int, string> stringArray,
      Dictionary<int, string> originalAtkSnapshot,
      Dictionary<int, string> originalArraySnapshot,
      Config config,
      TranslationService service)
      where T : class, IGenericEntity, new()
  {
    PluginLog.Debug($"[{addonName}] [Async] Starting translation...");
    Type entityType = typeof(T);
    PluginLog.Debug($"[{addonName}] Entity type resolved as {entityType.Name}");

    try
    {
      var sourceLang = ClientStateInterface.ClientLanguage.Humanize();
      var targetLang = LangDict[config.Lang].Code;

      var allPairs = new List<string>();
      allPairs.AddRange(atkValues.Select(kvp => $"a{kvp.Key}|{kvp.Value}"));
      allPairs.AddRange(stringArray.Select(kvp => $"s{kvp.Key}|{kvp.Value}"));

      var builder = new StringBuilder();
      var translatedMap = new Dictionary<string, string>();

      foreach (var pair in allPairs)
      {
        if (builder.Length + pair.Length + 1 > MaxChunkLength)
        {
          await TranslateAndMergeAsync(service, builder.ToString(), translatedMap, sourceLang, targetLang);
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
        await TranslateAndMergeAsync(service, builder.ToString(), translatedMap, sourceLang, targetLang);
      }

      var updatedAtk = new Dictionary<int, string>();
      var updatedArray = new Dictionary<int, string>();

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
      }

      var entity = new T();
      PluginLog.Debug($"[{addonName}] [Async] Creating entity of type {entityType.Name}...");

      if (entity is IMultiTextEntity multi)
      {
        PluginLog.Debug($"[{addonName}] [Async] Saving IMultiTextEntity...");

        var messageEntry = updatedAtk.FirstOrDefault(kvp => kvp.Key == 0);
        var senderEntry = updatedAtk.FirstOrDefault(kvp => kvp.Key == 1);

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
        PluginLog.Debug($"[{addonName}] [Async] Saving IGenericEntity...");

        var translatedJson = JsonConvert.SerializeObject(new
        {
          atkValues = updatedAtk.Count > 0 ? updatedAtk : null,
          stringArrayData = updatedArray.Count > 0 ? updatedArray : null,
        });

        var originalJson = JsonConvert.SerializeObject(new
        {
          atkValues = originalAtkSnapshot.Count > 0 ? originalAtkSnapshot : null,
          stringArrayData = originalArraySnapshot.Count > 0 ? originalArraySnapshot : null,
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
        PluginLog.Debug($"[{addonName}] [Async] Saving GameWindow...");
        gw.GameVersion = GetGameVersion();
        InsertGameWindow(gw);
        GameWindowCacheManager.Update(gw);
      }
      else
      {
        if (entity.GetGameVersion() is null && entity is not IMultiTextEntity)
        {
          entity.SetGameVersion(GetGameVersion());
        }

        InsertEntity(entity);
      }

      PluginLog.Debug($"[{addonName}] [Async] Translation saved successfully.");
    }
    catch (Exception ex)
    {
      PluginLog.Error($"[{addonName}] [Async] Error during translation: {ex}");
    }
  }


  private static async Task TranslateAndMergeAsync(
      TranslationService service,
      string chunk,
      Dictionary<string, string> result,
      string sourceLang,
      string targetLang)
  {
    PluginLog.Debug($"Translating chunk of length {chunk.Length} characters.");
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
}
