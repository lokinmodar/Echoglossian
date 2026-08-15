// <copyright file="LlmCapabilityParameterName.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators.Capabilities;

/// <summary>
///     Identifies an LLM request parameter governed by the capability matrix.
/// </summary>
public enum LlmCapabilityParameterName
{
  /// <summary>
  ///     Identifies the temperature parameter.
  /// </summary>
  Temperature = 0,

  /// <summary>
  ///     Identifies the top-p parameter.
  /// </summary>
  TopP = 1,

  /// <summary>
  ///     Identifies the top-k parameter.
  /// </summary>
  TopK = 2,

  /// <summary>
  ///     Identifies the presence-penalty parameter.
  /// </summary>
  PresencePenalty = 3,

  /// <summary>
  ///     Identifies the frequency-penalty parameter.
  /// </summary>
  FrequencyPenalty = 4,

  /// <summary>
  ///     Identifies the reasoning-effort parameter.
  /// </summary>
  ReasoningEffort = 5,

  /// <summary>
  ///     Identifies structured tool calling support.
  /// </summary>
  StructuredToolCalling = 6,
}
