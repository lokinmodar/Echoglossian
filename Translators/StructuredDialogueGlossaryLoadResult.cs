// <copyright file="StructuredDialogueGlossaryLoadResult.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators;

/// <summary>
///     Describes one glossary load attempt for the structured dialogue runtime.
/// </summary>
/// <param name="Succeeded">Whether the load attempt succeeded.</param>
/// <param name="Entries">The normalized glossary entries loaded.</param>
/// <param name="SkippedEntryCount">
///     The number of malformed or empty rows skipped during normalization.
/// </param>
/// <param name="FailureDetail">
///     The failure detail when the load attempt failed.
/// </param>
public readonly record struct StructuredDialogueGlossaryLoadResult(
    bool Succeeded,
    IReadOnlyList<StructuredDialogueGlossaryEntry> Entries,
    int SkippedEntryCount,
    string? FailureDetail);
