// <copyright file="LlmTextModel.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators.OpenAI;

public sealed record LlmTextModel(
    string Id,
    string DisplayName,
    bool SupportsText,
    bool SupportsVision,
    bool IsTurbo,
    bool IsMini,
    bool IsDefault = false,
    string EngineName = "",
    string? TierOverride = null);
