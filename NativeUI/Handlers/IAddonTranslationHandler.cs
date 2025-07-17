// <copyright file="IAddonTranslationHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Handlers;

using static IAddonLifecycle;

/// <summary>
///     Interface for a reusable addon handler that maps AddonEvents to their
///     delegates.
/// </summary>
public interface IAddonTranslationHandler
{
    /// <summary>
    ///     Returns a mapping of event types to their combined delegate(s).
    /// </summary>
    /// <returns>
    ///     A dictionary mapping <see cref="AddonEvent" /> to their corresponding
    ///     <see cref="AddonEventDelegate" />.
    /// </returns>
    Dictionary<AddonEvent, AddonEventDelegate> GetEventHandlers();
}