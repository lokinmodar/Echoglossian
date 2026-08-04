// <copyright file="SelectionDialogCaptureSourceKind.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.SelectionDialogs;

/// <summary>
///     Identifies which live addon data surface supplied the current
///     selection-dialog text payload.
/// </summary>
internal enum SelectionDialogCaptureSourceKind
{
    /// <summary>
    ///     No usable capture source was available.
    /// </summary>
    None = 0,

    /// <summary>
    ///     Text was captured directly from <c>AtkValue</c> slots.
    /// </summary>
    AtkValues,

    /// <summary>
    ///     Text was captured from <c>StringArrayData</c>.
    /// </summary>
    StringArrayData,

    /// <summary>
    ///     Text was scraped from readable text nodes.
    /// </summary>
    TextNodes,
}
