// <copyright file="DialogueTranslationSessionStoreTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers runtime-only short-lived dialogue session history behavior.
/// </summary>
public class DialogueTranslationSessionStoreTests
{
  /// <summary>
  ///     Ensures the store returns prior turns for the same session while
  ///     appending the current turn for future requests.
  /// </summary>
  [Fact]
  public void BuildContext_SameSession_ReturnsPriorTurnsOnly()
  {
    DialogueTranslationSessionStore.Clear();
    var firstObservedAtUtc = new DateTime(2026, 05, 12, 15, 0, 0, DateTimeKind.Utc);
    var secondObservedAtUtc = firstObservedAtUtc.AddSeconds(5);

    var firstContext = DialogueTranslationSessionStore.BuildContext(
        "Talk",
        "Krile|engine:8|target:pt-BR",
        "Krile",
        "Pray return.",
        3,
        TimeSpan.FromSeconds(30),
        firstObservedAtUtc);
    var secondContext = DialogueTranslationSessionStore.BuildContext(
        "Talk",
        "Krile|engine:8|target:pt-BR",
        "Krile",
        "We must press on.",
        3,
        TimeSpan.FromSeconds(30),
        secondObservedAtUtc);

    Assert.Empty(firstContext.PriorTurns);
    var priorTurn = Assert.Single(secondContext.PriorTurns);
    Assert.Equal("Krile", priorTurn.SpeakerName);
    Assert.Equal("Pray return.", priorTurn.SourceText);
  }

  /// <summary>
  ///     Ensures BattleTalk and Talk remain isolated even when the speaker key
  ///     is the same.
  /// </summary>
  [Fact]
  public void BuildContext_DifferentNamespaces_IsolatesHistory()
  {
    DialogueTranslationSessionStore.Clear();
    var observedAtUtc = new DateTime(2026, 05, 12, 15, 5, 0, DateTimeKind.Utc);

    DialogueTranslationSessionStore.BuildContext(
        "Talk",
        "Y'shtola|engine:8|target:pt-BR",
        "Y'shtola",
        "First talk line.",
        3,
        TimeSpan.FromSeconds(30),
        observedAtUtc);
    var isolatedContext = DialogueTranslationSessionStore.BuildContext(
        "_BattleTalk",
        "Y'shtola|engine:8|target:pt-BR",
        "Y'shtola",
        "First battle line.",
        3,
        TimeSpan.FromSeconds(30),
        observedAtUtc.AddSeconds(1));

    Assert.Empty(isolatedContext.PriorTurns);
  }

  /// <summary>
  ///     Ensures expired sessions are pruned before later requests reuse them.
  /// </summary>
  [Fact]
  public void BuildContext_ExpiredSession_DropsOldHistory()
  {
    DialogueTranslationSessionStore.Clear();
    var firstObservedAtUtc = new DateTime(2026, 05, 12, 15, 10, 0, DateTimeKind.Utc);
    var expiredObservedAtUtc = firstObservedAtUtc.AddMinutes(1);

    DialogueTranslationSessionStore.BuildContext(
        "Talk",
        "Alisaie|engine:8|target:pt-BR",
        "Alisaie",
        "Old line.",
        3,
        TimeSpan.FromSeconds(30),
        firstObservedAtUtc);
    var freshContext = DialogueTranslationSessionStore.BuildContext(
        "Talk",
        "Alisaie|engine:8|target:pt-BR",
        "Alisaie",
        "Fresh line.",
        3,
        TimeSpan.FromSeconds(30),
        expiredObservedAtUtc);

    Assert.Empty(freshContext.PriorTurns);
  }
}
