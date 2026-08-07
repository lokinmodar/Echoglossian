// <copyright file="CatalogValidationTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using FluentAssertions;
using TranslationSurfaceDocs;
using TranslationSurfaceDocs.Tests.TestInfrastructure;
using Xunit;

namespace TranslationSurfaceDocs.Tests;

/// <summary>
/// Tests repository-backed validation for translation-surface catalog entries.
/// </summary>
public sealed class CatalogValidationTests
{
    /// <summary>
    /// Ensures unknown database read modes are rejected.
    /// </summary>
    [Fact]
    public void Validate_WhenSurfaceUsesUnknownDbMode_ReturnsIssue()
    {
        TranslationSurfaceCatalog catalog = TestCatalogFactory.CreateSingleSurfaceCatalog(
            dbRead: "later",
            dbWrite: "async");

        IReadOnlyList<ValidationIssue> issues =
            TranslationSurfaceCatalogValidator.Validate(
                catalog,
                TestRepositoryPaths.ResolveRepoRoot());

        issues.Should().ContainSingle(issue => issue.Code == "invalid-db-read-mode");
    }

    /// <summary>
    /// Ensures missing documentation references are rejected.
    /// </summary>
    [Fact]
    public void Validate_WhenReferencedDocIsMissing_ReturnsIssue()
    {
        TranslationSurfaceCatalog catalog = TestCatalogFactory.CreateSingleSurfaceCatalog(
            docs: ["docs/does-not-exist.md"]);

        IReadOnlyList<ValidationIssue> issues =
            TranslationSurfaceCatalogValidator.Validate(
                catalog,
                TestRepositoryPaths.ResolveRepoRoot());

        issues.Should().ContainSingle(issue => issue.Code == "missing-doc-reference");
    }

    /// <summary>
    /// Ensures missing required repository code anchors are rejected.
    /// </summary>
    [Fact]
    public void Validate_WhenRequiredCodeAnchorIsMissing_ReturnsIssue()
    {
        TranslationSurfaceCatalog catalog = TestCatalogFactory.CreateSingleSurfaceCatalog(
            requiredCodeAnchors: ["ThisAnchorDoesNotExist"]);

        IReadOnlyList<ValidationIssue> issues =
            TranslationSurfaceCatalogValidator.Validate(
                catalog,
                TestRepositoryPaths.ResolveRepoRoot());

        issues.Should().ContainSingle(issue => issue.Code == "missing-required-code-anchor");
    }

    /// <summary>
    /// Ensures the checked-in catalog is structurally valid against this repository.
    /// </summary>
    [Fact]
    public void Validate_WhenCanonicalCatalogIsLoaded_ReturnsNoIssues()
    {
        string repoRoot = TestRepositoryPaths.ResolveRepoRoot();
        TranslationSurfaceCatalog catalog = TranslationSurfaceCatalogLoader.Load(
            repoRoot,
            Path.Combine(repoRoot, "docs", "translation-surface-catalog.json"));

        IReadOnlyList<ValidationIssue> issues =
            TranslationSurfaceCatalogValidator.Validate(catalog, repoRoot);

        issues.Should().BeEmpty();
    }
}
