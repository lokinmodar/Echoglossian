// <copyright file="QuestPopupPersistenceTarget.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Identifies which persistence bucket a quest popup should use.
/// </summary>
internal enum QuestPopupPersistenceTarget
{
    /// <summary>
    ///     Persist to the canonical quest plate table.
    /// </summary>
    CanonicalQuestPlate,

    /// <summary>
    ///     Persist to the dedicated quest popup table.
    /// </summary>
    DedicatedPopupTable,
}
