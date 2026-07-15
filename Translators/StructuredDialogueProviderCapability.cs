// <copyright file="StructuredDialogueProviderCapability.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators;

/// <summary>
///     Describes the best-effort structured dialogue mode a provider family
///     can attempt without forcing every engine into the same request shape.
/// </summary>
public enum StructuredDialogueProviderCapability
{
  /// <summary>
  ///     Structured dialogue mode is disabled for this provider family.
  /// </summary>
  Disabled = 0,

  /// <summary>
  ///     The provider can attempt strict JSON-schema style structured output.
  /// </summary>
  JsonSchema = 1,

  /// <summary>
  ///     The provider can attempt looser JSON-object style structured output.
  /// </summary>
  JsonObject = 2,

  /// <summary>
  ///     The provider should remain plain-text but can still receive glossary
  ///     hints in a non-JSON format.
  /// </summary>
  PlainTextGlossary = 3,
}
