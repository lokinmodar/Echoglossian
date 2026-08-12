// <copyright file="LlmCapabilityScope.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators.Capabilities;

/// <summary>
///     Identifies the active engine, provider, endpoint, and model for a
///     capability lookup.
/// </summary>
/// <param name="Engine">The active translation engine.</param>
/// <param name="ProviderScope">The provider identity within the engine.</param>
/// <param name="EndpointScope">The endpoint identity within the provider.</param>
/// <param name="ModelId">The active model identifier.</param>
public readonly record struct LlmCapabilityScope(
    Echoglossian.TransEngines Engine,
    string ProviderScope,
    string EndpointScope,
    string ModelId);
