// <copyright file="PluginRuntimeLogFileTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Tests.TestDoubles;

using System.Text;

using Xunit;

using PluginEntry = Echoglossian.Echoglossian;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the dedicated Echoglossian runtime log mirror that writes
///     <see cref="PluginRuntimeLog" /> output into an exclusive file.
/// </summary>
public sealed class PluginRuntimeLogFileTests : IDisposable
{
    private readonly string configDirectory;
    private readonly NoOpPluginLog pluginLog = new();

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="PluginRuntimeLogFileTests" /> class.
    /// </summary>
    public PluginRuntimeLogFileTests()
    {
        this.configDirectory = CreateTempConfigDirectory();
        PluginEntry.ConfigDirectory = this.configDirectory;
        PluginEntry.PluginLog = this.pluginLog;
        PluginRuntimeFileLog.ResetForTests();
        PluginRuntimeFileLog.SetMaxLineCountForTests(200);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        PluginRuntimeFileLog.ResetForTests();

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
    ///     Ensures a basic informational log line is mirrored into the
    ///     dedicated runtime log file.
    /// </summary>
    [Fact]
    public void Information_WritesLineToExclusiveRuntimeLog()
    {
        Assert.Equal(
            Path.Combine(this.configDirectory, "Echoglossian.log"),
            PluginRuntimeFileLog.GetCurrentFilePathForTests());

        PluginRuntimeLog.Information("Quest prefetch started.");

        var content = ReadAllTextShared(
            PluginRuntimeFileLog.GetCurrentFilePathForTests());

        Assert.Contains("[INF] Quest prefetch started.", content);
    }

    /// <summary>
    ///     Ensures scoped structured debug lines are rendered with values in
    ///     the dedicated runtime log file.
    /// </summary>
    [Fact]
    public void Debug_StructuredScopedMessage_RendersValuesIntoExclusiveRuntimeLog()
    {
        PluginRuntimeLog.Debug(
            "QuestRuntime",
            "phase={Phase} quest={QuestId}",
            "request-queued",
            67011);

        var content = ReadAllTextShared(
            PluginRuntimeFileLog.GetCurrentFilePathForTests());

        Assert.Contains("[DBG] [QuestRuntime]", content);
        Assert.Contains("request-queued", content);
        Assert.Contains("67011", content);
    }

    /// <summary>
    ///     Ensures warning entries that include exceptions are mirrored into
    ///     the dedicated runtime log file with the exception details.
    /// </summary>
    [Fact]
    public void Warning_WithException_WritesMessageAndExceptionToExclusiveRuntimeLog()
    {
        var exception = new InvalidOperationException("duplicate queue");

        PluginRuntimeLog.Warning(exception, "Quest prefetch warning.");

        var content = ReadAllTextShared(
            PluginRuntimeFileLog.GetCurrentFilePathForTests());

        Assert.Contains("[WRN] Quest prefetch warning.", content);
        Assert.Contains("InvalidOperationException: duplicate queue", content);
    }

    /// <summary>
    ///     Ensures the active runtime log rotates to a timestamped archive when
    ///     the line cap is exceeded.
    /// </summary>
    [Fact]
    public void Information_RotatesActiveRuntimeLogWhenLineCapIsExceeded()
    {
        PluginRuntimeFileLog.SetMaxLineCountForTests(2);

        PluginRuntimeLog.Information("line one");
        PluginRuntimeLog.Information("line two");
        PluginRuntimeLog.Information("line three");

        var activeLogPath = PluginRuntimeFileLog.GetCurrentFilePathForTests();
        var activeContent = ReadAllTextShared(activeLogPath);
        var archivePaths = Directory.GetFiles(
            Path.GetDirectoryName(activeLogPath)!,
            "Echoglossian-*.log");

        Assert.Single(archivePaths);
        Assert.Contains("[INF] line three", activeContent);
        Assert.DoesNotContain("[INF] line one", activeContent);

        var archiveContent = ReadAllTextShared(archivePaths[0]);
        Assert.Contains("[INF] line one", archiveContent);
        Assert.Contains("[INF] line two", archiveContent);
    }

    /// <summary>
    ///     Ensures a previously oversized dedicated runtime log rotates before
    ///     the next write after the plugin starts.
    /// </summary>
    [Fact]
    public void Information_RotatesExistingOversizedRuntimeLogBeforeAppend()
    {
        PluginRuntimeFileLog.SetMaxLineCountForTests(2);

        var activeLogPath = Path.Combine(
            this.configDirectory,
            "Echoglossian.log");
        File.WriteAllLines(
            activeLogPath,
            [
                "legacy-one",
                "legacy-two",
                "legacy-three",
            ]);

        PluginRuntimeLog.Information("fresh line");

        var activeContent = ReadAllTextShared(activeLogPath);
        var archivePaths = Directory.GetFiles(
            Path.GetDirectoryName(activeLogPath)!,
            "Echoglossian-*.log");

        Assert.Single(archivePaths);
        Assert.Contains("[INF] fresh line", activeContent);
        Assert.DoesNotContain("legacy-one", activeContent);

        var archiveContent = ReadAllTextShared(archivePaths[0]);
        Assert.Contains("legacy-one", archiveContent);
        Assert.Contains("legacy-three", archiveContent);
    }

    private static string ReadAllTextShared(string filePath)
    {
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
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
