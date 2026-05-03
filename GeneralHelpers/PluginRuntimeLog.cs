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
  /// <param name="message">The message to write.</param>
  [Conditional("DEBUG")]
  public static void Debug(string message)
  {
    Echoglossian.PluginLog.Debug(message);
  }

  /// <summary>
  ///     Writes a formatted debug log line only in <c>DEBUG</c> builds.
  /// </summary>
  /// <param name="messageTemplate">The structured message template.</param>
  /// <param name="values">The structured logging values.</param>
  [Conditional("DEBUG")]
  public static void Debug(string messageTemplate, params object[] values)
  {
    Echoglossian.PluginLog.Debug(messageTemplate, values);
  }

  /// <summary>
  ///     Writes a scoped debug log line only in <c>DEBUG</c> builds.
  /// </summary>
  /// <param name="scope">The logical scope to render inside square brackets.</param>
  /// <param name="message">The message to write.</param>
  [Conditional("DEBUG")]
  public static void Debug(string scope, string message)
  {
    Echoglossian.PluginLog.Debug(Format(scope, message));
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
    Echoglossian.PluginLog.Debug(Format(scope, messageTemplate), values);
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
    if (level == PluginRuntimeLogLevel.Debug)
    {
#if DEBUG
      Echoglossian.PluginLog.Debug(Format(scope, message));
#endif
      return;
    }

    var formatted = Format(scope, message);
    switch (level)
    {
      case PluginRuntimeLogLevel.Verbose:
        Echoglossian.PluginLog.Verbose(formatted);
        break;
      case PluginRuntimeLogLevel.Information:
        Echoglossian.PluginLog.Information(formatted);
        break;
      case PluginRuntimeLogLevel.Warning:
        Echoglossian.PluginLog.Warning(formatted);
        break;
      case PluginRuntimeLogLevel.Error:
        Echoglossian.PluginLog.Error(formatted);
        break;
      default:
        Echoglossian.PluginLog.Information(formatted);
        break;
    }
  }

  /// <summary>
  ///     Writes a scoped verbose log line.
  /// </summary>
  /// <param name="scope">The logical scope to render inside square brackets.</param>
  /// <param name="message">The message to write.</param>
  public static void Verbose(string scope, string message)
  {
    Echoglossian.PluginLog.Verbose(Format(scope, message));
  }

  /// <summary>
  ///     Writes a scoped informational log line.
  /// </summary>
  /// <param name="scope">The logical scope to render inside square brackets.</param>
  /// <param name="message">The message to write.</param>
  public static void Information(string scope, string message)
  {
    Echoglossian.PluginLog.Information(Format(scope, message));
  }

  /// <summary>
  ///     Writes a scoped warning log line.
  /// </summary>
  /// <param name="scope">The logical scope to render inside square brackets.</param>
  /// <param name="message">The message to write.</param>
  public static void Warning(string scope, string message)
  {
    Echoglossian.PluginLog.Warning(Format(scope, message));
  }

  /// <summary>
  ///     Writes a scoped error log line.
  /// </summary>
  /// <param name="scope">The logical scope to render inside square brackets.</param>
  /// <param name="message">The message to write.</param>
  public static void Error(string scope, string message)
  {
    Echoglossian.PluginLog.Error(Format(scope, message));
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
