// <copyright file="TranslationQueueHelpers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Newtonsoft.Json;

namespace Echoglossian;

public partial class Echoglossian
{
  /// <summary>
  ///     Attempts to read a queued translation from the shared broker cache.
  /// </summary>
  /// <param name="key">Stable translation key.</param>
  /// <param name="translatedText">The cached translated text, if any.</param>
  /// <returns>True when a cached translation exists.</returns>
  private bool TryGetQueuedTranslation(
      string key,
      out string translatedText)
  {
    return this.queuedTranslationBroker.TryGetCached(key, out translatedText);
  }

  /// <summary>
  ///     Enqueues a translation request on the shared broker without blocking
  ///     the addon lifecycle callback.
  /// </summary>
  /// <param name="key">Stable translation key.</param>
  /// <param name="resolver">Function that returns the translated text.</param>
  /// <param name="onResolved">Optional callback invoked after the text is cached.</param>
  /// <returns>True if the request was queued, false if one is already in flight.</returns>
  private bool QueueTranslation(
      string key,
      Func<Task<string>> resolver,
      Action<string>? onResolved = null)
  {
    return this.queuedTranslationBroker.Queue(
        key,
        resolver,
        onResolved);
  }

  /// <summary>
  ///     Enqueues a synchronous translation request on the shared broker
  ///     without blocking the broker pump thread.
  /// </summary>
  /// <param name="key">Stable translation key.</param>
  /// <param name="resolver">Function that returns the translated text.</param>
  /// <param name="onResolved">Optional callback invoked after the text is cached.</param>
  /// <returns>True if the request was queued, false if one is already in flight.</returns>
  private bool QueueTranslation(
      string key,
      Func<string> resolver,
      Action<string>? onResolved = null)
  {
    return this.QueueTranslation(
        key,
        () => Task.Run(resolver),
        onResolved);
  }

  /// <summary>
  ///     Serializes a pair of translated strings for broker caching.
  /// </summary>
  /// <param name="first">The first translated string.</param>
  /// <param name="second">The second translated string.</param>
  /// <returns>A JSON payload representing both strings.</returns>
  private static string SerializeTranslationPair(string first, string second)
  {
    return JsonConvert.SerializeObject(new[] { first, second });
  }

  /// <summary>
  /// Serializes a batch of translated strings for broker caching.
  /// </summary>
  /// <param name="values">The translated strings.</param>
  /// <returns>A JSON payload representing the translated strings.</returns>
  internal static string SerializeTranslationBatch(
      IReadOnlyCollection<string> values)
  {
    return JsonConvert.SerializeObject(values, Formatting.None);
  }

  /// <summary>
  ///     Tries to deserialize a cached translation pair payload.
  /// </summary>
  /// <param name="payload">The cached payload.</param>
  /// <param name="first">The first translated string.</param>
  /// <param name="second">The second translated string.</param>
  /// <returns>True when the payload contains two strings.</returns>
  private static bool TryDeserializeTranslationPair(
      string payload,
      out string first,
      out string second)
  {
    first = string.Empty;
    second = string.Empty;

    try
    {
      var items = JsonConvert.DeserializeObject<string[]>(payload);
      if (items == null || items.Length < 2)
      {
        return false;
      }

      first = items[0] ?? string.Empty;
      second = items[1] ?? string.Empty;
      return true;
    }
    catch
    {
      return false;
    }
  }

  /// <summary>
  /// Tries to deserialize a cached translation batch payload.
  /// </summary>
  /// <param name="payload">The cached payload.</param>
  /// <param name="values">The translated strings.</param>
  /// <returns>True when the payload contains a valid batch.</returns>
  internal static bool TryDeserializeTranslationBatch(
      string payload,
      out string[] values)
  {
    try
    {
      values = JsonConvert.DeserializeObject<string[]>(payload) ?? [];
      return true;
    }
    catch
    {
      values = [];
      return false;
    }
  }

  /// <summary>
  /// Queues a batch translation request and stores the serialized array result.
  /// </summary>
  /// <param name="key">The cache key.</param>
  /// <param name="sourceTexts">The source texts to translate.</param>
  /// <param name="onResolved">Optional callback invoked with the translated array.</param>
  /// <returns>True when the request is queued.</returns>
  private bool QueueTranslationBatch(
      string key,
      IReadOnlyCollection<string> sourceTexts,
      Action<string[]>? onResolved = null)
  {
    return this.QueueTranslation(
        key,
        () => SerializeTranslationBatch(sourceTexts.Select(this.Translate).ToArray()),
        translatedPayload =>
        {
          if (!TryDeserializeTranslationBatch(
                  translatedPayload,
                  out var translatedTexts))
          {
            return;
          }

          onResolved?.Invoke(translatedTexts);
        });
  }

  /// <summary>
  /// Persists a toast row into the correct historical cache/table according to
  /// its toast type.
  /// </summary>
  /// <param name="toastMessage">The translated toast row to persist.</param>
  /// <returns>The persistence result message.</returns>
  private string InsertToastMessageData(ToastMessage toastMessage)
  {
    return string.Equals(
            toastMessage.ToastType,
            "Error",
            StringComparison.OrdinalIgnoreCase)
        ? this.InsertErrorToastMessageData(toastMessage)
        : this.InsertOtherToastMessageData(toastMessage);
  }
}
