// <copyright file="TranslationFailureTextClassifier.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
///     Describes the normalized class of a translated-output failure emitted by
///     a translator engine.
/// </summary>
internal enum TranslationFailureKind
{
  EmptyResult,
  SyntheticError,
  EngineUnavailable,
  Authentication,
  QuotaOrRateLimit,
  EndpointUnavailable,
  Timeout,
  ProviderFailure,
}

/// <summary>
///     Represents one normalized classification of a failed translated output.
/// </summary>
/// <param name="Kind">The normalized failure kind.</param>
/// <param name="FailureReason">The shared failure reason identifier.</param>
/// <param name="UserFacingMessage">The user-facing failure message.</param>
/// <param name="ShouldNotifyOperator">
///     Whether the plugin should surface a notification for this failure.
/// </param>
internal sealed record TranslationFailureClassification(
    TranslationFailureKind Kind,
    string FailureReason,
    string UserFacingMessage,
    bool ShouldNotifyOperator);

/// <summary>
///     Classifies translator outputs that are not safe to accept as real
///     translations and normalizes them into operator-meaningful failure
///     categories.
/// </summary>
internal static class TranslationFailureTextClassifier
{
  private static readonly string[] KnownCultureNames =
  {
    string.Empty,
    "da",
    "de",
    "el",
    "es",
    "eu",
    "fr",
    "it",
    "pt",
    "pt-BR",
    "ru",
  };

  private static readonly Lazy<HashSet<string>> KnownUnavailableMessages =
      new(BuildKnownUnavailableMessages);

  /// <summary>
  ///     Determines whether the specified text is one of the known localized
  ///     translator-unavailable messages returned when a provider is selected
  ///     but not configured.
  /// </summary>
  /// <param name="text">The translated text candidate.</param>
  /// <returns>
  ///     <see langword="true" /> when the text is a known unavailable message;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  public static bool IsKnownUnavailableTranslation(string? text)
  {
    if (string.IsNullOrWhiteSpace(text))
    {
      return false;
    }

    return KnownUnavailableMessages.Value.Contains(text.Trim());
  }

  /// <summary>
  ///     Attempts to classify the specified translated output as a known
  ///     failure.
  /// </summary>
  /// <param name="text">The translated text candidate.</param>
  /// <param name="classification">The normalized failure classification.</param>
  /// <returns>
  ///     <see langword="true" /> when the text represents a known failure;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  public static bool TryClassify(
      string? text,
      out TranslationFailureClassification? classification)
  {
    if (string.IsNullOrWhiteSpace(text))
    {
      classification = new TranslationFailureClassification(
          TranslationFailureKind.EmptyResult,
          "empty-result",
          string.Empty,
          false);
      return true;
    }

    if (IsKnownUnavailableTranslation(text))
    {
      classification = new TranslationFailureClassification(
          TranslationFailureKind.EngineUnavailable,
          "llm-engine-unavailable",
          text.Trim(),
          false);
      return true;
    }

    if (!TranslationResultGuard.ContainsSyntheticTranslationError(text))
    {
      classification = null;
      return false;
    }

    var normalizedMessage = NormalizeUserFacingMessage(text);
    var loweredMessage = normalizedMessage.ToLowerInvariant();
    if (ContainsAny(
            loweredMessage,
            "insufficient_quota",
            "quota",
            "rate limit",
            "rate-limit",
            "too many requests",
            "429"))
    {
      classification = new TranslationFailureClassification(
          TranslationFailureKind.QuotaOrRateLimit,
          "llm-quota-or-rate-limit",
          normalizedMessage,
          true);
      return true;
    }

    if (ContainsAny(
            loweredMessage,
            "unauthorized",
            "forbidden",
            "invalid api key",
            "incorrect api key",
            "authentication",
            "401",
            "403"))
    {
      classification = new TranslationFailureClassification(
          TranslationFailureKind.Authentication,
          "llm-authentication-failure",
          normalizedMessage,
          true);
      return true;
    }

    if (ContainsAny(
            loweredMessage,
            "timed out",
            "timeout",
            "taskcanceled",
            "task canceled",
            "task cancelled",
            "operation canceled",
            "operation cancelled"))
    {
      classification = new TranslationFailureClassification(
          TranslationFailureKind.Timeout,
          "llm-timeout",
          normalizedMessage,
          true);
      return true;
    }

    if (ContainsAny(
            loweredMessage,
            "connection refused",
            "actively refused",
            "no connection could be made",
            "name or service not known",
            "could not resolve",
            "could not be resolved",
            "host is unknown",
            "dns",
            "endpoint",
            "remote name could not be resolved",
            "no such host",
            "404",
            "502",
            "503",
            "connection reset"))
    {
      classification = new TranslationFailureClassification(
          TranslationFailureKind.EndpointUnavailable,
          "llm-endpoint-unavailable",
          normalizedMessage,
          true);
      return true;
    }

    classification = new TranslationFailureClassification(
        TranslationFailureKind.ProviderFailure,
        TranslationResultGuard.SyntheticErrorFailureReason,
        normalizedMessage,
        true);
    return true;
  }

  private static HashSet<string> BuildKnownUnavailableMessages()
  {
    var messages = new HashSet<string>(StringComparer.Ordinal);
    foreach (var cultureName in KnownCultureNames)
    {
      var culture = string.IsNullOrEmpty(cultureName)
          ? CultureInfo.InvariantCulture
          : CultureInfo.GetCultureInfo(cultureName);
      AddUnavailableMessage(
          messages,
          nameof(Resources.ChatGPTTranslationUnavailablePleaseCheckYourAPIKey),
          culture);
      AddUnavailableMessage(
          messages,
          nameof(Resources.OpenAiCompatibleTranslationUnavailablePleaseCheckProviderConfiguration),
          culture);
      AddUnavailableMessage(
          messages,
          nameof(Resources.ClaudeTranslationUnavailablePleaseCheckYourAPIKey),
          culture);
      AddUnavailableMessage(
          messages,
          nameof(Resources.DeepSeekTranslationUnavailablePleaseCheckYourAPIKey),
          culture);
      AddUnavailableMessage(
          messages,
          nameof(Resources.GeminiTranslationUnavailablePleaseCheckYourAPIKey),
          culture);
      AddUnavailableMessage(
          messages,
          nameof(Resources.MicrosoftTranslationUnavailablePleaseCheckYourAPIKey),
          culture);
    }

    return messages;
  }

  private static void AddUnavailableMessage(
      ISet<string> messages,
      string resourceName,
      CultureInfo culture)
  {
    var message = Resources.ResourceManager.GetString(resourceName, culture);
    if (string.IsNullOrWhiteSpace(message))
    {
      return;
    }

    messages.Add(message.Trim());
  }

  private static string NormalizeUserFacingMessage(string text)
  {
    var trimmedText = text.Trim();
    if (trimmedText.StartsWith("[", StringComparison.Ordinal) &&
        trimmedText.EndsWith("]", StringComparison.Ordinal) &&
        trimmedText.Length > 1)
    {
      return trimmedText[1..^1].Trim();
    }

    return trimmedText;
  }

  private static bool ContainsAny(
      string text,
      params string[] candidates)
  {
    foreach (var candidate in candidates)
    {
      if (text.Contains(candidate, StringComparison.Ordinal))
      {
        return true;
      }
    }

    return false;
  }
}
