// <copyright file="StructuredDialogueCapabilityHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators.Helpers;

/// <summary>
///     Resolves the current best-effort structured dialogue capability for one
///     configured translator family.
/// </summary>
public static class StructuredDialogueCapabilityHelper
{
  /// <summary>
  ///     Gets the preferred structured dialogue capability for the specified
  ///     engine family.
  /// </summary>
  /// <param name="engine">The effective translation engine.</param>
  /// <returns>The preferred structured dialogue capability.</returns>
  public static StructuredDialogueProviderCapability GetPreferredCapability(
      Echoglossian.TransEngines engine)
  {
    return engine switch
    {
      Echoglossian.TransEngines.ChatGPT => StructuredDialogueProviderCapability.JsonSchema,
      Echoglossian.TransEngines.OpenRouter => StructuredDialogueProviderCapability.JsonSchema,
      Echoglossian.TransEngines.DeepSeek => StructuredDialogueProviderCapability.JsonSchema,
      Echoglossian.TransEngines.LmStudio => StructuredDialogueProviderCapability.JsonSchema,
      Echoglossian.TransEngines.Ollama => StructuredDialogueProviderCapability.JsonSchema,
      Echoglossian.TransEngines.Gemini => StructuredDialogueProviderCapability.JsonSchema,
      Echoglossian.TransEngines.Claude => StructuredDialogueProviderCapability.Disabled,
      _ => StructuredDialogueProviderCapability.Disabled,
    };
  }
}
