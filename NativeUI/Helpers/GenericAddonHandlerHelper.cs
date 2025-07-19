// <copyright file="GenericAddonHandlerHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>


namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Helper for performing async translation logic outside unsafe context.
/// </summary>
public static class GenericAddonHandlerHelper
{
  private const int MaxChunkLength = 4000;

  /// <summary>
  ///     Performs translation and DB save in a safe context.
  /// </summary>
  /// <typeparam name="T">Generic entity type.</typeparam>
  /// <param name="addonName">Addon name.</param>
  /// <param name="atkValues">ATK values dictionary.</param>
  /// <param name="stringArray">StringArrayData dictionary.</param>
  /// <param name="config">Plugin config.</param>
  /// <param name="service">Translation service.</param>
  /// <param name="entityType">Entity type used for DB ops.</param>
  public static async Task PerformTranslationAndSaveAsync<T>(
      string addonName,
      Dictionary<int, string> atkValues,
      Dictionary<int, string> stringArray,
      Config config,
      TranslationService service,
      Type entityType)
      where T : class, IGenericEntity, new()
  {
    try
    {
      var sourceLang = ClientStateInterface.ClientLanguage.Humanize();
      var targetLang = LangDict[config.Lang].Code;

      var allPairs = new List<string>();
      allPairs.AddRange(atkValues.Select(kvp => $"a{kvp.Key}|{kvp.Value}"));
      allPairs.AddRange(stringArray.Select(kvp => $"s{kvp.Key}|{kvp.Value}"));

      var translatedMap = new Dictionary<string, string>();
      var builder = new StringBuilder();
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

      var payload = new { atkValues = updatedAtk, stringArrayData = updatedArray };
      var translatedJson = JsonConvert.SerializeObject(payload);
      var originalJson = string.Join('|', allPairs);

      var entity = new T();
      entity.SetTranslatedText(translatedJson);

      if (entity is not IMultiTextEntity)
      {
        entity.SetOriginalText(originalJson);
        entity.SetOriginalLang(sourceLang);
      }

      entity.SetTranslationLang(targetLang);
      entity.SetTranslationEngine(config.ChosenTransEngine);
      entity.SetEntityKey(addonName);

      if (entity.GetGameVersion() is null && entity is not IMultiTextEntity)
      {
        entity.SetGameVersion(GetGameVersion());
      }

      await InsertEntity(entity);

      PluginLog.Debug($"[{addonName}] Translation saved to DB from helper.");
    }
    catch (Exception ex)
    {
      PluginLog.Error($"[{nameof(GenericAddonHandlerHelper)}] Translation error: {ex.Message}");
    }
  }

  private static async Task TranslateAndMergeAsync(
      TranslationService service,
      string chunk,
      Dictionary<string, string> result,
      string sourceLang,
      string targetLang)
  {
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
