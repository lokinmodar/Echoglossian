// <copyright file="QuestWaitingNotificationGateTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Quest;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the shared quest-addon waiting-notification gating behavior.
/// </summary>
public class QuestWaitingNotificationGateTests
{
  /// <summary>
  ///     Ensures one waiting episode only emits one notification regardless of
  ///     how the visible blocking quest set changes while the addon is still
  ///     unresolved.
  /// </summary>
  [Fact]
  public void TryBeginWaiting_ReturnsTrueOnlyOncePerWaitingEpisode()
  {
    var gate = new QuestWaitingNotificationGate();

    Assert.True(gate.TryBeginWaiting(3));
    Assert.False(gate.TryBeginWaiting(2));
    Assert.False(gate.TryBeginWaiting(1));
  }

  /// <summary>
  ///     Ensures zero visible blocking quests never start a notification
  ///     episode.
  /// </summary>
  [Fact]
  public void TryBeginWaiting_ReturnsFalseWhenNothingIsBlocking()
  {
    var gate = new QuestWaitingNotificationGate();

    Assert.False(gate.TryBeginWaiting(0));
  }

  /// <summary>
  ///     Ensures a resolved waiting episode can notify again the next time the
  ///     addon re-enters a blocked state.
  /// </summary>
  [Fact]
  public void Clear_AllowsTheNextWaitingEpisodeToNotifyAgain()
  {
    var gate = new QuestWaitingNotificationGate();

    Assert.True(gate.TryBeginWaiting(2));

    gate.Clear();

    Assert.True(gate.TryBeginWaiting(1));
  }
}
