// <copyright file="TooltipRegisterLoggingCommandHelpers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

namespace Echoglossian;

#if DEBUG
/// <summary>
/// Handles the temporary tooltip-registration diagnostic command.
/// </summary>
public partial class Echoglossian
{
  /// <summary>
  /// Starts or stops temporary logging for hover-tooltip registrations and
  /// hover-hit transitions.
  /// </summary>
  /// <param name="command">Command name.</param>
  /// <param name="args">Command arguments.</param>
  private void OnEgloTooltipRegisterLoggingCommand(string command, string args)
  {
    if (!HoverTooltipRegistrationDiagnostics.TryParseCommandArguments(
            args,
            out var parsedArguments,
            out var hasInvalidDuration))
    {
      ChatGuiInterface.Print(
          hasInvalidDuration
              ? string.Format(
                  Resources.TooltipRegisterLoggingInvalidDurationFormat,
                  (int)HoverTooltipRegistrationDiagnostics.MaxDuration.TotalMinutes)
              : Resources.TooltipRegisterLoggingUsage);
      return;
    }

    if (parsedArguments.IsStop)
    {
      if (!this.hoverTooltipManager.IsRegistrationLoggingActive())
      {
        ChatGuiInterface.Print(Resources.TooltipRegisterLoggingNoActiveSession);
        return;
      }

      this.hoverTooltipManager.StopRegistrationLogging();
      ChatGuiInterface.Print(Resources.TooltipRegisterLoggingStopped);
      return;
    }

    this.hoverTooltipManager.StartRegistrationLogging(
        parsedArguments.SurfaceFilter,
        parsedArguments.Duration);
    ChatGuiInterface.Print(
        string.Format(
            Resources.TooltipRegisterLoggingStartedFormat,
            parsedArguments.SurfaceFilter,
            this.FormatAddonProbeDuration(parsedArguments.Duration)));
  }
}
#endif
