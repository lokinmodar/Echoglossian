// <copyright file="RuntimeMapRendererTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using FluentAssertions;
using TranslationSurfaceDocs.Tests.TestInfrastructure;
using Xunit;

namespace TranslationSurfaceDocs.Tests;

/// <summary>
/// Tests runtime-map document rendering.
/// </summary>
public sealed class RuntimeMapRendererTests
{
    /// <summary>
    /// Ensures Markdown includes the operational runtime fields for MiniTalk.
    /// </summary>
    [Fact]
    public void RenderMarkdown_IncludesOperationalColumnsForMiniTalk()
    {
        TranslationSurfaceCatalog catalog = TestCatalogFactory.CreateSingleSurfaceCatalog(
            surfaceId: "MiniTalk",
            translationModel: "Live capture -> cache or DB lookup -> async translation -> overlay/native publication",
            cache: "Dedicated MiniTalk cache",
            dbOwner: "MiniTalkMessage",
            dbRead: "sync",
            dbWrite: "async");

        string markdown = RuntimeMapRenderer.RenderMarkdown(catalog).Content;

        markdown.Should().Contain("| MiniTalk |");
        markdown.Should().Contain("| sync | async |");
    }

    /// <summary>
    /// Ensures JSON preserves the stable runtime read and write field names.
    /// </summary>
    [Fact]
    public void RenderJson_EmitsStableRuntimeFields()
    {
        TranslationSurfaceCatalog catalog = TestCatalogFactory.CreateSingleSurfaceCatalog();

        string json = RuntimeMapRenderer.RenderJson(catalog).Content;

        json.Should().Contain("\"dbRead\": \"sync\"");
        json.Should().Contain("\"dbWrite\": \"async\"");
    }
}
