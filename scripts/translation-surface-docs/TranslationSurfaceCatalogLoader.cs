// <copyright file="TranslationSurfaceCatalogLoader.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Text.Json;

namespace TranslationSurfaceDocs;

/// <summary>
/// Loads the canonical translation-surface catalog from disk.
/// </summary>
internal static class TranslationSurfaceCatalogLoader
{
    /// <summary>
    /// Parses the canonical catalog from disk.
    /// </summary>
    /// <param name="repoRoot">The repository root associated with the catalog.</param>
    /// <param name="catalogPath">The absolute catalog path.</param>
    /// <returns>The parsed catalog.</returns>
    public static TranslationSurfaceCatalog Load(string repoRoot, string catalogPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogPath);

        return JsonSerializer.Deserialize<TranslationSurfaceCatalog>(
                   File.ReadAllText(catalogPath),
                   new JsonSerializerOptions
                   {
                       PropertyNameCaseInsensitive = true,
                   })
               ?? throw new JsonException("The translation-surface catalog is empty.");
    }
}
