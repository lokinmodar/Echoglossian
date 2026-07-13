// <copyright file="SourceClientLanguage.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
///     Represents a source client language as separate persistence and
///     translation-provider identities.
/// </summary>
/// <param name="PersistenceCode">The code persisted with translated rows.</param>
/// <param name="ProviderCode">The code supplied to translation providers.</param>
public readonly record struct SourceClientLanguage(
    string PersistenceCode,
    string ProviderCode);
