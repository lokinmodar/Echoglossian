// <copyright file="TranslationSurfaceDocsRunner.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace TranslationSurfaceDocs;

/// <summary>
/// Coordinates catalog loading and document generation.
/// </summary>
internal static class TranslationSurfaceDocsRunner
{
    /// <summary>
    /// Loads the catalog and generates documentation when rendering is available.
    /// </summary>
    /// <param name="options">The generation inputs.</param>
    /// <returns>The generated documents.</returns>
    public static IReadOnlyList<GeneratedDocument> Generate(GenerationOptions options)
    {
        TranslationSurfaceCatalog catalog = TranslationSurfaceCatalogLoader.Load(
            options.RepoRoot,
            options.CatalogPath);
        IReadOnlyList<ValidationIssue> issues =
            TranslationSurfaceCatalogValidator.Validate(catalog, options.RepoRoot);
        if (issues.Count > 0)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                issues.Select(issue => $"[{issue.Code}] {issue.Message}")));
        }

        return options.ValidateOnly
            ? Array.Empty<GeneratedDocument>()
            : throw new NotImplementedException("Rendering is added in later tasks.");
    }
}
