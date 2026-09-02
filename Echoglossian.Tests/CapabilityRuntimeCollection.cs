// <copyright file="CapabilityRuntimeCollection.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Xunit;

namespace Echoglossian.Tests;

/// <summary>Serializes tests that own static capability runtime state.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CapabilityRuntimeCollection
{
    /// <summary>The shared collection name.</summary>
    public const string Name = "Capability runtime";
}
