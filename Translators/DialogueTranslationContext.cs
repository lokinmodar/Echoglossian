// <copyright file="DialogueTranslationContext.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators;

/// <summary>
///     Holds runtime-only short-lived dialogue session context for one live
///     translation request.
/// </summary>
/// <param name="SessionNamespace">The isolated dialogue session namespace, such as Talk or BattleTalk.</param>
/// <param name="SessionKey">The runtime session key within that namespace.</param>
/// <param name="SpeakerName">The visible speaker name when available.</param>
/// <param name="PriorTurns">The rolling source-side history preceding the current request.</param>
/// <param name="SpeakerRoleHint">The resolved current speaker role hint.</param>
/// <param name="SpeakerGenderHint">The resolved current speaker gender hint.</param>
/// <param name="AddresseeHint">The resolved current addressee name hint.</param>
/// <param name="AddresseeRoleHint">The resolved current addressee role hint.</param>
/// <param name="AddresseeGenderHint">The resolved current addressee gender hint.</param>
/// <param name="MetadataProvenance">The source that resolved the current metadata.</param>
/// <param name="MetadataConfidenceTier">The confidence tier of the current metadata.</param>
public readonly record struct DialogueTranslationContext(
    string SessionNamespace,
    string SessionKey,
    string SpeakerName,
    IReadOnlyList<DialogueTranslationTurn> PriorTurns,
    string? SpeakerRoleHint = null,
    string? SpeakerGenderHint = null,
    string? AddresseeHint = null,
    string? AddresseeRoleHint = null,
    string? AddresseeGenderHint = null,
    string? MetadataProvenance = null,
    int? MetadataConfidenceTier = null);
