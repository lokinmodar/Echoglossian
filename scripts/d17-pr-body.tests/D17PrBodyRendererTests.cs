// <copyright file="D17PrBodyRendererTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using FluentAssertions;
using Xunit;

namespace D17PrBody.Tests;

/// <summary>
/// Covers the supported official D17 PR body rendering rules.
/// </summary>
public sealed class D17PrBodyRendererTests
{
    /// <summary>
    /// Ensures the default rendered body stays within the documented safe subset.
    /// </summary>
    [Fact]
    public void Render_ShouldUseBoldLabelsRawUrlsAndFlatBullets()
    {
        D17PrBodyOptions options = new(
            Version: "v4.2601.0830.1200",
            SummaryLines: ["pull in the merged RTL overlay fix"],
            ValidationLines: ["release build completed"],
            EchoglossianPrUrl: "https://github.com/lokinmodar/Echoglossian/pull/310",
            ReleaseTagUrl: "https://github.com/lokinmodar/Echoglossian/releases/tag/v4.2601.0830.1200",
            IssueUrls: ["https://github.com/lokinmodar/Echoglossian/issues/274"],
            AiDisclosureLevel: null,
            AiScopeLines: [],
            HumanVerificationLines: [],
            IncludeAssetDisclosure: false,
            AssetDisclosureLines: []);

        string rendered = D17PrBodyRenderer.Render(options);

        rendered.Should().Contain("**Summary**");
        rendered.Should().Contain("- update `stable/Echoglossian` to `v4.2601.0830.1200`");
        rendered.Should().Contain("- Echoglossian PR: https://github.com/lokinmodar/Echoglossian/pull/310");
        rendered.Should().Contain("- Issue: https://github.com/lokinmodar/Echoglossian/issues/274");
        rendered.Should().NotContain("[issue #274](");
        rendered.Should().NotContain("- [ ]");
    }

    /// <summary>
    /// Ensures disclosure sections render with deterministic fallback bullets.
    /// </summary>
    [Fact]
    public void Render_ShouldEmitDisclosureSectionsWhenRequested()
    {
        D17PrBodyOptions options = new(
            Version: "v4.2601.0830.1200",
            SummaryLines: [],
            ValidationLines: [],
            EchoglossianPrUrl: "https://github.com/lokinmodar/Echoglossian/pull/310",
            ReleaseTagUrl: "https://github.com/lokinmodar/Echoglossian/releases/tag/v4.2601.0830.1200",
            IssueUrls: [],
            AiDisclosureLevel: "Assist",
            AiScopeLines: ["bounded release-text drafting help"],
            HumanVerificationLines: ["reviewed the final generated text manually"],
            IncludeAssetDisclosure: true,
            AssetDisclosureLines: []);

        string rendered = D17PrBodyRenderer.Render(options);

        rendered.Should().Contain("**AI Usage Disclosure**");
        rendered.Should().Contain("`Assist`");
        rendered.Should().Contain("AI scope:");
        rendered.Should().Contain("- bounded release-text drafting help");
        rendered.Should().Contain("Human verification:");
        rendered.Should().Contain("- reviewed the final generated text manually");
        rendered.Should().Contain("**AI-Generated Assets Disclosure**");
        rendered.Should().Contain("AI-generated assets:");
        rendered.Should().Contain("- none");
    }

    /// <summary>
    /// Rejects unsupported GitHub-only syntax from user-provided bullet content.
    /// </summary>
    [Fact]
    public void Render_ShouldRejectMaskedLinksAndTaskLists()
    {
        D17PrBodyOptions options = new(
            Version: "v4.2601.0830.1200",
            SummaryLines: ["[issue #274](https://github.com/lokinmodar/Echoglossian/issues/274)"],
            ValidationLines: [],
            EchoglossianPrUrl: "https://github.com/lokinmodar/Echoglossian/pull/310",
            ReleaseTagUrl: "https://github.com/lokinmodar/Echoglossian/releases/tag/v4.2601.0830.1200",
            IssueUrls: [],
            AiDisclosureLevel: null,
            AiScopeLines: [],
            HumanVerificationLines: [],
            IncludeAssetDisclosure: false,
            AssetDisclosureLines: []);

        Action act = () => D17PrBodyRenderer.Render(options);

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage("*Masked Markdown links are not allowed*");
    }
}
