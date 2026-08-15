// <copyright file="CapturingPluginLog.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Plugin.Services;

using Serilog;
using Serilog.Events;
using System.Globalization;

namespace Echoglossian.Tests.TestDoubles;

/// <summary>
///     Captures plugin logger messages for assertions without emitting runtime
///     diagnostics during tests.
/// </summary>
internal sealed class CapturingPluginLog : IPluginLog
{
    private readonly List<string> debugMessages = [];

    /// <summary>
    ///     Gets captured debug messages.
    /// </summary>
    public IReadOnlyList<string> DebugMessages => this.debugMessages;

    /// <summary>
    ///     Gets the inert Serilog logger required by the interface.
    /// </summary>
    public ILogger Logger { get; } = new LoggerConfiguration().CreateLogger();

    /// <summary>
    ///     Gets or sets the minimum log level accepted by this logger.
    /// </summary>
    public LogEventLevel MinimumLogLevel { get; set; } = LogEventLevel.Verbose;

    /// <inheritdoc/>
    public void Fatal(string messageTemplate, params object[] values) { }

    /// <inheritdoc/>
    public void Fatal(Exception? exception, string messageTemplate, params object[] values) { }

    /// <inheritdoc/>
    public void Error(string messageTemplate, params object[] values) { }

    /// <inheritdoc/>
    public void Error(Exception? exception, string messageTemplate, params object[] values) { }

    /// <inheritdoc/>
    public void Warning(string messageTemplate, params object[] values) { }

    /// <inheritdoc/>
    public void Warning(Exception? exception, string messageTemplate, params object[] values) { }

    /// <inheritdoc/>
    public void Information(string messageTemplate, params object[] values) { }

    /// <inheritdoc/>
    public void Information(Exception? exception, string messageTemplate, params object[] values) { }

    /// <inheritdoc/>
    public void Info(string messageTemplate, params object[] values) { }

    /// <inheritdoc/>
    public void Info(Exception? exception, string messageTemplate, params object[] values) { }

    /// <inheritdoc/>
    public void Debug(string messageTemplate, params object[] values)
    {
        this.debugMessages.Add(this.Render(messageTemplate, values));
    }

    /// <inheritdoc/>
    public void Debug(Exception? exception, string messageTemplate, params object[] values)
    {
        this.debugMessages.Add(this.Render(messageTemplate, values));
    }

    /// <inheritdoc/>
    public void Verbose(string messageTemplate, params object[] values) { }

    /// <inheritdoc/>
    public void Verbose(Exception? exception, string messageTemplate, params object[] values) { }

    /// <inheritdoc/>
    public void Write(LogEventLevel level, Exception? exception, string messageTemplate, params object[] values) { }

    private string Render(string messageTemplate, object[] values)
    {
        if (values.Length == 0)
        {
            return messageTemplate;
        }

        if (this.Logger.BindMessageTemplate(
                messageTemplate,
                values,
                out var parsedTemplate,
                out var properties))
        {
            var propertyMap = properties.ToDictionary(
                property => property.Name,
                property => property.Value,
                StringComparer.Ordinal);
            return parsedTemplate.Render(propertyMap);
        }

        var renderedValues = string.Join(
            ", ",
            values.Select(value => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "<null>"));
        return $"{messageTemplate} | values: {renderedValues}";
    }
}
