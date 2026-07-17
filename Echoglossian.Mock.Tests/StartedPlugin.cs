// <copyright file="StartedPlugin.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using DalaMock.Core.Plugin;

namespace Echoglossian.Mock.Tests;

/// <summary>
/// Represents a started DalaMock plugin instance and its owning mock container.
/// </summary>
internal sealed record StartedPlugin(
    MockContainer Container,
    global::Echoglossian.Echoglossian Plugin);
