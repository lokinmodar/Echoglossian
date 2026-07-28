// <copyright file="PluginRuntimeFileLog.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
///     Mirrors plugin runtime logs into an Echoglossian-exclusive file so
///     runtime investigations can read a focused log stream outside
///     <c>dalamud.log</c>.
/// </summary>
internal static class PluginRuntimeFileLog
{
    private const string ActiveLogFileName = "Echoglossian.log";
    private static readonly TimeSpan FlushTimeout = TimeSpan.FromSeconds(5);

    private static readonly Lock SyncRoot = new();
    private static AsyncSerialActionPump actionPump = new();

    private static StreamWriter? writer;
    private static string? currentConfigDirectory;
    private static string? currentLogFilePath;
    private static long currentLineCount;
    private static int? maxLineCountOverride;

    /// <summary>
    ///     Writes a formatted runtime log line into the dedicated plugin log
    ///     file.
    /// </summary>
    /// <param name="level">The runtime log level.</param>
    /// <param name="message">The already-rendered message.</param>
    internal static void Write(
        PluginRuntimeLogLevel level,
        string message)
    {
        try
        {
            var configDirectory = Echoglossian.ConfigDirectory;
            if (string.IsNullOrWhiteSpace(message) ||
                string.IsNullOrWhiteSpace(configDirectory))
            {
                return;
            }

            var timestamp = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture);
            var levelTag = GetLevelTag(level);
            var renderedLines = RotatingLogFileSupport
                .EnumerateLines(message)
                .Select(line => $"{timestamp} [{levelTag}] {line}")
                .ToArray();

            actionPump.Enqueue(() => WriteRenderedLines(configDirectory, renderedLines));
        }
        catch
        {
            // Logging must never throw back into the plugin runtime.
        }
    }

    /// <summary>
    ///     Releases the active writer and clears static state for isolated
    ///     tests.
    /// </summary>
    internal static void ResetForTests()
    {
        var previousPump = actionPump;
        actionPump = new AsyncSerialActionPump();
        previousPump.Dispose();

        lock (SyncRoot)
        {
            ResetWriterStateNoLock();
            maxLineCountOverride = null;
        }
    }

    /// <summary>
    ///     Overrides the line-count rotation threshold for isolated tests.
    /// </summary>
    /// <param name="maxLineCount">The maximum active-file line count.</param>
    internal static void SetMaxLineCountForTests(int maxLineCount)
    {
        lock (SyncRoot)
        {
            maxLineCountOverride = maxLineCount > 0
                ? maxLineCount
                : RotatingLogFileSupport.DefaultMaxLineCount;
        }
    }

    /// <summary>
    ///     Gets the current runtime log file path for tests and diagnostics.
    /// </summary>
    /// <returns>The absolute path of the active runtime log file.</returns>
    internal static string GetCurrentFilePathForTests()
    {
        lock (SyncRoot)
        {
            return currentLogFilePath ??
                   BuildActiveLogPath(Echoglossian.ConfigDirectory);
        }
    }

    /// <summary>
    ///     Waits for the background runtime log writer to flush queued lines.
    /// </summary>
    internal static void FlushForTests()
    {
        if (!actionPump.Flush(FlushTimeout))
        {
            throw new TimeoutException("Timed out while flushing PluginRuntimeFileLog.");
        }
    }

    /// <summary>
    ///     Stops accepting new runtime log writes and drains the pending queue.
    /// </summary>
    internal static void Shutdown()
    {
        _ = actionPump.Shutdown(FlushTimeout);

        lock (SyncRoot)
        {
            ResetWriterStateNoLock();
        }
    }

    private static void WriteRenderedLines(
        string configDirectory,
        string[] renderedLines)
    {
        lock (SyncRoot)
        {
            EnsureWriter(configDirectory);
            if (writer == null)
            {
                return;
            }

            RotateIfCurrentFileWouldOverflow(renderedLines.Length);
            foreach (var line in renderedLines)
            {
                writer.WriteLine(line);
            }

            writer.Flush();
            currentLineCount += renderedLines.Length;
        }
    }

    private static void EnsureWriter(string configDirectory)
    {
        if (writer != null &&
            string.Equals(
                currentConfigDirectory,
                configDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        writer?.Dispose();
        OpenWriter(configDirectory);
    }

    private static string BuildActiveLogPath(string? configDirectory)
    {
        if (string.IsNullOrWhiteSpace(configDirectory))
        {
            return string.Empty;
        }

        return Path.Combine(
            configDirectory,
            ActiveLogFileName);
    }

    private static void OpenWriter(string configDirectory)
    {
        Directory.CreateDirectory(configDirectory);

        var activeLogFilePath = Path.Combine(configDirectory, ActiveLogFileName);
        var maxLineCount = GetMaxLineCount();
        currentLineCount = RotatingLogFileSupport.CountExistingLinesUpTo(
            activeLogFilePath,
            maxLineCount);
        if (currentLineCount >= maxLineCount)
        {
            RotatingLogFileSupport.RotateActiveFile(activeLogFilePath);
            currentLineCount = 0;
        }

        writer = new StreamWriter(
            new FileStream(
                activeLogFilePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite),
            new UTF8Encoding(false));
        currentConfigDirectory = configDirectory;
        currentLogFilePath = activeLogFilePath;
    }

    private static void RotateIfCurrentFileWouldOverflow(int incomingLineCount)
    {
        if (writer == null ||
            string.IsNullOrWhiteSpace(currentLogFilePath) ||
            string.IsNullOrWhiteSpace(currentConfigDirectory))
        {
            return;
        }

        var maxLineCount = GetMaxLineCount();
        if (currentLineCount < maxLineCount &&
            (currentLineCount == 0 ||
             currentLineCount + incomingLineCount <= maxLineCount))
        {
            return;
        }

        writer.Dispose();
        writer = null;
        RotatingLogFileSupport.RotateActiveFile(currentLogFilePath);
        currentLineCount = 0;
        OpenWriter(currentConfigDirectory);
    }

    private static string GetLevelTag(PluginRuntimeLogLevel level)
    {
        return level switch
        {
            PluginRuntimeLogLevel.Debug => "DBG",
            PluginRuntimeLogLevel.Verbose => "VRB",
            PluginRuntimeLogLevel.Information => "INF",
            PluginRuntimeLogLevel.Warning => "WRN",
            PluginRuntimeLogLevel.Error => "ERR",
            _ => "INF",
        };
    }

    private static int GetMaxLineCount()
    {
        return maxLineCountOverride ?? RotatingLogFileSupport.DefaultMaxLineCount;
    }

    private static void ResetWriterStateNoLock()
    {
        writer?.Dispose();
        writer = null;
        currentConfigDirectory = null;
        currentLogFilePath = null;
        currentLineCount = 0;
    }
}
