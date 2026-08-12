// <copyright file="LlmCapabilityParameterDecision.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators.Capabilities;

/// <summary>
///     Describes the resolved policy for one LLM capability parameter.
/// </summary>
/// <param name="SupportState">The resolved support state.</param>
/// <param name="MinValue">The inclusive lower bound, when known.</param>
/// <param name="MaxValue">The inclusive upper bound, when known.</param>
/// <param name="OmitWhenDefaultOnly">
///     Whether the parameter must be omitted when only its implicit default is
///     allowed.
/// </param>
/// <param name="Source">The source that supplied the resolved decision.</param>
/// <param name="Reason">The reason supplied by the resolved decision source.</param>
public readonly record struct LlmCapabilityParameterDecision(
    LlmCapabilitySupportState SupportState,
    float? MinValue,
    float? MaxValue,
    bool OmitWhenDefaultOnly,
    string Source,
    string Reason);
