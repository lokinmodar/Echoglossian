// <copyright file="PreviewCommandLine.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Previewer.Hosting;

/// <summary>
///     Represents the stable command-line options accepted by the previewer.
/// </summary>
internal sealed class PreviewCommandLine
{
    /// <summary>
    ///     Gets a value indicating whether the native ImGui binding probe was
    ///     requested.
    /// </summary>
    internal bool BindingSmoke { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether the future standalone host probe was
    ///     requested.
    /// </summary>
    internal bool HostSmoke { get; private set; }

    /// <summary>
    ///     Gets the absolute or relative configuration JSON path.
    /// </summary>
    internal string? ConfigPath { get; private set; }

    /// <summary>
    ///     Gets the requested overlay surface key.
    /// </summary>
    internal string? Scenario { get; private set; }

    /// <summary>
    ///     Gets the requested viewport width in pixels.
    /// </summary>
    internal int? ViewportWidth { get; private set; }

    /// <summary>
    ///     Gets the requested viewport height in pixels.
    /// </summary>
    internal int? ViewportHeight { get; private set; }

    /// <summary>
    ///     Gets the requested screenshot mode.
    /// </summary>
    internal string? ScreenshotMode { get; private set; }

    /// <summary>
    ///     Gets the requested screenshot output directory.
    /// </summary>
    internal string? OutputDirectory { get; private set; }

    /// <summary>
    ///     Parses the supported previewer command-line arguments.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The parsed command-line options.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when an argument is unknown or malformed.
    /// </exception>
    internal static PreviewCommandLine Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var commandLine = new PreviewCommandLine();

        for (var index = 0; index < args.Count; index++)
        {
            switch (args[index])
            {
                case "--binding-smoke":
                    commandLine.BindingSmoke = SetFlag(
                        commandLine.BindingSmoke,
                        "--binding-smoke");
                    break;
                case "--host-smoke":
                    commandLine.HostSmoke = SetFlag(
                        commandLine.HostSmoke,
                        "--host-smoke");
                    break;
                case "--config":
                    commandLine.ConfigPath = GetValue(args, ref index, "--config");
                    break;
                case "--scenario":
                    commandLine.Scenario = GetValue(args, ref index, "--scenario");
                    break;
                case "--viewport":
                    var viewport = ParseViewport(
                        GetValue(args, ref index, "--viewport"));
                    commandLine.ViewportWidth = viewport.Width;
                    commandLine.ViewportHeight = viewport.Height;
                    break;
                case "--screenshot":
                    commandLine.ScreenshotMode = ParseScreenshotMode(
                        GetValue(args, ref index, "--screenshot"));
                    break;
                case "--output":
                    commandLine.OutputDirectory = GetValue(args, ref index, "--output");
                    break;
                default:
                    throw new ArgumentException(
                        $"Unknown previewer argument: {args[index]}");
            }
        }

        return commandLine;
    }

    /// <summary>
    ///     Validates and returns a command-line flag value.
    /// </summary>
    /// <param name="currentValue">The current flag value.</param>
    /// <param name="option">The option name.</param>
    /// <returns><see langword="true" /> when the option was not repeated.</returns>
    private static bool SetFlag(bool currentValue, string option)
    {
        if (currentValue)
        {
            throw new ArgumentException($"Previewer argument is repeated: {option}");
        }

        return true;
    }

    /// <summary>
    ///     Reads the required value that immediately follows an option.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="index">The index of the current option.</param>
    /// <param name="option">The option name.</param>
    /// <returns>The non-empty option value.</returns>
    private static string GetValue(
        IReadOnlyList<string> args,
        ref int index,
        string option)
    {
        if (index + 1 >= args.Count || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            throw new ArgumentException($"Previewer argument requires a value: {option}");
        }

        index++;
        return args[index];
    }

    /// <summary>
    ///     Parses one positive viewport dimension pair.
    /// </summary>
    /// <param name="value">The viewport value in <c>widthxheight</c> form.</param>
    /// <returns>The parsed viewport dimensions.</returns>
    private static (int Width, int Height) ParseViewport(string value)
    {
        var dimensions = value.Split('x', 'X');

        if (dimensions.Length != 2 ||
            !int.TryParse(dimensions[0], out var width) ||
            !int.TryParse(dimensions[1], out var height) ||
            width <= 0 ||
            height <= 0)
        {
            throw new ArgumentException(
                "Previewer viewport must use positive widthxheight dimensions.");
        }

        return (width, height);
    }

    /// <summary>
    ///     Validates the requested screenshot capture mode.
    /// </summary>
    /// <param name="value">The requested screenshot mode.</param>
    /// <returns>The validated screenshot mode.</returns>
    private static string ParseScreenshotMode(string value)
    {
        if (value is "full" or "surface" or "batch")
        {
            return value;
        }

        throw new ArgumentException(
            "Previewer screenshot mode must be full, surface, or batch.");
    }
}
