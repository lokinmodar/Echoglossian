// <copyright file="ClassJobCategorySheetHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using ClassJobCategorySheet = Lumina.Excel.Sheets.ClassJobCategory;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Reads class-job-category membership directly from sheet-backed row data
///     so newly added jobs do not depend on generated property names.
/// </summary>
internal static class ClassJobCategorySheetHelper
{
    /// <summary>
    ///     Returns whether the provided class-job-category row contains the
    ///     specified class/job identifier.
    /// </summary>
    /// <param name="category">The class-job-category row.</param>
    /// <param name="classJobId">The class/job row identifier.</param>
    /// <returns>
    ///     <see langword="true" /> when the category includes the requested
    ///     class/job; otherwise, <see langword="false" />.
    /// </returns>
    internal static bool HasClassJob(
        ClassJobCategorySheet? category,
        uint classJobId)
    {
        if (category == null ||
            !SheetRowIdNormalizationHelper.IsMeaningfulRowId(classJobId))
        {
            return false;
        }

        var categoryValue = category.Value;

        try
        {
            return categoryValue.ExcelPage.ReadBool(
                categoryValue.RowOffset + classJobId + 4);
        }
        catch
        {
            return false;
        }
    }
}
