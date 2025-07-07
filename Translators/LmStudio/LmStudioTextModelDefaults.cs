// <copyright file="LmStudioTextModelDefaults.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.OpenAI;

namespace Echoglossian.Translators.LmStudio;

/// <summary>
/// Provides default model list for LM Studio usage.
/// </summary>
public static class LmStudioTextModelDefaults
{
  /// <summary>
  /// Predefined LM Studio-compatible models for offline use.
  /// </summary>
  public static readonly List<OpenAITextModel> PredefinedModels = new()
  {
    new OpenAITextModel(
      Id: "lmstudio/llama3",
      DisplayName: "🦙 LLaMA 3",
      SupportsText: true,
      SupportsVision: false,
      IsTurbo: false,
      IsMini: false,
      IsDefault: true,
      EngineName: "LmStudio"),
  };
}
