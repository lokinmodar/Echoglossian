// <copyright file="StructuredDialogueTranslationMetadata.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators;

/// <summary>
///     Carries optional structured metadata that may help LLM dialogue
///     translation quality without changing the core source text itself.
/// </summary>
/// <param name="QuestNameOriginal">The original quest name when relevant.</param>
/// <param name="SpeakerOriginal">The original visible speaker name.</param>
/// <param name="SpeakerRoleHint">An optional role hint such as npc or player.</param>
/// <param name="PronounHint">An optional operator or runtime pronoun hint.</param>
/// <param name="SubjectHint">An optional omitted-subject hint.</param>
public readonly record struct StructuredDialogueTranslationMetadata(
    string? QuestNameOriginal,
    string? SpeakerOriginal,
    string? SpeakerRoleHint,
    string? PronounHint,
    string? SubjectHint);
