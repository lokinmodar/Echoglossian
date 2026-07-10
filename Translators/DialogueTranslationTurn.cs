// <copyright file="DialogueTranslationTurn.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators;

/// <summary>
///     Represents one prior source-side dialogue turn retained in the
///     runtime-only short-lived session history.
/// </summary>
/// <param name="SpeakerName">The visible speaker name when available.</param>
/// <param name="SourceText">The original source text shown by the game.</param>
/// <param name="ObservedAtUtc">The UTC time when the turn was observed.</param>
public readonly record struct DialogueTranslationTurn(
    string SpeakerName,
    string SourceText,
    DateTime ObservedAtUtc);
