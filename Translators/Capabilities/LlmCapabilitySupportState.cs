// <copyright file="LlmCapabilitySupportState.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators.Capabilities;

/// <summary>
///     Describes the known support state for an LLM capability parameter.
/// </summary>
public enum LlmCapabilitySupportState
{
  /// <summary>
  ///     Indicates that support has not been established.
  /// </summary>
  Unknown = 0,

  /// <summary>
  ///     Indicates that the parameter is supported.
  /// </summary>
  Supported = 1,

  /// <summary>
  ///     Indicates that the parameter is unsupported.
  /// </summary>
  Unsupported = 2,
}
