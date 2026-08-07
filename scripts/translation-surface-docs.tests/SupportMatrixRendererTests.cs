// <copyright file="SupportMatrixRendererTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using FluentAssertions;
using TranslationSurfaceDocs.Tests.TestInfrastructure;
using Xunit;

namespace TranslationSurfaceDocs.Tests;

/// <summary>
/// Tests localized support-matrix rendering.
/// </summary>
public sealed class SupportMatrixRendererTests
{
    /// <summary>
    /// Ensures the English matrix uses the canonical English headings.
    /// </summary>
    [Fact]
    public void RenderEnglishMatrix_UsesEnglishHeadings()
    {
        TranslationSurfaceCatalog catalog = TestCatalogFactory.CreateSingleSurfaceCatalog();

        string markdown = SupportMatrixRenderer.Render(catalog, "en").Content;

        markdown.Should().Contain("# Translation Surface Support Matrix");
        markdown.Should().Contain("| Surface | Config Toggle | Modes | Notes | Current Release Status |");
    }

    /// <summary>
    /// Ensures the Brazilian Portuguese matrix uses its localized title and note override.
    /// </summary>
    [Fact]
    public void RenderBrazilianPortugueseMatrix_UsesLocalizedTitleAndNoteOverride()
    {
        TranslationSurfaceCatalog catalog = CreateCatalogWithLocalizedNote("pt-BR", "Nota localizada.");

        string markdown = SupportMatrixRenderer.Render(catalog, "pt-BR").Content;

        markdown.Should().Contain("# Matriz de suporte das superfícies de tradução");
        markdown.Should().Contain("Toggle de configuração");
        markdown.Should().Contain("| Família de modos | Modos |");
        markdown.Should().Contain("Nota localizada.");
    }

    /// <summary>
    /// Ensures localized rendering does not silently fall back to English notes.
    /// </summary>
    [Fact]
    public void RenderLocalizedMatrix_WhenCatalogDoesNotPermitEnglishFallback_Throws()
    {
        TranslationSurfaceCatalog catalog = TestCatalogFactory.CreateSingleSurfaceCatalog();

        Action action = () => SupportMatrixRenderer.Render(catalog, "pt-BR");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*does not provide a 'pt-BR' note*");
    }

    private static TranslationSurfaceCatalog CreateCatalogWithLocalizedNote(string locale, string note)
    {
        TranslationSurfaceCatalog source = TestCatalogFactory.CreateSingleSurfaceCatalog();
        TranslationSurfaceEntry surface = source.Surfaces[0] with
        {
            Notes = new Dictionary<string, string>
            {
                ["en"] = "Surface note.",
                [locale] = note,
            },
        };

        return source with
        {
            Surfaces = [surface],
        };
    }
}
