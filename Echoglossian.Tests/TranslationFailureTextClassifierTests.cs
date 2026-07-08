// <copyright file="TranslationFailureTextClassifierTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Properties;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers shared classification of translator outputs that represent
///     runtime failures rather than real translations.
/// </summary>
public class TranslationFailureTextClassifierTests
{
  /// <summary>
  ///     Ensures exact localized unavailable messages are rejected as real
  ///     translations and classified as engine-unavailable failures.
  /// </summary>
  [Fact]
  public void TryClassify_UnavailableMessage_ReturnsEngineUnavailable()
  {
    var message = Resources.ChatGPTTranslationUnavailablePleaseCheckYourAPIKey;

    var classified = TranslationFailureTextClassifier.TryClassify(
        message,
        out var classification);

    Assert.True(classified);
    Assert.NotNull(classification);
    Assert.Equal(
        TranslationFailureKind.EngineUnavailable,
        classification!.Kind);
    Assert.False(classification.ShouldNotifyOperator);
    Assert.Equal(message, classification.UserFacingMessage);
  }

  /// <summary>
  ///     Ensures the custom OpenAI-compatible unavailable message is also
  ///     treated as an engine-unavailable failure instead of a real
  ///     translation.
  /// </summary>
  [Fact]
  public void TryClassify_OpenAiCompatibleUnavailableMessage_ReturnsEngineUnavailable()
  {
    var message =
        Resources.OpenAiCompatibleTranslationUnavailablePleaseCheckProviderConfiguration;

    var classified = TranslationFailureTextClassifier.TryClassify(
        message,
        out var classification);

    Assert.True(classified);
    Assert.NotNull(classification);
    Assert.Equal(
        TranslationFailureKind.EngineUnavailable,
        classification!.Kind);
    Assert.False(classification.ShouldNotifyOperator);
    Assert.Equal(message, classification.UserFacingMessage);
  }

  /// <summary>
  ///     Ensures quota-like synthetic provider errors are normalized into the
  ///     quota-or-rate-limit category.
  /// </summary>
  [Fact]
  public void TryClassify_QuotaPlaceholder_ReturnsQuotaFailure()
  {
    const string Message =
        "[Translation Error: OpenAI: insufficient_quota for current plan]";

    var classified = TranslationFailureTextClassifier.TryClassify(
        Message,
        out var classification);

    Assert.True(classified);
    Assert.NotNull(classification);
    Assert.Equal(
        TranslationFailureKind.QuotaOrRateLimit,
        classification!.Kind);
    Assert.True(classification.ShouldNotifyOperator);
    Assert.Contains("insufficient_quota", classification.UserFacingMessage);
  }

  /// <summary>
  ///     Ensures ordinary translated text is not misclassified as a runtime
  ///     failure.
  /// </summary>
  [Fact]
  public void TryClassify_NormalTranslatedText_ReturnsFalse()
  {
    var classified = TranslationFailureTextClassifier.TryClassify(
        "Texto traduzido normal",
        out var classification);

    Assert.False(classified);
    Assert.Null(classification);
  }
}
