// <copyright file="TranslationSurfaceDocsRunner.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Text.Json;

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
        _ = TranslationSurfaceCatalogLoader.Load(options.RepoRoot, options.CatalogPath);
        return options.ValidateOnly
            ? Array.Empty<GeneratedDocument>()
            : throw new NotImplementedException("Rendering is added in later tasks.");
    }
}

/// <summary>
/// Represents one generated repository document.
/// </summary>
/// <param name="RelativePath">The document path relative to the repository root.</param>
/// <param name="Content">The generated document content.</param>
internal sealed record GeneratedDocument(string RelativePath, string Content);

/// <summary>
/// Loads the canonical catalog while detailed schema validation is added later.
/// </summary>
internal static class TranslationSurfaceCatalogLoader
{
    /// <summary>
    /// Parses the canonical catalog from disk.
    /// </summary>
    /// <param name="repoRoot">The repository root associated with the catalog.</param>
    /// <param name="catalogPath">The absolute catalog path.</param>
    /// <returns>The parsed JSON catalog.</returns>
    public static JsonDocument Load(string repoRoot, string catalogPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogPath);

        return JsonDocument.Parse(File.ReadAllText(catalogPath));
    }
}
