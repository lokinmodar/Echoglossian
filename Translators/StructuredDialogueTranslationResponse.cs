// <copyright file="StructuredDialogueTranslationResponse.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators;

/// <summary>
///     Represents the narrow shared structured response contract for
///     dialogue-family LLM translation.
/// </summary>
/// <param name="SpeakerTranslated">The translated speaker name.</param>
/// <param name="TextTranslated">The translated dialogue text.</param>
public readonly record struct StructuredDialogueTranslationResponse(
    string SpeakerTranslated,
    string TextTranslated);
