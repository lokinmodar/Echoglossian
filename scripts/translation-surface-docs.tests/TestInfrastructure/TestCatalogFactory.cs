// <copyright file="TestCatalogFactory.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace TranslationSurfaceDocs.Tests.TestInfrastructure;

/// <summary>
/// Provides deterministic catalog test data for later generator tests.
/// </summary>
internal static class TestCatalogFactory
{
    /// <summary>
    /// Creates a minimal valid catalog with one surface for validation tests.
    /// </summary>
    /// <param name="surfaceId">The stable identifier for the surface.</param>
    /// <param name="translationModel">The translation model description.</param>
    /// <param name="cache">The cache description.</param>
    /// <param name="dbOwner">The persistence owner.</param>
    /// <param name="dbRead">The runtime database read mode.</param>
    /// <param name="dbWrite">The runtime database write mode.</param>
    /// <param name="locales">The declared generated locale outputs.</param>
    /// <param name="docs">The documentation paths for the surface.</param>
    /// <param name="requiredCodeAnchors">The required repository code anchors.</param>
    /// <returns>A deterministic catalog fixture.</returns>
    public static TranslationSurfaceCatalog CreateSingleSurfaceCatalog(
        string surfaceId = "surface",
        string translationModel = "Model",
        string cache = "Cache",
        string dbOwner = "Owner",
        string dbRead = "sync",
        string dbWrite = "async",
        IReadOnlyList<TranslationSurfaceLocale>? locales = null,
        IReadOnlyList<string>? docs = null,
        IReadOnlyList<string>? requiredCodeAnchors = null)
    {
        return new TranslationSurfaceCatalog(
            [new TranslationSurfaceModeFamily(
                "nativeTooltip",
                "Native-tooltip family",
                ["Native UI Translation"])],
            [new TranslationSurfaceSection("section", "Section")],
            locales ?? [],
            [new TranslationSurfaceEntry(
                surfaceId,
                "section",
                surfaceId,
                "TranslateSurface",
                "nativeTooltip",
                "Enabled",
                new Dictionary<string, string> { ["en"] = "Surface note." },
                new TranslationSurfaceRuntime(translationModel, cache, dbOwner, dbRead, dbWrite),
                docs ?? ["docs/translation-surface-catalog.json"],
                requiredCodeAnchors ?? ["PluginRuntimeLog"])])
        {
            GeneratedAtPolicy = "No timestamps.",
        };
    }
}
