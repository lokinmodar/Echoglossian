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
}
