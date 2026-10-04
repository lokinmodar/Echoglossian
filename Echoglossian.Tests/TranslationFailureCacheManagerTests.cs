// <copyright file="TranslationFailureCacheManagerTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Translators;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the runtime-only exact-failure cache behavior used to suppress
///     repeated transient provider failures.
/// </summary>
public class TranslationFailureCacheManagerTests
{
  /// <summary>
  ///     Ensures transient failures become visible to the shared exact-failure
  ///     gate without being persisted through the database preload path.
  /// </summary>
  [Fact]
  public void RememberTransientFailure_MakesContainsReturnTrue()
  {
    TranslationFailureCacheManager.Clear();

    TranslationFailureCacheManager.RememberTransientFailure(
        "hello",
        "en",
        "pt-BR",
        (int)Echoglossian.TransEngines.ChatGPT,
        "llm-timeout",
        TimeSpan.FromSeconds(30));

    Assert.True(
        TranslationFailureCacheManager.Contains(
            "hello",
            "en",
            "pt-BR",
            (int)Echoglossian.TransEngines.ChatGPT));

    TranslationFailureCacheManager.Clear();
  }

  /// <summary>
  ///     Ensures broker retry cache checks ignore transient failures while
  ///     continuing to recognize persistent failures for the same request.
  /// </summary>
  [Fact]
  public void ContainsPersistent_ExcludesTransientFailuresAndKeepsPersistentFailures()
  {
    const string SourceText = "hello";
    const string SourceLanguage = "en";
    const string TargetLanguage = "pt-BR";
    const int TranslationEngine = (int)Echoglossian.TransEngines.ChatGPT;

    TranslationFailureCacheManager.Clear();
    try
    {
      TranslationFailureCacheManager.RememberTransientFailure(
          SourceText,
          SourceLanguage,
          TargetLanguage,
          TranslationEngine,
          "llm-quota-or-rate-limit",
          TimeSpan.FromSeconds(30));

      Assert.True(TranslationFailureCacheManager.Contains(
          SourceText,
          SourceLanguage,
          TargetLanguage,
          TranslationEngine));
      Assert.False(TranslationFailureCacheManager.ContainsPersistent(
          SourceText,
          SourceLanguage,
          TargetLanguage,
          TranslationEngine));

      TranslationFailureCacheManager.Update(new TranslationFailure
      {
        SourceText = SourceText,
        SourceTextHash = TranslationFailureKey.ComputeSourceTextHash(SourceText),
        SourceLanguage = SourceLanguage,
        TargetLanguage = TargetLanguage,
        TranslationEngine = TranslationEngine,
        FailureReason = "provider-rejected",
      });

      Assert.True(TranslationFailureCacheManager.ContainsPersistent(
          SourceText,
          SourceLanguage,
          TargetLanguage,
          TranslationEngine));
    }
    finally
    {
      TranslationFailureCacheManager.Clear();
    }
  }

  /// <summary>Ensures a V2-only record cannot short circuit Google V0.</summary>
  [Fact]
  public void Contains_V2OnlyFailure_RespectsVariantApplicability()
  {
    const int Google = (int)Echoglossian.TransEngines.Google;
    TranslationFailureCacheManager.Clear();
    try
    {
      TranslationFailureCacheManager.Update(new TranslationFailure
      {
        SourceText = "term", SourceTextHash = TranslationFailureKey.ComputeSourceTextHash("term"),
        SourceLanguage = "en", TargetLanguage = "pt-BR", TranslationEngine = Google,
        FailureReason = "google-v2-no-translation",
      });

      Assert.True(TranslationFailureCacheManager.Contains("term", "en", "pt-BR", Google,
          row => row.FailureReason == "google-v2-no-translation"));
      Assert.False(TranslationFailureCacheManager.Contains("term", "en", "pt-BR", Google,
          row => row.FailureReason != "google-v2-no-translation"));
    }
    finally
    {
      TranslationFailureCacheManager.Clear();
    }
  }
}
