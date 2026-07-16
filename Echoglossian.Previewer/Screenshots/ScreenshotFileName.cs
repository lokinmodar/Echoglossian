// <copyright file="ScreenshotFileName.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.Scenarios;

namespace Echoglossian.Previewer.Screenshots;

/// <summary>
/// Builds deterministic screenshot output paths.
/// </summary>
internal static class ScreenshotFileName
{
    private static readonly char[] UnsafeFileNameCharacters =
    [
        .. Path.GetInvalidFileNameChars(),
        ':',
        '*',
        '?',
        '"',
        '<',
        '>',
        '|',
        '/',
        '\\',
    ];

    /// <summary>
    /// Creates a deterministic PNG file name for a screenshot.
    /// </summary>
    /// <param name="mode">The screenshot mode.</param>
    /// <param name="scenarioKey">The scenario key.</param>
    /// <param name="viewport">The logical viewport.</param>
    /// <returns>A Windows-safe PNG file name.</returns>
    internal static string CreatePngName(
        ScreenshotMode mode,
        string scenarioKey,
        PreviewViewportPreset viewport)
    {
        ArgumentNullException.ThrowIfNull(viewport);

        var modePart = Sanitize(mode.ToString().ToLowerInvariant());
        var scenarioPart = Sanitize(scenarioKey);
        return $"{modePart}-{scenarioPart}-{viewport.Width}x{viewport.Height}.png";
    }

    /// <summary>
    /// Creates the default timestamped screenshot output directory.
    /// </summary>
    /// <param name="timestamp">The timestamp used for deterministic directory naming.</param>
    /// <returns>The relative artifact output directory.</returns>
    internal static string CreateDefaultOutputDirectory(DateTimeOffset timestamp)
    {
        return Path.Combine(
            "artifacts",
            "previewer",
            "screenshots",
            timestamp.UtcDateTime.ToString("yyyyMMdd-HHmmss"));
    }

    private static string Sanitize(string? value)
    {
        var safe = new char[(value ?? string.Empty).Length];
        var count = 0;
        var lastWasDash = false;

        foreach (var character in value ?? string.Empty)
        {
            var replacement = UnsafeFileNameCharacters.Contains(character) ||
                char.IsWhiteSpace(character)
                ? '-'
                : char.ToLowerInvariant(character);
            if (replacement == '-')
            {
                if (lastWasDash)
                {
                    continue;
                }

                lastWasDash = true;
            }
            else
            {
                lastWasDash = false;
            }

            safe[count++] = replacement;
        }

        var sanitized = new string(safe, 0, count).Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? "preview" : sanitized;
    }
}
