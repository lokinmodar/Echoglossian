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
    private static readonly TimeSpan RepositorySearchTimeout = TimeSpan.FromSeconds(10);

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
        ValidateLocales(catalog.Locales, repoRoot, issues);
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

    private static void ValidateLocales(
        IReadOnlyList<TranslationSurfaceLocale> locales,
        string repoRoot,
        ICollection<ValidationIssue> issues)
    {
        HashSet<string> seenLocaleIds = new(StringComparer.Ordinal);
        HashSet<string> seenLocalePaths = new(StringComparer.Ordinal);
        foreach (TranslationSurfaceLocale locale in locales)
        {
            if (string.IsNullOrWhiteSpace(locale.Id))
            {
                issues.Add(new ValidationIssue(
                    "missing-locale-id",
                    "A generated support-matrix locale is missing its id.",
                    null));
                continue;
            }

            if (!seenLocaleIds.Add(locale.Id))
            {
                issues.Add(new ValidationIssue(
                    "duplicate-locale-id",
                    $"Locale '{locale.Id}' is declared more than once.",
                    locale.Id));
            }

            if (!SupportMatrixLocaleResources.IsSupportedLocale(locale.Id))
            {
                issues.Add(new ValidationIssue(
                    "missing-locale-resources",
                    $"Locale '{locale.Id}' does not have support-matrix label resources.",
                    locale.Id));
            }

            if (string.IsNullOrWhiteSpace(locale.Path))
            {
                issues.Add(new ValidationIssue(
                    "missing-locale-output-path",
                    $"Locale '{locale.Id}' is missing its output path.",
                    locale.Id));
                continue;
            }

            if (!seenLocalePaths.Add(locale.Path))
            {
                issues.Add(new ValidationIssue(
                    "duplicate-locale-output-path",
                    $"Locale output path '{locale.Path}' is declared more than once.",
                    locale.Id));
            }

            if (!RepositoryPathResolver.TryResolveRepositoryPath(
                    repoRoot,
                    locale.Path,
                    out _))
            {
                issues.Add(new ValidationIssue(
                    "invalid-locale-output-path",
                    $"Locale '{locale.Id}' references output path outside the repository: '{locale.Path}'.",
                    locale.Id));
            }
        }
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
            if (!RepositoryPathResolver.TryResolveRepositoryPath(
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

    private static void ValidateCodeAnchors(
        TranslationSurfaceEntry surface,
        string repoRoot,
        ICollection<ValidationIssue> issues)
    {
        if (surface.RequiredCodeAnchors.Count == 0)
        {
            issues.Add(new ValidationIssue(
                "missing-required-code-anchor-declaration",
                $"Surface '{surface.Id}' must declare at least one required code anchor.",
                surface.Id));
            return;
        }

        foreach (string anchor in surface.RequiredCodeAnchors)
        {
            RepositorySearchOutcome outcome = TryRepositoryContainsAnchor(
                repoRoot,
                anchor,
                out string errorMessage);
            switch (outcome)
            {
                case RepositorySearchOutcome.MatchFound:
                    break;
                case RepositorySearchOutcome.NoMatchFound:
                    issues.Add(new ValidationIssue(
                        "missing-required-code-anchor",
                        $"Surface '{surface.Id}' requires missing code anchor " +
                        $"'{anchor}'.",
                        surface.Id));
                    break;
                case RepositorySearchOutcome.ExecutionError:
                    issues.Add(new ValidationIssue(
                        "code-anchor-validation-error",
                        $"Surface '{surface.Id}' failed repository anchor validation for '{anchor}': {errorMessage}",
                        surface.Id));
                    break;
            }
        }
    }

    private static RepositorySearchOutcome TryRepositoryContainsAnchor(
        string repoRoot,
        string anchor,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "rg.exe",
                WorkingDirectory = repoRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
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
        try
        {
            process.Start();
            if (!process.WaitForExit((int)RepositorySearchTimeout.TotalMilliseconds))
            {
                process.Kill(entireProcessTree: true);
                errorMessage =
                    $"rg timed out after {RepositorySearchTimeout.TotalSeconds:0} seconds.";
                return RepositorySearchOutcome.ExecutionError;
            }

            string standardError = process.StandardError.ReadToEnd().Trim();
            return process.ExitCode switch
            {
                0 => RepositorySearchOutcome.MatchFound,
                1 => RepositorySearchOutcome.NoMatchFound,
                _ => FailRepositorySearch(
                    standardError,
                    process.ExitCode,
                    out errorMessage),
            };
        }
        catch (Exception ex) when (
            ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            errorMessage = ex.Message;
            return RepositorySearchOutcome.ExecutionError;
        }
    }

    private static RepositorySearchOutcome FailRepositorySearch(
        string standardError,
        int exitCode,
        out string errorMessage)
    {
        errorMessage = string.IsNullOrWhiteSpace(standardError)
            ? $"rg exited with code {exitCode}."
            : standardError;
        return RepositorySearchOutcome.ExecutionError;
    }

    private enum RepositorySearchOutcome
    {
        MatchFound,
        NoMatchFound,
        ExecutionError,
    }
}
