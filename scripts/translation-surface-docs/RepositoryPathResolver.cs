// <copyright file="RepositoryPathResolver.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace TranslationSurfaceDocs;

/// <summary>
/// Resolves catalog-controlled relative paths under the repository root.
/// </summary>
internal static class RepositoryPathResolver
{
    /// <summary>
    /// Resolves a relative path under the repository root.
    /// </summary>
    /// <param name="repoRoot">The repository root directory.</param>
    /// <param name="relativePath">The catalog-controlled relative path.</param>
    /// <param name="resolvedPath">The resolved absolute path when containment succeeds.</param>
    /// <returns><see langword="true" /> when the path resolves inside the repository root; otherwise, <see langword="false" />.</returns>
    public static bool TryResolveRepositoryPath(
        string repoRoot,
        string relativePath,
        out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            return false;
        }

        try
        {
            string normalizedRepoRoot = Path.GetFullPath(repoRoot);
            string candidatePath = Path.GetFullPath(
                Path.Combine(normalizedRepoRoot, relativePath));
            string relativeCandidatePath = Path.GetRelativePath(
                normalizedRepoRoot,
                candidatePath);
            if (relativeCandidatePath.Equals("..", StringComparison.Ordinal) ||
                relativeCandidatePath.StartsWith(
                    $"..{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal) ||
                relativeCandidatePath.StartsWith(
                    $"..{Path.AltDirectorySeparatorChar}",
                    StringComparison.Ordinal) ||
                Path.IsPathRooted(relativeCandidatePath))
            {
                return false;
            }

            resolvedPath = candidatePath;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Resolves a relative path under the repository root or throws when it escapes containment.
    /// </summary>
    /// <param name="repoRoot">The repository root directory.</param>
    /// <param name="relativePath">The catalog-controlled relative path.</param>
    /// <returns>The resolved absolute path.</returns>
    public static string ResolveRepositoryPathOrThrow(string repoRoot, string relativePath)
    {
        if (!TryResolveRepositoryPath(repoRoot, relativePath, out string resolvedPath))
        {
            throw new InvalidOperationException(
                $"Catalog-controlled path '{relativePath}' escapes repository root '{repoRoot}'.");
        }

        return resolvedPath;
    }
}
