// <copyright file="GeneratedDocumentWriter.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace TranslationSurfaceDocs;

/// <summary>
/// Writes generated documentation artifacts to the repository.
/// </summary>
internal static class GeneratedDocumentWriter
{
    /// <summary>
    /// Writes every generated document under the repository root.
    /// </summary>
    /// <param name="repoRoot">The repository root directory.</param>
    /// <param name="documents">The generated documents to write.</param>
    public static void WriteAll(string repoRoot, IReadOnlyList<GeneratedDocument> documents)
    {
        foreach (GeneratedDocument document in documents)
        {
            string outputPath = Path.Combine(repoRoot, document.RelativePath);
            string? outputDirectory = Path.GetDirectoryName(outputPath);
            if (outputDirectory is not null)
            {
                Directory.CreateDirectory(outputDirectory);
            }

            File.WriteAllText(outputPath, document.Content);
        }
    }
}
