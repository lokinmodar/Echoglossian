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

using System.Drawing;

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

    /// <summary>
    /// Ensures capture bounds become ready only after consecutive stable frames.
    /// </summary>
    [Fact]
    public void CaptureStabilityTracker_RequiresThreeConsecutiveStableBounds()
    {
        var tracker = new PreviewCaptureStabilityTracker(
            requiredStableFrames: 3,
            maximumObservationFrames: 6);
        var bounds = new Rectangle(20, 30, 900, 800);
        tracker.Begin(PreviewCaptureTarget.ConfigWindow);

        tracker.Observe(PreviewCaptureTarget.ConfigWindow, bounds);
        tracker.Observe(PreviewCaptureTarget.ConfigWindow, bounds);

        Assert.False(tracker.TryGetStableBounds(
            PreviewCaptureTarget.ConfigWindow,
            out _));

        tracker.Observe(PreviewCaptureTarget.ConfigWindow, bounds);

        Assert.True(tracker.TryGetStableBounds(
            PreviewCaptureTarget.ConfigWindow,
            out var stableBounds));
        Assert.Equal(bounds, stableBounds);
    }

    /// <summary>
    /// Ensures changing bounds resets the consecutive-frame stabilization count.
    /// </summary>
    [Fact]
    public void CaptureStabilityTracker_ChangedBounds_ResetsStability()
    {
        var tracker = new PreviewCaptureStabilityTracker(
            requiredStableFrames: 2,
            maximumObservationFrames: 5);
        var firstBounds = new Rectangle(20, 30, 900, 800);
        var changedBounds = new Rectangle(20, 30, 920, 800);
        tracker.Begin(PreviewCaptureTarget.ConfigWindow);

        tracker.Observe(PreviewCaptureTarget.ConfigWindow, firstBounds);
        tracker.Observe(PreviewCaptureTarget.ConfigWindow, changedBounds);

        Assert.False(tracker.TryGetStableBounds(
            PreviewCaptureTarget.ConfigWindow,
            out _));

        tracker.Observe(PreviewCaptureTarget.ConfigWindow, changedBounds);

        Assert.True(tracker.TryGetStableBounds(
            PreviewCaptureTarget.ConfigWindow,
            out var stableBounds));
        Assert.Equal(changedBounds, stableBounds);
    }

    /// <summary>
    /// Ensures repeatedly missing bounds produce an explicit failed capture state.
    /// </summary>
    [Fact]
    public void CaptureStabilityTracker_MissingBounds_ReachesFailureLimit()
    {
        var tracker = new PreviewCaptureStabilityTracker(
            requiredStableFrames: 2,
            maximumObservationFrames: 3);
        tracker.Begin(PreviewCaptureTarget.ConfigWindow);

        tracker.Observe(PreviewCaptureTarget.ConfigWindow, null);
        tracker.Observe(PreviewCaptureTarget.ConfigWindow, null);
        tracker.Observe(PreviewCaptureTarget.ConfigWindow, null);

        Assert.True(tracker.CaptureFailed);
        Assert.False(tracker.TryGetStableBounds(
            PreviewCaptureTarget.ConfigWindow,
            out _));
    }

    /// <summary>
    /// Ensures ending a capture releases layout while retaining completed bounds for consumption.
    /// </summary>
    [Fact]
    public void CaptureStabilityTracker_End_ReleasesLayoutAndRetainsCompletedBounds()
    {
        var tracker = new PreviewCaptureStabilityTracker(
            requiredStableFrames: 2,
            maximumObservationFrames: 3);
        var bounds = new Rectangle(20, 30, 900, 800);
        tracker.Begin(PreviewCaptureTarget.ConfigWindow);
        tracker.Observe(PreviewCaptureTarget.ConfigWindow, bounds);
        tracker.Observe(PreviewCaptureTarget.ConfigWindow, bounds);

        tracker.End();

        Assert.Null(tracker.Target);
        Assert.False(tracker.CaptureFailed);
        Assert.True(tracker.TryGetStableBounds(
            PreviewCaptureTarget.ConfigWindow,
            out var completedBounds));
        Assert.Equal(bounds, completedBounds);
    }

    /// <summary>
    /// Ensures language and font changes are identified as requiring a preview restart.
    /// </summary>
    [Theory]
    [InlineData(28, 24, false)]
    [InlineData(2, 24, true)]
    [InlineData(28, 31, true)]
    public void GetRuntimeRestartWarning_ReflectsAppliedRuntimeValues(
        int languageId,
        int fontSize,
        bool warningExpected)
    {
        var configuration = new Config
        {
            Lang = languageId,
            FontSize = fontSize,
        };

        var warning = PreviewShell.GetRuntimeRestartWarning(
            configuration,
            appliedLanguageId: 28,
            appliedFontSize: 24);

        Assert.Equal(warningExpected, warning is not null);
        if (warningExpected)
        {
            Assert.Contains("restart", warning, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Ensures preview config windows explicitly disable unavailable imagery.
    /// </summary>
    [Fact]
    public void CreatePreviewPluginWindowContext_UsesUnavailableImageryState()
    {
        var configuration = new Config { Lang = 28 };
        var languages = global::Echoglossian.Echoglossian.CreateLanguagesDictionary();

        var context = Program.CreatePreviewPluginWindowContext(
            configuration,
            languages);

        Assert.False(context.ImagesAvailable);
        Assert.Equal(default, context.LogoTextureHandle);
        Assert.Equal(default, context.PixTextureHandle);
        Assert.Equal(default, context.CryptoTextureHandle);
    }

    /// <summary>
    /// Ensures preview database paths containing semicolons remain a single
    /// SQLite data-source value rather than being interpreted as connection options.
    /// </summary>
    [Fact]
    public void CreatePreviewDbContext_SemicolonInPath_PreservesFullDataSource()
    {
        const string databasePath = @"C:\preview;session\Echoglossian.db";
        var createContext = typeof(Program).GetMethod(
            "CreatePreviewDbContext",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(createContext);

        using var context = Assert.IsType<EchoglossianDbContext>(
            createContext.Invoke(null, [databasePath]));
        var builder = new SqliteConnectionStringBuilder(
            context.Database.GetDbConnection().ConnectionString);

        Assert.Equal(databasePath, builder.DataSource);
        Assert.False(builder.Pooling);
    }
}
