// <copyright file="TranslationSurfaceDocsRunnerTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using FluentAssertions;
using TranslationSurfaceDocs;
using TranslationSurfaceDocs.Tests.TestInfrastructure;
using Xunit;

namespace TranslationSurfaceDocs.Tests;

/// <summary>
/// Tests the translation-surface documentation generation entry point.
/// </summary>
public sealed class TranslationSurfaceDocsRunnerTests
{
    /// <summary>
    /// Ensures validation-only mode loads the canonical catalog without producing documents.
    /// </summary>
    [Fact]
    public void Generate_WithCanonicalCatalogAndValidateOnly_ReturnsNoDocuments()
    {
        string repoRoot = TestRepositoryPaths.ResolveRepoRoot();
        var options = new GenerationOptions(
            RepoRoot: repoRoot,
            CatalogPath: Path.Combine(repoRoot, "docs", "translation-surface-catalog.json"),
            ValidateOnly: true);

        IReadOnlyList<GeneratedDocument> generated = TranslationSurfaceDocsRunner.Generate(options);

        generated.Should().BeEmpty();
    }
}
