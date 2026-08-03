// <copyright file="QuestPopupIdentity.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Reads explicit quest identity from quest popup setup payloads only when
///     a proven contract exists for that popup surface.
/// </summary>
internal static class QuestPopupIdentity
{
    /// <summary>
    ///     Attempts to read a canonical quest id from JournalAccept setup
    ///     payload values.
    /// </summary>
    /// <param name="setupAtkValues">The popup setup values.</param>
    /// <param name="questId">The resolved quest id, if any.</param>
    /// <returns>
    ///     <see langword="true" /> when a validated quest id contract exists;
    ///     otherwise, <see langword="false" />.
    /// </returns>
    internal static unsafe bool TryReadJournalAcceptQuestId(
        AtkValue* setupAtkValues,
        out string questId)
    {
        questId = string.Empty;
        return false;
    }

    /// <summary>
    ///     Attempts to read a canonical quest id from JournalResult setup
    ///     payload values.
    /// </summary>
    /// <param name="setupAtkValues">The popup setup values.</param>
    /// <param name="questId">The resolved quest id, if any.</param>
    /// <returns>
    ///     <see langword="true" /> when a validated quest id contract exists;
    ///     otherwise, <see langword="false" />.
    /// </returns>
    internal static unsafe bool TryReadJournalResultQuestId(
        AtkValue* setupAtkValues,
        out string questId)
    {
        questId = string.Empty;
        return false;
    }
}
