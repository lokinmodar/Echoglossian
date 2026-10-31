// <copyright file="TranslationToggleCommandParser.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
/// Parses the global translation subcommand accepted by <c>/eglo</c>.
/// </summary>
internal static class TranslationToggleCommandParser
{
  /// <summary>
  /// Parses command arguments without changing runtime state.
  /// </summary>
  /// <param name="arguments">The raw command arguments.</param>
  /// <returns>The parsed command action.</returns>
  public static TranslationToggleCommand Parse(string arguments)
  {
    if (string.IsNullOrWhiteSpace(arguments))
    {
      return TranslationToggleCommand.OpenConfiguration;
    }

    var parts = arguments.Split(
        ' ',
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (parts.Length != 2 ||
        !string.Equals(parts[0], "translations", StringComparison.OrdinalIgnoreCase))
    {
      return TranslationToggleCommand.Invalid;
    }

    return parts[1].ToLowerInvariant() switch
    {
      "on" => TranslationToggleCommand.Enable,
      "off" => TranslationToggleCommand.Disable,
      "toggle" => TranslationToggleCommand.Toggle,
      _ => TranslationToggleCommand.Invalid,
    };
  }
}

/// <summary>
/// Describes one parsed <c>/eglo</c> command action.
/// </summary>
internal enum TranslationToggleCommand
{
  OpenConfiguration,
  Enable,
  Disable,
  Toggle,
  Invalid,
}
