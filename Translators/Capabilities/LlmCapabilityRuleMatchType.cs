// <copyright file="LlmCapabilityRuleMatchType.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators.Capabilities;

/// <summary>
///     Describes how a capability rule identifies an LLM model.
/// </summary>
public enum LlmCapabilityRuleMatchType
{
  /// <summary>
  ///     Matches one complete model identifier.
  /// </summary>
  ExactModel = 0,

  /// <summary>
  ///     Matches model identifiers that begin with a family prefix.
  /// </summary>
  FamilyPrefix = 1,
}
