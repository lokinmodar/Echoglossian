// <copyright file="VisibleStorySurfaceDiagnosticsSnapshot.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Represents one runtime-only diagnostic snapshot for the most recently
///     resolved visible story-facing surface.
/// </summary>
/// <param name="Surface">The active story surface.</param>
/// <param name="Provenance">The provenance label kind shown to operators.</param>
/// <param name="TableName">The effective DB manager table name for handoff.</param>
/// <param name="OriginalSpeakerText">The original speaker text when applicable.</param>
/// <param name="OriginalText">The primary original text payload.</param>
/// <param name="OriginalOptionsText">The original options payload when applicable.</param>
/// <param name="TranslatedSpeakerText">The translated speaker text when applicable.</param>
/// <param name="TranslatedText">The primary translated text payload.</param>
/// <param name="TranslatedOptionsText">The translated options payload when applicable.</param>
/// <param name="UsedRuntimeOnlyDialogueContext">
/// Whether runtime-only dialogue context influenced the live translation.
/// </param>
/// <param name="EffectiveTranslationEngineId">
/// The effective translation engine identifier used for the snapshot.
/// </param>
/// <param name="ObservedAtUtc">
/// The UTC timestamp when this snapshot was last updated, either from a live
/// observation or from an explicit retranslation outcome.
/// </param>
/// <param name="LastRetranslationSuccess">
/// The latest explicit retranslation success flag for this surface.
/// </param>
/// <param name="LastRetranslationMessage">
/// The latest explicit retranslation outcome message for this surface.
/// </param>
public readonly record struct VisibleStorySurfaceDiagnosticsSnapshot(
    VisibleStorySurfaceKind Surface,
    VisibleStorySurfaceProvenanceKind Provenance,
    string TableName,
    string OriginalSpeakerText,
    string OriginalText,
    string OriginalOptionsText,
    string TranslatedSpeakerText,
    string TranslatedText,
    string TranslatedOptionsText,
    bool UsedRuntimeOnlyDialogueContext,
    int EffectiveTranslationEngineId,
    DateTime ObservedAtUtc,
    bool? LastRetranslationSuccess,
    string? LastRetranslationMessage);
