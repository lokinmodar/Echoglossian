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
    pluginLog.Debug(message);
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
    pluginLog.Debug(messageTemplate, values);
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
      return;
    }

    pluginLog.Debug(message);
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
      return;
    }

    pluginLog.Debug(messageTemplate, values);
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
    if (pluginLog == null)
    {
      return;
    }

    pluginLog.Debug(Format(scope, message));
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
    if (pluginLog == null)
    {
      return;
    }

    pluginLog.Debug(Format(scope, messageTemplate), values);
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
    if (pluginLog == null)
    {
      return;
    }

    Write(pluginLog, level, scope, message);
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
      return;
    }

    Write(pluginLog, level, string.Empty, message);
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
    if (level == PluginRuntimeLogLevel.Debug)
    {
#if DEBUG
      pluginLog.Debug(Format(scope, message));
#endif
      return;
    }

    var formatted = Format(scope, message);
    switch (level)
    {
      case PluginRuntimeLogLevel.Verbose:
        pluginLog.Verbose(formatted);
        break;
      case PluginRuntimeLogLevel.Information:
        pluginLog.Information(formatted);
        break;
      case PluginRuntimeLogLevel.Warning:
        pluginLog.Warning(formatted);
        break;
      case PluginRuntimeLogLevel.Error:
        pluginLog.Error(formatted);
        break;
      default:
        pluginLog.Information(formatted);
        break;
    }
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
      return;
    }

    pluginLog.Verbose(message);
  }

  /// <summary>
  ///     Writes a verbose log line to an explicit logger sink.
  /// </summary>
  /// <param name="pluginLog">The explicit logger sink to use.</param>
  /// <param name="message">The message to write.</param>
  public static void Verbose(IPluginLog pluginLog, string message)
  {
    pluginLog.Verbose(message);
  }

  /// <summary>
  ///     Writes a scoped verbose log line.
  /// </summary>
  /// <param name="scope">The logical scope to render inside square brackets.</param>
  /// <param name="message">The message to write.</param>
  public static void Verbose(string scope, string message)
  {
    var pluginLog = Echoglossian.PluginLog;
    if (pluginLog == null)
    {
      return;
    }

    pluginLog.Verbose(Format(scope, message));
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
      return;
    }

    pluginLog.Information(message);
  }

  /// <summary>
  ///     Writes an informational log line to an explicit logger sink.
  /// </summary>
  /// <param name="pluginLog">The explicit logger sink to use.</param>
  /// <param name="message">The message to write.</param>
  public static void Information(IPluginLog pluginLog, string message)
  {
    pluginLog.Information(message);
  }

  /// <summary>
  ///     Writes a scoped informational log line.
  /// </summary>
  /// <param name="scope">The logical scope to render inside square brackets.</param>
  /// <param name="message">The message to write.</param>
  public static void Information(string scope, string message)
  {
    var pluginLog = Echoglossian.PluginLog;
    if (pluginLog == null)
    {
      return;
    }

    pluginLog.Information(Format(scope, message));
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
      return;
    }

    pluginLog.Warning(message);
  }

  /// <summary>
  ///     Writes a warning log line to an explicit logger sink.
  /// </summary>
  /// <param name="pluginLog">The explicit logger sink to use.</param>
  /// <param name="message">The message to write.</param>
  public static void Warning(IPluginLog pluginLog, string message)
  {
    pluginLog.Warning(message);
  }

  /// <summary>
  ///     Writes a scoped warning log line.
  /// </summary>
  /// <param name="scope">The logical scope to render inside square brackets.</param>
  /// <param name="message">The message to write.</param>
  public static void Warning(string scope, string message)
  {
    var pluginLog = Echoglossian.PluginLog;
    if (pluginLog == null)
    {
      return;
    }

    pluginLog.Warning(Format(scope, message));
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
      return;
    }

    pluginLog.Error(message);
  }

  /// <summary>
  ///     Writes an error log line to an explicit logger sink.
  /// </summary>
  /// <param name="pluginLog">The explicit logger sink to use.</param>
  /// <param name="message">The message to write.</param>
  public static void Error(IPluginLog pluginLog, string message)
  {
    pluginLog.Error(message);
  }

  /// <summary>
  ///     Writes a scoped error log line.
  /// </summary>
  /// <param name="scope">The logical scope to render inside square brackets.</param>
  /// <param name="message">The message to write.</param>
  public static void Error(string scope, string message)
  {
    var pluginLog = Echoglossian.PluginLog;
    if (pluginLog == null)
    {
      return;
    }

    pluginLog.Error(Format(scope, message));
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
