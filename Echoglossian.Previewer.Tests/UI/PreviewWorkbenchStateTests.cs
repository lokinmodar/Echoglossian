// <copyright file="PreviewWorkbenchStateTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.DBManagerUI;
using Echoglossian.EFCoreSqlite;
using Echoglossian.Previewer.Scenarios;
using Echoglossian.Previewer.UI;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Echoglossian.Previewer.Tests.UI;

/// <summary>
/// Covers unified preview workbench state defaults.
/// </summary>
public sealed class PreviewWorkbenchStateTests
{
    /// <summary>
    /// Ensures a new workbench starts with only the overlay visible.
    /// </summary>
    [Fact]
    public void CreateDefault_EnablesOverlayAndKeepsPluginWindowsClosed()
    {
        var state = PreviewWorkbenchState.CreateDefault(
            PreviewScenarioCatalog.Defaults[0],
            PreviewScenarioCatalog.ViewportPresets[1]);

        Assert.True(state.OverlayVisible);
        Assert.False(state.ConfigWindowOpen);
        Assert.False(state.DbManagerWindowOpen);
        Assert.False(state.TranslatorMetricsWindowOpen);
        Assert.Equal(PreviewCaptureTarget.FullFrame, state.CaptureTarget);
    }

    /// <summary>
    /// Ensures preview DB row loads can replace the live notification service.
    /// </summary>
    [Fact]
    public void DbEditorWindow_OpenAndSelectTable_UsesHostNotificationAdapter()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<EchoglossianDbContext>()
            .UseSqlite(connection)
            .Options;
        using var dbContext = new EchoglossianDbContext(options);
        dbContext.Database.EnsureCreated();
        var notificationCount = 0;
        var window = new DbEditorWindow(
            dbContext,
            _ => notificationCount++);

        window.OpenAndSelectTable("TalkMessage");

        Assert.Equal(1, notificationCount);
    }

    /// <summary>
    /// Ensures a DB window opened by another plugin window is copied back to workbench state.
    /// </summary>
    [Fact]
    public void SynchronizeDbManagerState_AfterMetricsOpen_PreservesOpenRequest()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<EchoglossianDbContext>()
            .UseSqlite(connection)
            .Options;
        using var dbContext = new EchoglossianDbContext(options);
        var window = new DbEditorWindow(dbContext, static _ => { })
        {
            IsOpen = true,
        };
        var state = PreviewWorkbenchState.CreateDefault(
            PreviewScenarioCatalog.Defaults[0],
            PreviewScenarioCatalog.ViewportPresets[1]);

        PreviewPluginWindowHost.SynchronizeDbManagerState(state, window);

        Assert.True(state.DbManagerWindowOpen);
    }
}
