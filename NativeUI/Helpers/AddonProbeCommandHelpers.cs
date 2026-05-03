// <copyright file="AddonProbeCommandHelpers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

#if DEBUG
public partial class Echoglossian
{
  /// <summary>
  /// Dumps a recursive probe of the requested addon to the log so we can
  /// inspect its live node tree, component roots, and likely overlay anchors.
  /// </summary>
  /// <param name="command">Command name.</param>
  /// <param name="args">Command arguments.</param>
  private void OnEgloAddonProbeCommand(string command, string args)
  {
    var trimmedArgs = args.Trim();
    if (trimmedArgs.Equals("stop", StringComparison.OrdinalIgnoreCase) ||
        trimmedArgs.Equals("cancel", StringComparison.OrdinalIgnoreCase))
    {
      if (this.addonProbeWatch == null)
      {
        ChatGuiInterface.Print("No active addon probe watch to stop.");
        return;
      }

      this.addonProbeWatch.Stop();
      this.addonProbeWatch = null;

      ChatGuiInterface.Print("Addon probe watch stopped.");
      return;
    }

    var (addonName, addonIndex) = this.ParseAddonProbeArguments(args);
    if (string.IsNullOrWhiteSpace(addonName))
    {
      ChatGuiInterface.Print(
          "Usage: /egloaddonprobe <addon name> [index] or /egloaddonprobe stop");
      return;
    }

    this.addonProbeWatch?.Dispose();
    this.addonProbeWatch = AddonStructureProbe.StartWatch(
        GameGuiInterface,
        PluginLog,
        addonName,
        addonIndex);

    ChatGuiInterface.Print(
        $"Addon probe watch started for '{addonName}'[{addonIndex}] for 60 seconds. Check the Dalamud log for event and tree dumps.");
  }

  /// <summary>
  /// Parses the addon probe command arguments into an addon name and optional index.
  /// </summary>
  /// <param name="args">The raw command arguments.</param>
  /// <returns>The addon name and index to probe.</returns>
  private (string AddonName, int Index) ParseAddonProbeArguments(string args)
  {
    var trimmedArgs = args.Trim();
    if (trimmedArgs.Length == 0)
    {
      return (string.Empty, 0);
    }

    var lastSpace = trimmedArgs.LastIndexOf(' ');
    if (lastSpace > 0 &&
        int.TryParse(trimmedArgs[(lastSpace + 1)..], out var parsedIndex))
    {
      var parsedName = trimmedArgs[..lastSpace].Trim();
      if (parsedName.Length > 0)
      {
        return (parsedName, parsedIndex);
      }
    }

    return (trimmedArgs, 0);
  }
}
#endif
