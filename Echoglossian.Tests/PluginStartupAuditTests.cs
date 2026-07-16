// <copyright file="PluginStartupAuditTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginRuntime.Startup;

using FluentAssertions;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers the startup-audit helper used by the DalaMock startup rail.
/// </summary>
public class PluginStartupAuditTests
{
    /// <summary>
    /// Verifies duplicate stage writes collapse into a single recorded milestone.
    /// </summary>
    [Fact]
    public void Mark_records_each_stage_once()
    {
        var audit = new PluginStartupAudit();

        audit.Mark(PluginStartupStage.CommandHandlersRegistered);
        audit.Mark(PluginStartupStage.CommandHandlersRegistered);

        var snapshot = audit.CaptureSnapshot();

        snapshot.CompletedStages.Should().ContainSingle()
            .Which.Should().Be(PluginStartupStage.CommandHandlersRegistered);
    }

    /// <summary>
    /// Verifies snapshots do not observe later stage writes.
    /// </summary>
    [Fact]
    public void CaptureSnapshot_returns_an_independent_copy()
    {
        var audit = new PluginStartupAudit();
        audit.Mark(PluginStartupStage.CommandHandlersRegistered);

        var firstSnapshot = audit.CaptureSnapshot();

        audit.Mark(PluginStartupStage.StartupComplete);

        firstSnapshot.HasStage(PluginStartupStage.StartupComplete).Should().BeFalse();
    }
}
