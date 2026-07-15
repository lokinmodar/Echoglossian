// <copyright file="StructuredDialogueResponseValidationResult.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators;

/// <summary>
///     Represents the result of validating one structured dialogue translation
///     response payload.
/// </summary>
/// <param name="IsValid">Whether the payload is valid.</param>
/// <param name="Response">The parsed structured response when valid.</param>
/// <param name="FailureReason">The shared failure reason when invalid.</param>
public readonly record struct StructuredDialogueResponseValidationResult(
    bool IsValid,
    StructuredDialogueTranslationResponse? Response,
    string? FailureReason);
