// <copyright file="D17PrBodyOptions.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace D17PrBody;

/// <summary>
/// Describes the structured content for a Discord-webhook-safe official D17 PR body.
/// </summary>
/// <param name="Version">The released plugin version.</param>
/// <param name="SummaryLines">Additional summary bullets after the version line.</param>
/// <param name="ValidationLines">Optional validation bullets.</param>
/// <param name="EchoglossianPrUrl">The source Echoglossian PR URL.</param>
/// <param name="ReleaseTagUrl">The GitHub release tag URL.</param>
/// <param name="IssueUrls">Optional related issue URLs.</param>
/// <param name="AiDisclosureLevel">Optional official AI disclosure level.</param>
/// <param name="AiScopeLines">Optional AI scope bullets.</param>
/// <param name="HumanVerificationLines">Optional human verification bullets.</param>
/// <param name="IncludeAssetDisclosure">Whether to emit the asset disclosure section.</param>
/// <param name="AssetDisclosureLines">Optional asset disclosure bullets.</param>
public sealed record D17PrBodyOptions(
    string Version,
    IReadOnlyList<string> SummaryLines,
    IReadOnlyList<string> ValidationLines,
    string EchoglossianPrUrl,
    string ReleaseTagUrl,
    IReadOnlyList<string> IssueUrls,
    string? AiDisclosureLevel,
    IReadOnlyList<string> AiScopeLines,
    IReadOnlyList<string> HumanVerificationLines,
    bool IncludeAssetDisclosure,
    IReadOnlyList<string> AssetDisclosureLines);
