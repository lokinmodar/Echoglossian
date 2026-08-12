// <copyright file="LlmCapabilityResolver.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators.Capabilities;

/// <summary>
///     Resolves static defaults and persisted overlays into one capability
///     snapshot.
/// </summary>
public static class LlmCapabilityResolver
{
  /// <summary>
  ///     Resolves the effective capability policy for a scope.
  /// </summary>
  /// <param name="scope">The active capability lookup scope.</param>
  /// <param name="overlayRules">The persisted capability overlay rules.</param>
  /// <returns>The resolved capability snapshot.</returns>
  public static LlmCapabilitySnapshot Resolve(
      LlmCapabilityScope scope,
      IReadOnlyList<LlmCapabilityRuleDefinition> overlayRules)
  {
    var definitions = LlmCapabilityStaticCatalog.GetDefinitions(scope.Engine);
    return LlmCapabilitySnapshot.Create(scope, definitions, overlayRules);
  }
}
