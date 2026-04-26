// <copyright file="QuestWaitingNotificationGate.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Tracks whether a quest addon has already emitted its waiting
///     notification for the current unresolved episode.
/// </summary>
internal sealed class QuestWaitingNotificationGate
{
  private bool notificationActive;

  /// <summary>
  ///     Tries to begin a new waiting-notification episode.
  /// </summary>
  /// <param name="blockingQuestCount">
  ///     The number of currently visible blocking quest rows.
  /// </param>
  /// <returns>
  ///     True only for the first unresolved observation in the current waiting
  ///     episode; otherwise, false.
  /// </returns>
  public bool TryBeginWaiting(int blockingQuestCount)
  {
    if (blockingQuestCount <= 0 || this.notificationActive)
    {
      return false;
    }

    this.notificationActive = true;
    return true;
  }

  /// <summary>
  ///     Clears the current waiting-notification episode.
  /// </summary>
  public void Clear()
  {
    this.notificationActive = false;
  }
}
