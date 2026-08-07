// <copyright file="TranslationSurfaceCatalogValidator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Diagnostics;

namespace TranslationSurfaceDocs;

/// <summary>
/// Validates catalog structure and repository-backed source references.
/// </summary>
internal static class TranslationSurfaceCatalogValidator
{
    private static readonly string[] ValidDatabaseModes = ["none", "sync", "async"];

    /// <summary>
    /// Validates a catalog against a local repository checkout.
    /// </summary>
    /// <param name="catalog">The catalog to validate.</param>
    /// <param name="repoRoot">The local repository root.</param>
    /// <returns>The deterministic validation issues.</returns>
    public static IReadOnlyList<ValidationIssue> Validate(
        TranslationSurfaceCatalog catalog,
        string repoRoot)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);

        List<ValidationIssue> issues = [];
        HashSet<string> sectionIds = catalog.Sections
            .Select(section => section.Id)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> modeFamilyIds = catalog.ModeFamilies
            .Select(modeFamily => modeFamily.Id)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> seenSurfaceIds = new(StringComparer.Ordinal);

        foreach (TranslationSurfaceEntry surface in catalog.Surfaces)
        {
            ValidateSurfaceIdentity(surface, seenSurfaceIds, issues);
            ValidateReferences(surface, sectionIds, modeFamilyIds, issues);
            ValidateRequiredFields(surface, issues);
            ValidateDatabaseModes(surface, issues);
            ValidateDocumentation(surface, repoRoot, issues);
            ValidateCodeAnchors(surface, repoRoot, issues);
        }

        return issues;
    }

    private static void ValidateSurfaceIdentity(
        TranslationSurfaceEntry surface,
        ISet<string> seenSurfaceIds,
        ICollection<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(surface.Id))
        {
            issues.Add(new ValidationIssue(
                "missing-surface-id",
                "A translation surface is missing its id.",
                null));
            return;
        }

        if (!seenSurfaceIds.Add(surface.Id))
        {
            issues.Add(new ValidationIssue(
                "duplicate-surface-id",
                $"Surface '{surface.Id}' is declared more than once.",
                surface.Id));
        }
    }

    private static void ValidateReferences(
        TranslationSurfaceEntry surface,
        ISet<string> sectionIds,
        ISet<string> modeFamilyIds,
        ICollection<ValidationIssue> issues)
    {
        if (!sectionIds.Contains(surface.Section))
        {
            issues.Add(new ValidationIssue(
                "invalid-section-reference",
                $"Surface '{surface.Id}' references unknown section '{surface.Section}'.",
                surface.Id));
        }

        if (!modeFamilyIds.Contains(surface.ModeFamilyId))
        {
            issues.Add(new ValidationIssue(
                "invalid-mode-family-reference",
                $"Surface '{surface.Id}' references unknown mode family " +
                $"'{surface.ModeFamilyId}'.",
                surface.Id));
        }
    }

    private static void ValidateRequiredFields(
        TranslationSurfaceEntry surface,
        ICollection<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(surface.ConfigToggle))
        {
            issues.Add(new ValidationIssue(
                "missing-config-toggle",
                $"Surface '{surface.Id}' is missing its config toggle.",
                surface.Id));
        }

        if (string.IsNullOrWhiteSpace(surface.ReleaseStatus))
        {
            issues.Add(new ValidationIssue(
                "missing-release-status",
                $"Surface '{surface.Id}' is missing its release status.",
                surface.Id));
        }

        if (surface.Notes.Count == 0 ||
            surface.Notes.Values.Any(string.IsNullOrWhiteSpace))
        {
            issues.Add(new ValidationIssue(
                "missing-surface-notes",
                $"Surface '{surface.Id}' is missing required notes.",
                surface.Id));
        }
    }

    private static void ValidateDatabaseModes(
        TranslationSurfaceEntry surface,
        ICollection<ValidationIssue> issues)
    {
        if (!ValidDatabaseModes.Contains(surface.Runtime.DbRead, StringComparer.Ordinal))
        {
            issues.Add(new ValidationIssue(
                "invalid-db-read-mode",
                $"Surface '{surface.Id}' uses unknown dbRead " +
                $"'{surface.Runtime.DbRead}'.",
                surface.Id));
        }

        if (!ValidDatabaseModes.Contains(surface.Runtime.DbWrite, StringComparer.Ordinal))
        {
            issues.Add(new ValidationIssue(
                "invalid-db-write-mode",
                $"Surface '{surface.Id}' uses unknown dbWrite " +
                $"'{surface.Runtime.DbWrite}'.",
                surface.Id));
        }
    }

    private static void ValidateDocumentation(
        TranslationSurfaceEntry surface,
        string repoRoot,
        ICollection<ValidationIssue> issues)
    {
        foreach (string documentPath in surface.Docs)
        {
            if (!TryResolveRepositoryDocumentPath(
                    repoRoot,
                    documentPath,
                    out string resolvedDocumentPath))
            {
                issues.Add(new ValidationIssue(
                    "invalid-doc-reference-path",
                    $"Surface '{surface.Id}' references document path outside " +
                    $"the repository: '{documentPath}'.",
                    surface.Id));
            }
            else if (!File.Exists(resolvedDocumentPath))
            {
                issues.Add(new ValidationIssue(
                    "missing-doc-reference",
                    $"Surface '{surface.Id}' references missing document " +
                    $"'{documentPath}'.",
                    surface.Id));
            }
        }
    }

    private static bool TryResolveRepositoryDocumentPath(
        string repoRoot,
        string documentPath,
        out string resolvedDocumentPath)
    {
        resolvedDocumentPath = string.Empty;
        if (Path.IsPathRooted(documentPath))
        {
            return false;
        }

        try
        {
            string normalizedRepoRoot = Path.GetFullPath(repoRoot);
            string candidatePath = Path.GetFullPath(
                Path.Combine(normalizedRepoRoot, documentPath));
            string relativePath = Path.GetRelativePath(
                normalizedRepoRoot,
                candidatePath);
            if (relativePath.Equals("..", StringComparison.Ordinal) ||
                relativePath.StartsWith(
                    $"..{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal) ||
                relativePath.StartsWith(
                    $"..{Path.AltDirectorySeparatorChar}",
                    StringComparison.Ordinal) ||
                Path.IsPathRooted(relativePath))
            {
                return false;
            }

            resolvedDocumentPath = candidatePath;
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

    private static void ValidateCodeAnchors(
        TranslationSurfaceEntry surface,
        string repoRoot,
        ICollection<ValidationIssue> issues)
    {
        foreach (string anchor in surface.RequiredCodeAnchors)
        {
            if (!RepositoryContainsAnchor(repoRoot, anchor))
            {
                issues.Add(new ValidationIssue(
                    "missing-required-code-anchor",
                    $"Surface '{surface.Id}' requires missing code anchor " +
                    $"'{anchor}'.",
                    surface.Id));
            }
        }
    }

    private static bool RepositoryContainsAnchor(string repoRoot, string anchor)
    {
        using Process process = new();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "rg.exe",
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        process.StartInfo.ArgumentList.Add("--fixed-strings");
        process.StartInfo.ArgumentList.Add("--quiet");
        process.StartInfo.ArgumentList.Add("--glob");
        process.StartInfo.ArgumentList.Add("*.cs");
        process.StartInfo.ArgumentList.Add("--glob");
        process.StartInfo.ArgumentList.Add("!scripts/**");
        process.StartInfo.ArgumentList.Add("--glob");
        process.StartInfo.ArgumentList.Add("!Echoglossian.Tests/**");
        process.StartInfo.ArgumentList.Add("--glob");
        process.StartInfo.ArgumentList.Add("!Echoglossian.Mock.Tests/**");
        process.StartInfo.ArgumentList.Add("--glob");
        process.StartInfo.ArgumentList.Add("!**/bin/**");
        process.StartInfo.ArgumentList.Add("--glob");
        process.StartInfo.ArgumentList.Add("!**/obj/**");
        process.StartInfo.ArgumentList.Add("--");
        process.StartInfo.ArgumentList.Add(anchor);
        process.StartInfo.ArgumentList.Add(repoRoot);
        process.Start();
        process.WaitForExit();

        return process.ExitCode == 0;
    }
}
