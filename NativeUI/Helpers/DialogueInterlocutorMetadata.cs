// <copyright file="DialogueInterlocutorMetadata.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
///     Specifies the evidence tier used to resolve dialogue interlocutor metadata.
/// </summary>
public enum DialogueInterlocutorResolutionTier
{
    /// <summary>
    ///     Indicates that exact persisted quest-sheet metadata supplied the result.
    /// </summary>
    QuestSheetDerivedExact,

    /// <summary>
    ///     Indicates that persisted quest-sheet metadata was enriched by a live actor.
    /// </summary>
    QuestSheetPlusLiveFusion,
}

/// <summary>
///     Represents the managed dialogue input used to resolve interlocutor metadata.
/// </summary>
/// <param name="SourceText">The visible source dialogue text.</param>
/// <param name="VisibleSpeakerName">The visible speaker name, when available.</param>
/// <param name="SourceLanguageCode">The source language persistence code.</param>
/// <param name="GameVersion">The game version that supplied the source text.</param>
/// <param name="DerivationVersion">The quest metadata derivation version.</param>
public readonly record struct DialogueInterlocutorMetadataRequest(
    string SourceText,
    string? VisibleSpeakerName,
    string SourceLanguageCode,
    string GameVersion,
    string DerivationVersion);

/// <summary>
///     Represents a copied live actor state that is safe to retain across awaits.
/// </summary>
/// <param name="Name">The actor name.</param>
/// <param name="DataId">The actor data identifier.</param>
/// <param name="GenderHint">The actor gender hint.</param>
/// <param name="RaceHint">The actor race hint.</param>
/// <param name="BodyTypeHint">The actor body-type hint.</param>
public sealed record LiveDialogueActorSnapshot(
    string Name,
    uint DataId,
    string? GenderHint,
    string? RaceHint,
    string? BodyTypeHint);

/// <summary>
///     Carries resolved interlocutor hints for one current dialogue request.
/// </summary>
/// <param name="SpeakerRoleHint">The resolved speaker role hint.</param>
/// <param name="SpeakerGenderHint">The resolved speaker gender hint.</param>
/// <param name="AddresseeHint">The resolved addressee name hint.</param>
/// <param name="AddresseeRoleHint">The resolved addressee role hint.</param>
/// <param name="AddresseeGenderHint">The resolved addressee gender hint.</param>
/// <param name="MetadataProvenance">The source that resolved the metadata.</param>
/// <param name="MetadataConfidenceTier">The confidence tier of the resolved metadata.</param>
public readonly record struct DialogueInterlocutorHints(
    string? SpeakerRoleHint,
    string? SpeakerGenderHint,
    string? AddresseeHint,
    string? AddresseeRoleHint,
    string? AddresseeGenderHint,
    string? MetadataProvenance,
    string? MetadataConfidenceTier);

/// <summary>
///     Represents the resolved persisted and live dialogue interlocutor hints.
/// </summary>
public sealed class DialogueInterlocutorMetadata
{
    /// <summary>
    ///     Gets the evidence tier used for this result.
    /// </summary>
    public required DialogueInterlocutorResolutionTier ResolutionTier { get; init; }

    /// <summary>
    ///     Gets the persisted speaker hint.
    /// </summary>
    public required string SpeakerHint { get; init; }

    /// <summary>
    ///     Gets the persisted addressee hint.
    /// </summary>
    public required string AddresseeHint { get; init; }

    /// <summary>
    ///     Gets the persisted speaker role hint.
    /// </summary>
    public required string SpeakerRoleHint { get; init; }

    /// <summary>
    ///     Gets the persisted addressee role hint.
    /// </summary>
    public required string AddresseeRoleHint { get; init; }

    /// <summary>
    ///     Gets the resolved speaker gender hint.
    /// </summary>
    public string? SpeakerGenderHint { get; init; }

    /// <summary>
    ///     Gets the resolved speaker race hint.
    /// </summary>
    public string? SpeakerRaceHint { get; init; }

    /// <summary>
    ///     Gets the resolved speaker body-type hint.
    /// </summary>
    public string? SpeakerBodyTypeHint { get; init; }

    /// <summary>
    ///     Gets the resolved addressee gender hint.
    /// </summary>
    public string? AddresseeGenderHint { get; init; }

    /// <summary>
    ///     Gets the resolved addressee race hint.
    /// </summary>
    public string? AddresseeRaceHint { get; init; }

    /// <summary>
    ///     Gets the resolved addressee body-type hint.
    /// </summary>
    public string? AddresseeBodyTypeHint { get; init; }

    /// <summary>
    ///     Gets the copied live speaker actor, when one matched.
    /// </summary>
    public LiveDialogueActorSnapshot? SpeakerActor { get; init; }

    /// <summary>
    ///     Gets the copied live addressee actor, when one matched.
    /// </summary>
    public LiveDialogueActorSnapshot? AddresseeActor { get; init; }
}
