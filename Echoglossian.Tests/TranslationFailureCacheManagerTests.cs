// <copyright file="TranslationFailureCacheManagerTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;

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
}
