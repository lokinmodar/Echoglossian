// <copyright file="SheetRowIdNormalizationHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Normalizes sheet-backed row identifiers before they become canonical
///     runtime or persistence identity.
/// </summary>
internal static class SheetRowIdNormalizationHelper
{
    /// <summary>
    ///     Returns whether the provided row identifier represents one concrete
    ///     sheet row.
    /// </summary>
    /// <param name="rowId">The raw sheet row identifier.</param>
    /// <returns>
    ///     <see langword="true" /> when the row identifier represents one real
    ///     row; otherwise, <see langword="false" />.
    /// </returns>
    internal static bool IsMeaningfulRowId(uint rowId)
    {
        return rowId != 0 && rowId != uint.MaxValue;
    }

    /// <summary>
    ///     Normalizes one raw sheet row identifier to zero when the source
    ///     sheet exposes its empty sentinel.
    /// </summary>
    /// <param name="rowId">The raw sheet row identifier.</param>
    /// <returns>The normalized row identifier.</returns>
    internal static uint NormalizeOrZero(uint rowId)
    {
        return IsMeaningfulRowId(rowId) ? rowId : 0;
    }

    /// <summary>
    ///     Normalizes one raw scoped row identifier and falls back to one
    ///     caller-supplied identity when the sheet value is empty.
    /// </summary>
    /// <param name="rowId">The raw sheet row identifier.</param>
    /// <param name="fallbackRowId">The fallback identity.</param>
    /// <returns>The normalized resolved row identifier.</returns>
    internal static uint NormalizeWithFallback(uint rowId, uint fallbackRowId)
    {
        var normalizedRowId = NormalizeOrZero(rowId);
        if (normalizedRowId != 0)
        {
            return normalizedRowId;
        }

        return NormalizeOrZero(fallbackRowId);
    }
}
