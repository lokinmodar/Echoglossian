// <copyright file="GameVersionLookupHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
///     Centralizes runtime lookup semantics for persisted rows that may have a
///     version-specific or version-agnostic <c>GameVersion</c>.
/// </summary>
public static class GameVersionLookupHelper
{
    /// <summary>
    ///     Gets whether the requested lookup version is populated.
    /// </summary>
    /// <param name="requestedVersion">The requested runtime version.</param>
    /// <returns>True when the requested version is populated.</returns>
    public static bool HasRequestedVersion(string? requestedVersion)
    {
        return !string.IsNullOrWhiteSpace(requestedVersion);
    }

    /// <summary>
    ///     Determines whether one stored row version is compatible with the
    ///     requested runtime version.
    /// </summary>
    /// <param name="storedVersion">The version persisted with the row.</param>
    /// <param name="requestedVersion">The current runtime version being requested.</param>
    /// <returns>True when the stored row should be considered compatible.</returns>
    public static bool MatchesStoredVersion(
        string? storedVersion,
        string? requestedVersion)
    {
        if (!HasRequestedVersion(requestedVersion))
        {
            return string.IsNullOrWhiteSpace(storedVersion);
        }

        return string.IsNullOrWhiteSpace(storedVersion) ||
               string.Equals(
                   storedVersion,
                   requestedVersion,
                   StringComparison.Ordinal);
    }
}
