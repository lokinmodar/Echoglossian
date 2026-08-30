// <copyright file="Program.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Text;

namespace D17PrBody;

/// <summary>
/// Entry point for rendering the official D17 PR body template.
/// </summary>
public static class Program
{
    /// <summary>
    /// Parses arguments, renders the PR body, and optionally writes it to disk.
    /// </summary>
    /// <param name="args">The process command-line arguments.</param>
    /// <returns>A process exit code.</returns>
    public static int Main(string[] args)
    {
        try
        {
            ParsedArguments parsedArguments = ParseArguments(args);
            string renderedBody = D17PrBodyRenderer.Render(parsedArguments.Options);

            if (parsedArguments.OutputPath is not null)
            {
                File.WriteAllText(
                    parsedArguments.OutputPath,
                    renderedBody + Environment.NewLine,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }

            Console.Out.Write(renderedBody);
            if (!renderedBody.EndsWith(Environment.NewLine, StringComparison.Ordinal))
            {
                Console.Out.WriteLine();
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static ParsedArguments ParseArguments(IReadOnlyList<string> args)
    {
        string? version = null;
        string? echoglossianPrUrl = null;
        string? releaseTagUrl = null;
        string? aiDisclosureLevel = null;
        string? outputPath = null;
        bool includeAssetDisclosure = false;
        var summaryLines = new List<string>();
        var validationLines = new List<string>();
        var issueUrls = new List<string>();
        var aiScopeLines = new List<string>();
        var humanVerificationLines = new List<string>();
        var assetDisclosureLines = new List<string>();

        for (int i = 0; i < args.Count; i++)
        {
            string argument = args[i];
            switch (argument)
            {
                case "--version":
                    version = ReadSingleValue(args, ref i, argument);
                    break;
                case "--summary":
                    summaryLines.Add(ReadSingleValue(args, ref i, argument));
                    break;
                case "--validation":
                    validationLines.Add(ReadSingleValue(args, ref i, argument));
                    break;
                case "--echoglossian-pr-url":
                    echoglossianPrUrl = ReadSingleValue(args, ref i, argument);
                    break;
                case "--release-tag-url":
                    releaseTagUrl = ReadSingleValue(args, ref i, argument);
                    break;
                case "--issue-url":
                    issueUrls.Add(ReadSingleValue(args, ref i, argument));
                    break;
                case "--ai-disclosure-level":
                    aiDisclosureLevel = ReadSingleValue(args, ref i, argument);
                    break;
                case "--ai-scope":
                    aiScopeLines.Add(ReadSingleValue(args, ref i, argument));
                    break;
                case "--human-verification":
                    humanVerificationLines.Add(ReadSingleValue(args, ref i, argument));
                    break;
                case "--include-asset-disclosure":
                    includeAssetDisclosure = true;
                    break;
                case "--asset-disclosure":
                    assetDisclosureLines.Add(ReadSingleValue(args, ref i, argument));
                    break;
                case "--output":
                    outputPath = ReadSingleValue(args, ref i, argument);
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{argument}'.");
            }
        }

        if (version is null)
        {
            throw new ArgumentException("Missing required argument '--version'.");
        }

        if (echoglossianPrUrl is null)
        {
            throw new ArgumentException("Missing required argument '--echoglossian-pr-url'.");
        }

        if (releaseTagUrl is null)
        {
            throw new ArgumentException("Missing required argument '--release-tag-url'.");
        }

        return new ParsedArguments(
            new D17PrBodyOptions(
                version,
                summaryLines,
                validationLines,
                echoglossianPrUrl,
                releaseTagUrl,
                issueUrls,
                aiDisclosureLevel,
                aiScopeLines,
                humanVerificationLines,
                includeAssetDisclosure,
                assetDisclosureLines),
            outputPath);
    }

    private static string ReadSingleValue(IReadOnlyList<string> args, ref int index, string argumentName)
    {
        int valueIndex = index + 1;
        if (valueIndex >= args.Count)
        {
            throw new ArgumentException($"Missing value for '{argumentName}'.");
        }

        index = valueIndex;
        return args[valueIndex];
    }

    private sealed record ParsedArguments(D17PrBodyOptions Options, string? OutputPath);
}
