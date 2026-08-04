// <copyright file="PluginRuntimeLog.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
///     Supported plugin log levels for shared logging helpers.
/// </summary>
internal enum PluginRuntimeLogLevel
{
  /// <summary>Debug-only diagnostics.</summary>
  Debug,

  /// <summary>Low-priority runtime detail.</summary>
  Verbose,

  /// <summary>General informational message.</summary>
  Information,

  /// <summary>Warning that does not abort the current flow.</summary>
  Warning,

  /// <summary>Error condition.</summary>
  Error,
}

/// <summary>
///     Wraps plugin logging so debug diagnostics compile away from release
///     builds while also centralizing optional <c>[Scope]</c> formatting.
/// </summary>
internal static class PluginRuntimeLog
{
  /// <summary>
  ///     Writes a debug log line only in <c>DEBUG</c> builds.
  /// </summary>
  /// <param name="pluginLog">The explicit logger sink to use.</param>
  /// <param name="message">The message to write.</param>
  [Conditional("DEBUG")]
  public static void Debug(IPluginLog pluginLog, string message)
  {
    WriteDirect(pluginLog, PluginRuntimeLogLevel.Debug, message);
  }

  /// <summary>
  ///     Writes a formatted debug log line only in <c>DEBUG</c> builds.
  /// </summary>
  /// <param name="pluginLog">The explicit logger sink to use.</param>
  /// <param name="messageTemplate">The structured message template.</param>
  /// <param name="values">The structured logging values.</param>
  [Conditional("DEBUG")]
  public static void Debug(
      IPluginLog pluginLog,
      string messageTemplate,
      params object[] values)
  {
    WriteStructured(
        pluginLog,
        PluginRuntimeLogLevel.Debug,
        messageTemplate,
        values);
  }

  /// <summary>
  ///     Writes a debug log line only in <c>DEBUG</c> builds.
  /// </summary>
  /// <param name="message">The message to write.</param>
  [Conditional("DEBUG")]
  public static void Debug(string message)
  {
    var pluginLog = Echoglossian.PluginLog;
    if (pluginLog == null)
    {
      PluginRuntimeFileLog.Write(PluginRuntimeLogLevel.Debug, message);
      return;
    }

    WriteDirect(pluginLog, PluginRuntimeLogLevel.Debug, message);
  }

  /// <summary>
  ///     Writes a formatted debug log line only in <c>DEBUG</c> builds.
  /// </summary>
  /// <param name="messageTemplate">The structured message template.</param>
  /// <param name="values">The structured logging values.</param>
  [Conditional("DEBUG")]
  public static void Debug(string messageTemplate, params object[] values)
  {
    var pluginLog = Echoglossian.PluginLog;
    if (pluginLog == null)
    {
      PluginRuntimeFileLog.Write(
          PluginRuntimeLogLevel.Debug,
          RenderStructuredMessage(null, messageTemplate, values));
      return;
    }

    WriteStructured(
        pluginLog,
        PluginRuntimeLogLevel.Debug,
        messageTemplate,
        values);
  }

  /// <summary>
  ///     Writes a scoped debug log line only in <c>DEBUG</c> builds.
  /// </summary>
  /// <param name="scope">The logical scope to render inside square brackets.</param>
  /// <param name="message">The message to write.</param>
  [Conditional("DEBUG")]
  public static void Debug(string scope, string message)
  {
    var pluginLog = Echoglossian.PluginLog;
    var formatted = Format(scope, message);
    if (pluginLog == null)
    {
      PluginRuntimeFileLog.Write(PluginRuntimeLogLevel.Debug, formatted);
      return;
    }

    WriteDirect(pluginLog, PluginRuntimeLogLevel.Debug, formatted);
  }

  /// <summary>
  ///     Writes a scoped formatted debug log line only in <c>DEBUG</c> builds.
  /// </summary>
  /// <param name="scope">The logical scope to render inside square brackets.</param>
  /// <param name="messageTemplate">The structured message template.</param>
  /// <param name="values">The structured logging values.</param>
  [Conditional("DEBUG")]
  public static void Debug(
      string scope,
      string messageTemplate,
      params object[] values)
  {
    var pluginLog = Echoglossian.PluginLog;
    var formattedTemplate = Format(scope, messageTemplate);
    if (pluginLog == null)
    {
      PluginRuntimeFileLog.Write(
          PluginRuntimeLogLevel.Debug,
          RenderStructuredMessage(null, formattedTemplate, values));
      return;
    }

    WriteStructured(
        pluginLog,
        PluginRuntimeLogLevel.Debug,
        formattedTemplate,
        values);
  }

  /// <summary>
  ///     Writes a scoped message using the requested level.
  /// </summary>
  /// <param name="level">The log level to use.</param>
  /// <param name="scope">The logical scope to render inside square brackets.</param>
  /// <param name="message">The message to write.</param>
  public static void Write(
      PluginRuntimeLogLevel level,
      string scope,
      string message)
  {
    var pluginLog = Echoglossian.PluginLog;
    var formatted = Format(scope, message);
    if (pluginLog == null)
    {
      PluginRuntimeFileLog.Write(level, formatted);
      return;
    }

    WriteDirect(pluginLog, level, formatted);
  }

  /// <summary>
  ///     Writes a message using the requested level.
  /// </summary>
  /// <param name="level">The log level to use.</param>
  /// <param name="message">The message to write.</param>
  public static void Write(
      PluginRuntimeLogLevel level,
      string message)
  {
    var pluginLog = Echoglossian.PluginLog;
    if (pluginLog == null)
    {
      PluginRuntimeFileLog.Write(level, message);
      return;
    }

    WriteDirect(pluginLog, level, message);
  }

  /// <summary>
  ///     Writes a message using the requested level and explicit logger sink.
  /// </summary>
  /// <param name="pluginLog">The explicit logger sink to use.</param>
  /// <param name="level">The log level to use.</param>
  /// <param name="message">The message to write.</param>
  public static void Write(
      IPluginLog pluginLog,
      PluginRuntimeLogLevel level,
      string message)
  {
    Write(pluginLog, level, string.Empty, message);
  }

  /// <summary>
  ///     Writes a scoped message using the requested level and explicit logger sink.
  /// </summary>
  /// <param name="pluginLog">The explicit logger sink to use.</param>
  /// <param name="level">The log level to use.</param>
  /// <param name="scope">The logical scope to render inside square brackets.</param>
  /// <param name="message">The message to write.</param>
  public static void Write(
      IPluginLog pluginLog,
      PluginRuntimeLogLevel level,
      string scope,
      string message)
  {
    var formatted = Format(scope, message);
    WriteDirect(pluginLog, level, formatted);
  }

  /// <summary>
  ///     Writes a verbose log line.
  /// </summary>
  /// <param name="message">The message to write.</param>
  public static void Verbose(string message)
  {
    var pluginLog = Echoglossian.PluginLog;
    if (pluginLog == null)
    {
      PluginRuntimeFileLog.Write(PluginRuntimeLogLevel.Verbose, message);
      return;
    }

    WriteDirect(pluginLog, PluginRuntimeLogLevel.Verbose, message);
  }

  /// <summary>
  ///     Writes a verbose log line to an explicit logger sink.
  /// </summary>
  /// <param name="pluginLog">The explicit logger sink to use.</param>
  /// <param name="message">The message to write.</param>
  public static void Verbose(IPluginLog pluginLog, string message)
  {
    WriteDirect(pluginLog, PluginRuntimeLogLevel.Verbose, message);
  }

  /// <summary>
  ///     Writes a scoped verbose log line.
  /// </summary>
  /// <param name="scope">The logical scope to render inside square brackets.</param>
  /// <param name="message">The message to write.</param>
  public static void Verbose(string scope, string message)
  {
    var pluginLog = Echoglossian.PluginLog;
    var formatted = Format(scope, message);
    if (pluginLog == null)
    {
      PluginRuntimeFileLog.Write(PluginRuntimeLogLevel.Verbose, formatted);
      return;
    }

    WriteDirect(pluginLog, PluginRuntimeLogLevel.Verbose, formatted);
  }

  /// <summary>
  ///     Writes an informational log line.
  /// </summary>
  /// <param name="message">The message to write.</param>
  public static void Information(string message)
  {
    var pluginLog = Echoglossian.PluginLog;
    if (pluginLog == null)
    {
      PluginRuntimeFileLog.Write(PluginRuntimeLogLevel.Information, message);
      return;
    }

    WriteDirect(pluginLog, PluginRuntimeLogLevel.Information, message);
  }

  /// <summary>
  ///     Writes an informational log line to an explicit logger sink.
  /// </summary>
  /// <param name="pluginLog">The explicit logger sink to use.</param>
  /// <param name="message">The message to write.</param>
  public static void Information(IPluginLog pluginLog, string message)
  {
    WriteDirect(pluginLog, PluginRuntimeLogLevel.Information, message);
  }

  /// <summary>
  ///     Writes a scoped informational log line.
  /// </summary>
  /// <param name="scope">The logical scope to render inside square brackets.</param>
  /// <param name="message">The message to write.</param>
  public static void Information(string scope, string message)
  {
    var pluginLog = Echoglossian.PluginLog;
    var formatted = Format(scope, message);
    if (pluginLog == null)
    {
      PluginRuntimeFileLog.Write(PluginRuntimeLogLevel.Information, formatted);
      return;
    }

    WriteDirect(pluginLog, PluginRuntimeLogLevel.Information, formatted);
  }

  /// <summary>
  ///     Writes a warning log line.
  /// </summary>
  /// <param name="message">The message to write.</param>
  public static void Warning(string message)
  {
    var pluginLog = Echoglossian.PluginLog;
    if (pluginLog == null)
    {
      PluginRuntimeFileLog.Write(PluginRuntimeLogLevel.Warning, message);
      return;
    }

    WriteDirect(pluginLog, PluginRuntimeLogLevel.Warning, message);
  }

  /// <summary>
  ///     Writes a warning log line to an explicit logger sink.
  /// </summary>
  /// <param name="pluginLog">The explicit logger sink to use.</param>
  /// <param name="message">The message to write.</param>
  public static void Warning(IPluginLog pluginLog, string message)
  {
    WriteDirect(pluginLog, PluginRuntimeLogLevel.Warning, message);
  }

  /// <summary>
  ///     Writes a scoped warning log line.
  /// </summary>
  /// <param name="scope">The logical scope to render inside square brackets.</param>
  /// <param name="message">The message to write.</param>
  public static void Warning(string scope, string message)
  {
    var pluginLog = Echoglossian.PluginLog;
    var formatted = Format(scope, message);
    if (pluginLog == null)
    {
      PluginRuntimeFileLog.Write(PluginRuntimeLogLevel.Warning, formatted);
      return;
    }

    WriteDirect(pluginLog, PluginRuntimeLogLevel.Warning, formatted);
  }

  /// <summary>
  ///     Writes a warning log line with exception details.
  /// </summary>
  /// <param name="exception">The exception to include in the log entry.</param>
  /// <param name="message">The message to write.</param>
  public static void Warning(Exception exception, string message)
  {
    var pluginLog = Echoglossian.PluginLog;
    var formatted = FormatExceptionMessage(message, exception);
    if (pluginLog == null)
    {
      PluginRuntimeFileLog.Write(PluginRuntimeLogLevel.Warning, formatted);
      return;
    }

    pluginLog.Warning(exception, message);
    PluginRuntimeFileLog.Write(PluginRuntimeLogLevel.Warning, formatted);
  }

  /// <summary>
  ///     Writes an error log line.
  /// </summary>
  /// <param name="message">The message to write.</param>
  public static void Error(string message)
  {
    var pluginLog = Echoglossian.PluginLog;
    if (pluginLog == null)
    {
      PluginRuntimeFileLog.Write(PluginRuntimeLogLevel.Error, message);
      return;
    }

    WriteDirect(pluginLog, PluginRuntimeLogLevel.Error, message);
  }

  /// <summary>
  ///     Writes an error log line to an explicit logger sink.
  /// </summary>
  /// <param name="pluginLog">The explicit logger sink to use.</param>
  /// <param name="message">The message to write.</param>
  public static void Error(IPluginLog pluginLog, string message)
  {
    WriteDirect(pluginLog, PluginRuntimeLogLevel.Error, message);
  }

  /// <summary>
  ///     Writes a scoped error log line.
  /// </summary>
  /// <param name="scope">The logical scope to render inside square brackets.</param>
  /// <param name="message">The message to write.</param>
  public static void Error(string scope, string message)
  {
    var pluginLog = Echoglossian.PluginLog;
    var formatted = Format(scope, message);
    if (pluginLog == null)
    {
      PluginRuntimeFileLog.Write(PluginRuntimeLogLevel.Error, formatted);
      return;
    }

    WriteDirect(pluginLog, PluginRuntimeLogLevel.Error, formatted);
  }

  private static void WriteDirect(
      IPluginLog pluginLog,
      PluginRuntimeLogLevel level,
      string message)
  {
    switch (level)
    {
      case PluginRuntimeLogLevel.Debug:
#if DEBUG
        pluginLog.Debug(message);
        PluginRuntimeFileLog.Write(level, message);
#endif
        return;
      case PluginRuntimeLogLevel.Verbose:
        pluginLog.Verbose(message);
        break;
      case PluginRuntimeLogLevel.Information:
        pluginLog.Information(message);
        break;
      case PluginRuntimeLogLevel.Warning:
        pluginLog.Warning(message);
        break;
      case PluginRuntimeLogLevel.Error:
        pluginLog.Error(message);
        break;
      default:
        pluginLog.Information(message);
        break;
    }

    PluginRuntimeFileLog.Write(level, message);
  }

  private static void WriteStructured(
      IPluginLog pluginLog,
      PluginRuntimeLogLevel level,
      string messageTemplate,
      params object[] values)
  {
    switch (level)
    {
      case PluginRuntimeLogLevel.Debug:
#if DEBUG
        pluginLog.Debug(messageTemplate, values);
        PluginRuntimeFileLog.Write(
            level,
            RenderStructuredMessage(pluginLog, messageTemplate, values));
#endif
        return;
      case PluginRuntimeLogLevel.Verbose:
        pluginLog.Verbose(messageTemplate, values);
        break;
      case PluginRuntimeLogLevel.Information:
        pluginLog.Information(messageTemplate, values);
        break;
      case PluginRuntimeLogLevel.Warning:
        pluginLog.Warning(messageTemplate, values);
        break;
      case PluginRuntimeLogLevel.Error:
        pluginLog.Error(messageTemplate, values);
        break;
      default:
        pluginLog.Information(messageTemplate, values);
        break;
    }

    PluginRuntimeFileLog.Write(
        level,
        RenderStructuredMessage(pluginLog, messageTemplate, values));
  }

  private static string RenderStructuredMessage(
      IPluginLog? pluginLog,
      string messageTemplate,
      params object[] values)
  {
    if (values.Length == 0)
    {
      return messageTemplate;
    }

    if (pluginLog != null &&
        pluginLog.Logger.BindMessageTemplate(
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
        values.Select(RenderFallbackValue));
    return $"{messageTemplate} | values: {renderedValues}";
  }

  private static string RenderFallbackValue(object? value)
  {
    return value switch
    {
      null => "<null>",
      string text => text,
      _ => Convert.ToString(value, CultureInfo.InvariantCulture) ??
           value.ToString() ??
           "<null>",
    };
  }

  private static string FormatExceptionMessage(
      string message,
      Exception exception)
  {
    return $"{message}{Environment.NewLine}{exception}";
  }

  /// <summary>
  ///     Prepends an optional logical scope to a message.
  /// </summary>
  /// <param name="scope">The logical scope to render inside square brackets.</param>
  /// <param name="message">The message to format.</param>
  /// <returns>The formatted message.</returns>
  private static string Format(string scope, string message)
  {
    return string.IsNullOrWhiteSpace(scope)
        ? message
        : $"[{scope}] {message}";
  }
}
