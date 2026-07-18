// <copyright file="DalaMockHostCompatibilityGuard.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Plugin.Services;
using DalaMock.Core.Plugin;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace Echoglossian.Mock;

/// <summary>
/// Detects the known host/runtime drift between the local Dalamud hook and the
/// currently published DalaMock.Core package.
/// </summary>
internal static class DalaMockHostCompatibilityGuard
{
    private const string IFrameworkCreateDebouncerToken =
        "M:Dalamud.Plugin.Services.IFramework.CreateDebouncer(System.TimeSpan,System.Action)";
    private const string CreateDebouncerToken = "CreateDebouncer";

    /// <summary>
    /// Evaluates the known framework contract drift using already-resolved facts.
    /// </summary>
    /// <param name="dalamudRequiresCreateDebouncer">Whether the active local Dalamud contract requires <c>CreateDebouncer</c>.</param>
    /// <param name="dalamudIdentity">The resolved Dalamud identity string.</param>
    /// <param name="dalaMockAdvertisesCreateDebouncer">Whether the active DalaMock assembly advertises <c>CreateDebouncer</c>.</param>
    /// <param name="dalaMockIdentity">The resolved DalaMock identity string.</param>
    /// <returns>The compatibility result.</returns>
    internal static CompatibilityResult EvaluateKnownContracts(
        bool dalamudRequiresCreateDebouncer,
        string dalamudIdentity,
        bool dalaMockAdvertisesCreateDebouncer,
        string dalaMockIdentity)
    {
        if (!dalamudRequiresCreateDebouncer || dalaMockAdvertisesCreateDebouncer)
        {
            return CompatibilityResult.Compatible();
        }

        return CompatibilityResult.Incompatible(
            "The local DalaMock host is incompatible with the current Dalamud runtime. "
            + $"{dalamudIdentity} requires IFramework.{CreateDebouncerToken}(TimeSpan, Action), "
            + $"but {dalaMockIdentity} does not advertise that member. "
            + "Treat this as upstream DalaMock/Dalamud drift rather than a JournalDetail regression. "
            + "Update DalaMock or pin an older compatible Dalamud hook before running the .Mock rail.");
    }

    /// <summary>
    /// Inspects the current local runtime and returns the known DalaMock host compatibility state.
    /// </summary>
    /// <returns>The compatibility result.</returns>
    internal static CompatibilityResult InspectCurrentRuntime()
    {
        var dalamudAssemblyPath = typeof(IFramework).Assembly.Location;
        var dalamudXmlPath = Path.Combine(
            Path.GetDirectoryName(dalamudAssemblyPath) ?? string.Empty,
            "Dalamud.xml");
        var dalamudRequiresCreateDebouncer =
            File.Exists(dalamudXmlPath)
            && File.ReadAllText(dalamudXmlPath, Encoding.UTF8).Contains(
                IFrameworkCreateDebouncerToken,
                StringComparison.Ordinal);

        var dalaMockAssemblyPath = typeof(MockContainer).Assembly.Location;
        var dalaMockAdvertisesCreateDebouncer = FileContainsUtf8Token(
            dalaMockAssemblyPath,
            CreateDebouncerToken);

        return EvaluateKnownContracts(
            dalamudRequiresCreateDebouncer,
            DescribeAssembly(dalamudAssemblyPath, includeCommitHash: true),
            dalaMockAdvertisesCreateDebouncer,
            DescribeAssembly(dalaMockAssemblyPath, includeCommitHash: false));
    }

    /// <summary>
    /// Throws when the current local DalaMock host is known to be incompatible.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the host/runtime pair is known to be incompatible.</exception>
    internal static void ThrowIfIncompatible()
    {
        var result = InspectCurrentRuntime();
        if (!result.IsCompatible)
        {
            throw new InvalidOperationException(result.Message);
        }
    }

    /// <summary>
    /// Writes the current compatibility state in a script-friendly form.
    /// </summary>
    /// <param name="output">The output writer that receives the probe message.</param>
    /// <returns><c>0</c> when compatible; otherwise <c>1</c>.</returns>
    internal static int WriteCommandLineStatus(TextWriter output)
    {
        var result = InspectCurrentRuntime();
        if (result.IsCompatible)
        {
            output.WriteLine("DalaMock compatibility check passed.");
            return 0;
        }

        output.WriteLine(result.Message);
        return 1;
    }

    /// <summary>
    /// Describes an assembly using its versions, optional Dalamud commit hash,
    /// and path for diagnostics.
    /// </summary>
    /// <param name="assemblyPath">The assembly path to describe.</param>
    /// <param name="includeCommitHash">Whether a sibling <c>commit_hash.txt</c> should be included when present.</param>
    /// <returns>The assembly identity string.</returns>
    private static string DescribeAssembly(string assemblyPath, bool includeCommitHash)
    {
        var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
        var versionInfo = FileVersionInfo.GetVersionInfo(assemblyPath);
        var description = $"{assemblyName.Name} assembly {assemblyName.Version}";

        if (!string.IsNullOrWhiteSpace(versionInfo.FileVersion))
        {
            description += $", file {versionInfo.FileVersion}";
        }

        if (includeCommitHash)
        {
            var assemblyDirectory = Path.GetDirectoryName(assemblyPath);
            if (!string.IsNullOrWhiteSpace(assemblyDirectory))
            {
                var commitHashPath = Path.Combine(assemblyDirectory, "commit_hash.txt");
                if (File.Exists(commitHashPath))
                {
                    var commitHash = File.ReadAllText(commitHashPath, Encoding.UTF8).Trim();
                    if (!string.IsNullOrWhiteSpace(commitHash))
                    {
                        description += $", commit {commitHash}";
                    }
                }
            }
        }

        return $"{description} at {assemblyPath}";
    }

    /// <summary>
    /// Checks whether a binary file contains a UTF-8 metadata token.
    /// </summary>
    /// <param name="path">The file path to inspect.</param>
    /// <param name="token">The metadata token to search for.</param>
    /// <returns><see langword="true"/> when the token is present; otherwise <see langword="false"/>.</returns>
    private static bool FileContainsUtf8Token(string path, string token)
    {
        var haystack = File.ReadAllBytes(path);
        var needle = Encoding.UTF8.GetBytes(token);

        if (needle.Length == 0 || haystack.Length < needle.Length)
        {
            return false;
        }

        for (var start = 0; start <= haystack.Length - needle.Length; start++)
        {
            var matched = true;
            for (var offset = 0; offset < needle.Length; offset++)
            {
                if (haystack[start + offset] != needle[offset])
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Represents the current host/runtime compatibility decision.
    /// </summary>
    internal sealed class CompatibilityResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CompatibilityResult"/> class.
        /// </summary>
        /// <param name="isCompatible">Whether the current host/runtime pair is compatible.</param>
        /// <param name="message">The diagnostic message for incompatible states.</param>
        private CompatibilityResult(bool isCompatible, string? message)
        {
            this.IsCompatible = isCompatible;
            this.Message = message;
        }

        /// <summary>
        /// Gets a value indicating whether the inspected runtime pair is compatible.
        /// </summary>
        public bool IsCompatible { get; }

        /// <summary>
        /// Gets the diagnostic message for incompatible states.
        /// </summary>
        public string? Message { get; }

        /// <summary>
        /// Creates a compatible result.
        /// </summary>
        /// <returns>The compatible result.</returns>
        public static CompatibilityResult Compatible()
        {
            return new CompatibilityResult(true, null);
        }

        /// <summary>
        /// Creates an incompatible result with a diagnostic message.
        /// </summary>
        /// <param name="message">The incompatibility message.</param>
        /// <returns>The incompatible result.</returns>
        public static CompatibilityResult Incompatible(string message)
        {
            return new CompatibilityResult(false, message);
        }
    }
}
