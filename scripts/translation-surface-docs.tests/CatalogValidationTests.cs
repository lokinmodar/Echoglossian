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
    /// Ensures rooted and parent-traversal documentation paths are rejected.
    /// </summary>
    [Fact]
    public void Validate_WhenDocPathIsRootedOrEscapesRepo_ReturnsInvalidPathIssue()
    {
        string repoRoot = TestRepositoryPaths.ResolveRepoRoot();
        string rootedExternalDocument = Path.GetTempFileName();
        string parentExternalDocument = Path.Combine(
            Directory.GetParent(repoRoot)!.FullName,
            $"translation-surface-catalog-{Guid.NewGuid():N}.md");
        File.WriteAllText(parentExternalDocument, "external");

        try
        {
            TranslationSurfaceCatalog rootedCatalog =
                TestCatalogFactory.CreateSingleSurfaceCatalog(
                    docs: [rootedExternalDocument]);
            TranslationSurfaceCatalog escapingCatalog =
                TestCatalogFactory.CreateSingleSurfaceCatalog(
                    docs: [Path.GetRelativePath(repoRoot, parentExternalDocument)]);

            TranslationSurfaceCatalogValidator.Validate(rootedCatalog, repoRoot)
                .Should()
                .ContainSingle(issue => issue.Code == "invalid-doc-reference-path");
            TranslationSurfaceCatalogValidator.Validate(escapingCatalog, repoRoot)
                .Should()
                .ContainSingle(issue => issue.Code == "invalid-doc-reference-path");
        }
        finally
        {
            File.Delete(rootedExternalDocument);
            File.Delete(parentExternalDocument);
        }
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
    /// Ensures every surface declares at least one required code anchor.
    /// </summary>
    [Fact]
    public void Validate_WhenSurfaceDeclaresNoRequiredCodeAnchors_ReturnsIssue()
    {
        TranslationSurfaceCatalog catalog = TestCatalogFactory.CreateSingleSurfaceCatalog(
            requiredCodeAnchors: []);

        IReadOnlyList<ValidationIssue> issues =
            TranslationSurfaceCatalogValidator.Validate(
                catalog,
                TestRepositoryPaths.ResolveRepoRoot());

        issues.Should().ContainSingle(
            issue => issue.Code == "missing-required-code-anchor-declaration");
    }

    /// <summary>
    /// Ensures locale output paths stay under the repository root.
    /// </summary>
    [Fact]
    public void Validate_WhenLocaleOutputPathEscapesRepo_ReturnsIssue()
    {
        string repoRoot = TestRepositoryPaths.ResolveRepoRoot();
        TranslationSurfaceCatalog catalog = TestCatalogFactory.CreateSingleSurfaceCatalog(
            locales:
            [
                new TranslationSurfaceLocale(
                    "pt-BR",
                    "..\\..\\outside.md",
                    AllowEnglishNoteFallback: true),
            ]);

        IReadOnlyList<ValidationIssue> issues =
            TranslationSurfaceCatalogValidator.Validate(catalog, repoRoot);

        issues.Should().ContainSingle(
            issue => issue.Code == "invalid-locale-output-path");
    }

    /// <summary>
    /// Ensures declared locales have corresponding label resources.
    /// </summary>
    [Fact]
    public void Validate_WhenLocaleResourcesAreMissing_ReturnsIssue()
    {
        TranslationSurfaceCatalog catalog = TestCatalogFactory.CreateSingleSurfaceCatalog(
            locales:
            [
                new TranslationSurfaceLocale(
                    "xx-test",
                    "docs/translation-surface-support-matrix.xx-test.md",
                    AllowEnglishNoteFallback: true),
            ]);

        IReadOnlyList<ValidationIssue> issues =
            TranslationSurfaceCatalogValidator.Validate(
                catalog,
                TestRepositoryPaths.ResolveRepoRoot());

        issues.Should().ContainSingle(issue => issue.Code == "missing-locale-resources");
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
