// <copyright file="TranslationSurfaceDocsRunnerTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using FluentAssertions;
using System.Text.Json;
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

    /// <summary>
    /// Ensures full generation returns both the runtime map and localized support matrices.
    /// </summary>
    [Fact]
    public void Generate_WhenValidateOnlyIsFalse_ReturnsRuntimeMapAndMatrices()
    {
        string repoRoot = TestRepositoryPaths.ResolveRepoRoot();
        var options = new GenerationOptions(
            RepoRoot: repoRoot,
            CatalogPath: Path.Combine(repoRoot, "docs", "translation-surface-catalog.json"),
            ValidateOnly: false);

        IReadOnlyList<GeneratedDocument> generated = TranslationSurfaceDocsRunner.Generate(options);

        generated.Should().Contain(document => document.RelativePath == "docs/translation-surface-runtime-map.md");
        generated.Should().Contain(document => document.RelativePath == "docs/translation-surface-support-matrix.pt-BR.md");
    }

    /// <summary>
    /// Ensures reviewed persistence entities are retained in the canonical catalog.
    /// </summary>
    [Fact]
    public void CanonicalCatalog_UsesDocumentedPersistenceOwners()
    {
        string repoRoot = TestRepositoryPaths.ResolveRepoRoot();
        Dictionary<string, string> owners = LoadCatalogOwners(repoRoot);

        owners.Should().Contain(new Dictionary<string, string>
        {
            ["CutSceneSelectString"] = "CutSceneSelectStringMessage",
            ["YesNo"] = "SelectionDialogText",
            ["SelectOk"] = "SelectionDialogText",
            ["SelectString"] = "SelectString; SelectionDialogText fallback",
            ["SelectIconString"] = "SelectionDialogText",
            ["Journal"] = "QuestPlate",
            ["JournalDetail"] = "QuestPlate",
            ["ToDoList"] = "QuestPlate",
            ["ToDo"] = "ToDoText",
            ["ScenarioTree"] = "QuestPlate",
            ["JournalAccept"] = "QuestPlate; QuestPopupText fallback",
            ["JournalResult"] = "QuestPlate; QuestPopupText fallback",
            ["RecommendList"] = "QuestPlate",
            ["AreaMap"] = "StringArrayData",
            ["WideTextToast"] = "ToastMessage",
            ["ErrorToast"] = "ToastMessage",
            ["AreaToast"] = "ToastMessage",
            ["ClassChangeToast"] = "ToastMessage",
            ["TextGimmickHint"] = "TextGimmickHintMessage",
            ["QuestToast"] = "ToastMessage",
            ["ContextMenu"] = "ContextMenuText",
            ["TooltipAddon"] = "TooltipText",
            ["ActionItemTooltips"] = "ActionTooltip, ItemTooltip, Trait",
            ["NamePlates"] = "NamePlateMessage",
        });
    }

    private static Dictionary<string, string> LoadCatalogOwners(string repoRoot)
    {
        string catalogPath = Path.Combine(repoRoot, "docs", "translation-surface-catalog.json");
        using JsonDocument catalog = JsonDocument.Parse(File.ReadAllText(catalogPath));

        return catalog.RootElement
            .GetProperty("surfaces")
            .EnumerateArray()
            .ToDictionary(
                surface => surface.GetProperty("id").GetString()!,
                surface => surface.GetProperty("runtime").GetProperty("dbOwner").GetString()!);
    }
}
