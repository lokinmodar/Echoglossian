// <copyright file="StructuredDialogueGlossaryEntry.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Text.Json.Serialization;

namespace Echoglossian.Translators;

/// <summary>
///     Represents one glossary row that may be injected into a structured
///     dialogue translation request.
/// </summary>
/// <param name="SourceText">The source-language term.</param>
/// <param name="TargetText">The target-language term.</param>
/// <param name="Comment">Optional operator note or disambiguation hint.</param>
/// <param name="SourceLanguage">Optional source-language scope.</param>
/// <param name="TargetLanguage">Optional target-language scope.</param>
public readonly record struct StructuredDialogueGlossaryEntry(
    [property: JsonPropertyName("source_text")]
    string SourceText,
    [property: JsonPropertyName("target_text")]
    string TargetText,
    [property: JsonPropertyName("comment")]
    string? Comment = null,
    [property: JsonPropertyName("source_language")]
    string? SourceLanguage = null,
    [property: JsonPropertyName("target_language")]
    string? TargetLanguage = null);
