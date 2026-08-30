// <copyright file="D17PrBodyRenderer.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Text;
using System.Text.RegularExpressions;

namespace D17PrBody;

/// <summary>
/// Renders and validates the repo-approved official D17 PR body format.
/// </summary>
public static partial class D17PrBodyRenderer
{
    private static readonly HashSet<string> AllowedDisclosureLevels = new(StringComparer.Ordinal)
    {
        "Assist",
        "Pair",
        "Copilot",
        "Auto",
    };

    /// <summary>
    /// Validates structured input and renders the final Markdown body.
    /// </summary>
    /// <param name="options">The structured PR body options.</param>
    /// <returns>The rendered Markdown body.</returns>
    public static string Render(D17PrBodyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        ValidateRequiredValue(options.Version, nameof(options.Version));
        ValidateRequiredValue(options.EchoglossianPrUrl, nameof(options.EchoglossianPrUrl));
        ValidateRequiredValue(options.ReleaseTagUrl, nameof(options.ReleaseTagUrl));
        ValidateUrl(options.EchoglossianPrUrl, nameof(options.EchoglossianPrUrl));
        ValidateUrl(options.ReleaseTagUrl, nameof(options.ReleaseTagUrl));

        foreach (string issueUrl in options.IssueUrls)
        {
            ValidateUrl(issueUrl, nameof(options.IssueUrls));
        }

        ValidateLines(options.SummaryLines, nameof(options.SummaryLines));
        ValidateLines(options.ValidationLines, nameof(options.ValidationLines));
        ValidateLines(options.AiScopeLines, nameof(options.AiScopeLines));
        ValidateLines(options.HumanVerificationLines, nameof(options.HumanVerificationLines));
        ValidateLines(options.AssetDisclosureLines, nameof(options.AssetDisclosureLines));

        if (options.AiDisclosureLevel is not null &&
            !AllowedDisclosureLevels.Contains(options.AiDisclosureLevel))
        {
            throw new ArgumentException(
                $"Unsupported AI disclosure level '{options.AiDisclosureLevel}'.",
                nameof(options.AiDisclosureLevel));
        }

        var builder = new StringBuilder();
        AppendSectionLabel(builder, "Summary");
        AppendBullet(builder, $"update `stable/Echoglossian` to `{options.Version}`");
        foreach (string summaryLine in options.SummaryLines)
        {
            AppendBullet(builder, summaryLine);
        }

        if (options.ValidationLines.Count > 0)
        {
            builder.AppendLine();
            AppendSectionLabel(builder, "Validation");
            foreach (string validationLine in options.ValidationLines)
            {
                AppendBullet(builder, validationLine);
            }
        }

        builder.AppendLine();
        AppendSectionLabel(builder, "Source Links");
        AppendBullet(builder, $"Echoglossian PR: {options.EchoglossianPrUrl}");
        AppendBullet(builder, $"Release tag: {options.ReleaseTagUrl}");
        foreach (string issueUrl in options.IssueUrls)
        {
            AppendBullet(builder, $"Issue: {issueUrl}");
        }

        if (options.AiDisclosureLevel is not null)
        {
            builder.AppendLine();
            AppendSectionLabel(builder, "AI Usage Disclosure");
            builder.Append('`').Append(options.AiDisclosureLevel).AppendLine("`");
            builder.AppendLine();
            builder.AppendLine("```text");
            builder.AppendLine("AI scope:");
            AppendCodeBlockBullets(
                builder,
                options.AiScopeLines,
                "add scope details");
            builder.AppendLine("Human verification:");
            AppendCodeBlockBullets(
                builder,
                options.HumanVerificationLines,
                "add verification details");
            builder.AppendLine("```");
        }

        if (options.IncludeAssetDisclosure || options.AssetDisclosureLines.Count > 0)
        {
            builder.AppendLine();
            AppendSectionLabel(builder, "AI-Generated Assets Disclosure");
            builder.AppendLine("```text");
            builder.AppendLine("AI-generated assets:");
            AppendCodeBlockBullets(
                builder,
                options.AssetDisclosureLines,
                "none");
            builder.AppendLine("```");
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendSectionLabel(StringBuilder builder, string label)
    {
        builder.Append("**").Append(label).AppendLine("**");
    }

    private static void AppendBullet(StringBuilder builder, string line)
    {
        builder.Append("- ").AppendLine(line);
    }

    private static void AppendCodeBlockBullets(
        StringBuilder builder,
        IReadOnlyList<string> lines,
        string fallbackLine)
    {
        if (lines.Count == 0)
        {
            AppendBullet(builder, fallbackLine);
            return;
        }

        foreach (string line in lines)
        {
            AppendBullet(builder, line);
        }
    }

    private static void ValidateRequiredValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }
    }

    private static void ValidateLines(IReadOnlyList<string> lines, string parameterName)
    {
        foreach (string line in lines)
        {
            ValidateLine(line, parameterName);
        }
    }

    private static void ValidateLine(string line, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            throw new ArgumentException("Lines must not be empty.", parameterName);
        }

        if (line.Contains('\r') || line.Contains('\n'))
        {
            throw new ArgumentException("Lines must be single-line values.", parameterName);
        }

        if (TaskListPattern().IsMatch(line))
        {
            throw new ArgumentException(
                "GitHub task-list syntax is not allowed in official D17 PR text.",
                parameterName);
        }

        if (MaskedLinkPattern().IsMatch(line))
        {
            throw new ArgumentException(
                "Masked Markdown links are not allowed. Use raw URLs instead.",
                parameterName);
        }

        if (DisallowedHtmlPattern().IsMatch(line))
        {
            throw new ArgumentException(
                "HTML and collapsible details blocks are not allowed in official D17 PR text.",
                parameterName);
        }
    }

    private static void ValidateUrl(string url, string parameterName)
    {
        ValidateLine(url, parameterName);
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("A valid absolute HTTP(S) URL is required.", parameterName);
        }
    }

    [GeneratedRegex(@"^\s*-\s*\[(?: |x|X)\]")]
    private static partial Regex TaskListPattern();

    [GeneratedRegex(@"\[[^\]]+\]\(https?://[^)]+\)")]
    private static partial Regex MaskedLinkPattern();

    [GeneratedRegex(@"<\s*/?\s*(details|summary|table|tr|td|th)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DisallowedHtmlPattern();
}
