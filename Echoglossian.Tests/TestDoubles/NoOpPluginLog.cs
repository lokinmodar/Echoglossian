// <copyright file="NoOpPluginLog.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Plugin.Services;

using Serilog;
using Serilog.Events;

namespace Echoglossian.Tests.TestDoubles;

/// <summary>
///     Minimal no-op plugin logger used by tests that need to construct runtime services.
/// </summary>
internal sealed class NoOpPluginLog : IPluginLog
{
    /// <summary>
    ///     Gets the shared inert Serilog logger.
    /// </summary>
    public ILogger Logger { get; } = new LoggerConfiguration().CreateLogger();

    /// <summary>
    ///     Gets or sets the minimum log level accepted by this logger.
    /// </summary>
    public LogEventLevel MinimumLogLevel { get; set; } = LogEventLevel.Verbose;

    /// <inheritdoc/>
    public void Fatal(string messageTemplate, params object[] values)
    {
    }

    /// <inheritdoc/>
    public void Fatal(Exception? exception, string messageTemplate, params object[] values)
    {
    }

    /// <inheritdoc/>
    public void Error(string messageTemplate, params object[] values)
    {
    }

    /// <inheritdoc/>
    public void Error(Exception? exception, string messageTemplate, params object[] values)
    {
    }

    /// <inheritdoc/>
    public void Warning(string messageTemplate, params object[] values)
    {
    }

    /// <inheritdoc/>
    public void Warning(Exception? exception, string messageTemplate, params object[] values)
    {
    }

    /// <inheritdoc/>
    public void Information(string messageTemplate, params object[] values)
    {
    }

    /// <inheritdoc/>
    public void Information(Exception? exception, string messageTemplate, params object[] values)
    {
    }

    /// <inheritdoc/>
    public void Info(string messageTemplate, params object[] values)
    {
    }

    /// <inheritdoc/>
    public void Info(Exception? exception, string messageTemplate, params object[] values)
    {
    }

    /// <inheritdoc/>
    public void Debug(string messageTemplate, params object[] values)
    {
    }

    /// <inheritdoc/>
    public void Debug(Exception? exception, string messageTemplate, params object[] values)
    {
    }

    /// <inheritdoc/>
    public void Verbose(string messageTemplate, params object[] values)
    {
    }

    /// <inheritdoc/>
    public void Verbose(Exception? exception, string messageTemplate, params object[] values)
    {
    }

    /// <inheritdoc/>
    public void Write(LogEventLevel level, Exception? exception, string messageTemplate, params object[] values)
    {
    }
}
