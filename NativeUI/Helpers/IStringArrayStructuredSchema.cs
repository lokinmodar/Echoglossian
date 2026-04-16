// <copyright file="IStringArrayStructuredSchema.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Defines the typed schema for one canonical StringArrayData-backed
///     surface.
/// </summary>
public interface IStringArrayStructuredSchema
{
    /// <summary>
    ///     Gets the logical surface type written to the DB row.
    /// </summary>
    string Type { get; }

    /// <summary>
    ///     Gets the schema version for the canonical payload.
    /// </summary>
    int SchemaVersion { get; }

    /// <summary>
    ///     Tries to describe one slot in the structured payload.
    /// </summary>
    /// <param name="slotIndex">The source slot index.</param>
    /// <param name="slotText">The source slot text.</param>
    /// <param name="description">The resolved slot description.</param>
    /// <returns>
    ///     <see langword="true" /> when the slot belongs in the payload;
    ///     otherwise <see langword="false" />.
    /// </returns>
    bool TryDescribeSlot(
        int slotIndex,
        string? slotText,
        out StringArrayStructuredSlotDescription description);
}

/// <summary>
///     Describes one canonical slot in a StringArrayData-backed structured
///     payload.
/// </summary>
/// <param name="SemanticKey">The stable semantic key for the slot.</param>
/// <param name="IsVisible">Whether the slot is user-visible.</param>
/// <param name="IsTranslatable">Whether the slot should be translated.</param>
public sealed record StringArrayStructuredSlotDescription(
    string SemanticKey,
    bool IsVisible = true,
    bool IsTranslatable = true);
