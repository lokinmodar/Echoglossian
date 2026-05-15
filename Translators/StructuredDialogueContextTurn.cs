// <copyright file="StructuredDialogueContextTurn.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators;

/// <summary>
///     Represents one prior dialogue turn included in a structured LLM request.
/// </summary>
/// <param name="SpeakerOriginal">The original speaker name.</param>
/// <param name="TextOriginal">The original source-side dialogue text.</param>
public readonly record struct StructuredDialogueContextTurn(
    string SpeakerOriginal,
    string TextOriginal);
