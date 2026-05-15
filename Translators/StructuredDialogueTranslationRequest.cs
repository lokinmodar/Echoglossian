// <copyright file="StructuredDialogueTranslationRequest.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators;

/// <summary>
///     Represents the shared internal structured request contract for
///     dialogue-family LLM translation.
/// </summary>
/// <param name="SourceLanguage">The source-language code or display name.</param>
/// <param name="TargetLanguage">The target-language code or display name.</param>
/// <param name="SurfaceFamily">The coarse source surface family.</param>
/// <param name="SpeakerOriginal">The original visible speaker name.</param>
/// <param name="TextOriginal">The original visible source text.</param>
/// <param name="DialogueContext">The prior dialogue turns to include.</param>
/// <param name="Glossary">The glossary rows to inject.</param>
/// <param name="Metadata">Optional structured metadata hints.</param>
public readonly record struct StructuredDialogueTranslationRequest(
    string SourceLanguage,
    string TargetLanguage,
    string SurfaceFamily,
    string SpeakerOriginal,
    string TextOriginal,
    IReadOnlyList<StructuredDialogueContextTurn> DialogueContext,
    IReadOnlyList<StructuredDialogueGlossaryEntry> Glossary,
    StructuredDialogueTranslationMetadata Metadata);
