// <copyright file="AddonProbeCommandHelpers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

#if DEBUG
public partial class Echoglossian
{
  private static readonly TimeSpan MaxAddonProbeWatchDuration = TimeSpan.FromMinutes(30);

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
        ChatGuiInterface.Print(Resources.AddonProbeNoActiveWatch);
        return;
      }

      this.addonProbeWatch.Stop();
      this.addonProbeWatch = null;

      ChatGuiInterface.Print(Resources.AddonProbeStopped);
      return;
    }

    var (addonName, addonIndex, duration, hasInvalidDuration) =
        this.ParseAddonProbeArguments(args);
    if (hasInvalidDuration)
    {
      ChatGuiInterface.Print(
          string.Format(
              Resources.AddonProbeInvalidDurationFormat,
              (int)MaxAddonProbeWatchDuration.TotalMinutes));
      return;
    }

    if (string.IsNullOrWhiteSpace(addonName))
    {
      ChatGuiInterface.Print(Resources.AddonProbeUsage);
      return;
    }

    this.addonProbeWatch?.Dispose();
    this.addonProbeWatch = AddonStructureProbe.StartWatch(
        GameGuiInterface,
        PluginLog,
        addonName,
        addonIndex,
        duration);

    ChatGuiInterface.Print(
        string.Format(
            Resources.AddonProbeStartedFormat,
            addonName,
            addonIndex,
            this.FormatAddonProbeDuration(duration ?? TimeSpan.FromMinutes(1))));
  }

  /// <summary>
  /// Parses the addon probe command arguments into an addon name, optional index,
  /// and optional watch duration.
  /// </summary>
  /// <param name="args">The raw command arguments.</param>
  /// <returns>The addon name, index, and duration to probe.</returns>
  private (string AddonName, int Index, TimeSpan? Duration, bool HasInvalidDuration)
      ParseAddonProbeArguments(string args)
  {
    var trimmedArgs = args.Trim();
    if (trimmedArgs.Length == 0)
    {
      return (string.Empty, 0, null, false);
    }

    var parts = trimmedArgs.Split(
        ' ',
        StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length == 0)
    {
      return (string.Empty, 0, null, false);
    }

    var partCount = parts.Length;
    TimeSpan? duration = null;

    if (TryParseAddonProbeDuration(parts[partCount - 1], out var parsedDuration))
    {
      duration = parsedDuration;
      partCount--;
    }
    else if (LooksLikeAddonProbeDuration(parts[partCount - 1]))
    {
      return (string.Empty, 0, null, true);
    }

    var index = 0;
    if (partCount > 1 &&
        int.TryParse(parts[partCount - 1], out var parsedIndex))
    {
      index = parsedIndex;
      partCount--;
    }

    var addonName = string.Join(" ", parts.Take(partCount)).Trim();
    return (addonName, index, duration, false);
  }

  /// <summary>
  /// Parses one addon-probe duration token such as "90s" or "15m".
  /// </summary>
  /// <param name="token">The raw duration token.</param>
  /// <param name="duration">Receives the parsed duration.</param>
  /// <returns><see langword="true"/> when the token parsed successfully.</returns>
  private static bool TryParseAddonProbeDuration(
      string token,
      out TimeSpan duration)
  {
    duration = default;
    if (string.IsNullOrWhiteSpace(token) || token.Length < 2)
    {
      return false;
    }

    var unit = char.ToLowerInvariant(token[^1]);
    var numberText = token[..^1];
    if (!int.TryParse(numberText, out var value) || value <= 0)
    {
      return false;
    }

    duration = unit switch
    {
      's' => TimeSpan.FromSeconds(value),
      'm' => TimeSpan.FromMinutes(value),
      _ => default,
    };

    return duration > TimeSpan.Zero &&
           duration <= MaxAddonProbeWatchDuration;
  }

  /// <summary>
  /// Determines whether a token looks like an addon-probe duration even when the
  /// value is outside the accepted range.
  /// </summary>
  /// <param name="token">The token to inspect.</param>
  /// <returns><see langword="true"/> when the token resembles a duration.</returns>
  private static bool LooksLikeAddonProbeDuration(string token)
  {
    if (string.IsNullOrWhiteSpace(token) || token.Length < 2)
    {
      return false;
    }

    var unit = char.ToLowerInvariant(token[^1]);
    if (unit != 's' && unit != 'm')
    {
      return false;
    }

    return int.TryParse(token[..^1], out _);
  }

  /// <summary>
  /// Formats one addon-probe watch duration for chat feedback.
  /// </summary>
  /// <param name="duration">The duration to format.</param>
  /// <returns>The human-readable duration string.</returns>
  private string FormatAddonProbeDuration(TimeSpan duration)
  {
    if (duration.TotalMinutes >= 1 &&
        Math.Abs(duration.TotalMinutes - Math.Round(duration.TotalMinutes)) < 0.001d)
    {
      return string.Format(
          duration.TotalMinutes == 1
              ? Resources.AddonProbeDurationMinuteFormat
              : Resources.AddonProbeDurationMinutesFormat,
          (int)Math.Round(duration.TotalMinutes));
    }

    return string.Format(
        duration.TotalSeconds == 1
            ? Resources.AddonProbeDurationSecondFormat
            : Resources.AddonProbeDurationSecondsFormat,
        (int)Math.Round(duration.TotalSeconds));
  }
}
#endif
