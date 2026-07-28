// <copyright file="DiagnosticFileEmitterTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Xunit;

using PluginEntry = Echoglossian.Echoglossian;

namespace Echoglossian.Tests;

/// <summary>
///     Covers diagnostic dump files that should rotate by line count instead of
///     growing without bound.
/// </summary>
public sealed class DiagnosticFileEmitterTests : IDisposable
{
    private readonly string configDirectory;

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="DiagnosticFileEmitterTests" /> class.
    /// </summary>
    public DiagnosticFileEmitterTests()
    {
        this.configDirectory = CreateTempConfigDirectory();
        PluginEntry.ConfigDirectory = this.configDirectory;
        DiagnosticFileEmitter.ResetForTests();
        DiagnosticFileEmitter.SetMaxLineCountForTests(4);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DiagnosticFileEmitter.FlushForTests();
        DiagnosticFileEmitter.ResetForTests();

        try
        {
            if (Directory.Exists(this.configDirectory))
            {
                Directory.Delete(this.configDirectory, recursive: true);
            }
        }
        catch
        {
            // Test cleanup should not mask assertion failures.
        }
    }

    /// <summary>
    ///     Ensures purpose-specific diagnostic logs rotate to timestamped
    ///     archives when the active file reaches the line cap.
    /// </summary>
    [Fact]
    public void Emit_RotatesPurposeSpecificDiagnosticLogsWhenLineCapIsExceeded()
    {
        DiagnosticFileEmitter.Emit(
            "accepted-quest-prefetch-activity",
            "quest-one",
            "phase=queue");
        DiagnosticFileEmitter.Emit(
            "accepted-quest-prefetch-activity",
            "quest-two",
            "phase=translated");
        DiagnosticFileEmitter.FlushForTests();

        var activeLogPath = Path.Combine(
            this.configDirectory,
            "accepted-quest-prefetch-activity.log");
        var archivePaths = Directory.GetFiles(
            this.configDirectory,
            "accepted-quest-prefetch-activity-*.log");

        Assert.Single(archivePaths);

        var activeContent = File.ReadAllText(activeLogPath);
        Assert.Contains("quest-two", activeContent);
        Assert.DoesNotContain("quest-one", activeContent);

        var archiveContent = File.ReadAllText(archivePaths[0]);
        Assert.Contains("quest-one", archiveContent);
        Assert.DoesNotContain("quest-two", archiveContent);
    }

    /// <summary>
    ///     Ensures a previously oversized accepted-quest diagnostic file is
    ///     rotated on the next emit instead of remaining the active file.
    /// </summary>
    [Fact]
    public void Emit_RotatesExistingOversizedAcceptedQuestDiagnosticLogBeforeAppend()
    {
        var activeLogPath = Path.Combine(
            this.configDirectory,
            "accepted-quest-prefetch-canonical.log");
        File.WriteAllLines(
            activeLogPath,
            [
                "legacy-one",
                "legacy-two",
                "legacy-three",
                "legacy-four",
                "legacy-five",
            ]);

        DiagnosticFileEmitter.Emit(
            "accepted-quest-prefetch-canonical",
            "quest-three",
            "phase=resolved");
        DiagnosticFileEmitter.FlushForTests();

        var archivePaths = Directory.GetFiles(
            this.configDirectory,
            "accepted-quest-prefetch-canonical-*.log");

        Assert.Single(archivePaths);

        var activeContent = File.ReadAllText(activeLogPath);
        Assert.Contains("quest-three", activeContent);
        Assert.DoesNotContain("legacy-one", activeContent);

        var archiveContent = File.ReadAllText(archivePaths[0]);
        Assert.Contains("legacy-one", archiveContent);
        Assert.Contains("legacy-five", archiveContent);
    }

    /// <summary>
    ///     Ensures queued diagnostic blocks are flushed before shutdown returns
    ///     so plugin unload does not drop the tail of purpose-specific dumps.
    /// </summary>
    [Fact]
    public void Shutdown_FlushesQueuedDiagnosticBlocksBeforeReturning()
    {
        DiagnosticFileEmitter.Emit(
            "accepted-quest-prefetch-activity",
            "quest-four",
            "phase=shutdown");

        DiagnosticFileEmitter.Shutdown();

        var activeLogPath = Path.Combine(
            this.configDirectory,
            "accepted-quest-prefetch-activity.log");
        var activeContent = File.ReadAllText(activeLogPath);

        Assert.Contains("quest-four", activeContent);
        Assert.Contains("phase=shutdown", activeContent);
    }

    private static string CreateTempConfigDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
