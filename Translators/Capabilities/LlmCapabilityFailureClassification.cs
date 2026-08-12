// <copyright file="LlmCapabilityFailureClassification.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators.Capabilities;

/// <summary>
///     Describes the safe, bounded interpretation of a provider failure.
/// </summary>
/// <param name="ObservationRecorded">Whether the failure is eligible for an audit observation.</param>
/// <param name="RulePromoted">Whether the failure is eligible for exact-model rule promotion.</param>
/// <param name="FailureKind">The bounded failure classification.</param>
/// <param name="ProviderErrorCode">The sanitized provider error code.</param>
/// <param name="MessageExcerpt">The sanitized provider error excerpt.</param>
public readonly record struct LlmCapabilityFailureClassification(
    bool ObservationRecorded,
    bool RulePromoted,
    string FailureKind,
    string ProviderErrorCode,
    string MessageExcerpt);
