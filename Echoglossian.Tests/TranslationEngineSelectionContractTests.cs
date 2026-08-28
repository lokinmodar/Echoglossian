// <copyright file="TranslationEngineSelectionContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Guards source-level startup contracts around translation-engine migration.
/// </summary>
public sealed class TranslationEngineSelectionContractTests
{
    /// <summary>
    /// Ensures startup translation-engine migration reads the plugin-owned
    /// language dictionary instead of the static global mirror so it remains
    /// safe before target-language synchronization runs.
    /// </summary>
    [Fact]
    public void MigrateTranslationEngineSelection_UsesInstanceLanguageDictionary()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "GeneralHelpers",
            "Utils.cs"));
        var methodBody = ExtractMethodBody(
            source,
            "public void MigrateTranslationEngineSelection(int loadedConfigVersion)");

        Assert.Contains(
            "this.languagesDictionary.TryGetValue(this.configuration.Lang, out var language)",
            methodBody,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "LangDict.TryGetValue(this.configuration.Lang, out var language)",
            methodBody,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Finds the repository root from the current test output directory.
    /// </summary>
    /// <returns>The absolute repository-root path.</returns>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Echoglossian.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the Echoglossian repository root.");
    }

    /// <summary>
    /// Extracts one method body, including braces, from a source file.
    /// </summary>
    /// <param name="source">The source file text.</param>
    /// <param name="signature">The method signature to locate.</param>
    /// <returns>The extracted method body.</returns>
    private static string ExtractMethodBody(string source, string signature)
    {
        var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Could not find method signature: {signature}");

        var bodyStart = source.IndexOf('{', signatureIndex);
        Assert.True(bodyStart >= 0, $"Could not find body start for: {signature}");

        var depth = 0;
        for (var index = bodyStart; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(bodyStart, (index - bodyStart) + 1);
                }
            }
        }

        throw new InvalidOperationException(
            $"Could not find body end for: {signature}");
    }
}
