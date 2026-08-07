// <copyright file="Program.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using TranslationSurfaceDocs;

string repoRoot = ResolveRepositoryRoot();
bool validateOnly = args.Contains("--validate-only", StringComparer.Ordinal);
var options = new GenerationOptions(
    RepoRoot: repoRoot,
    CatalogPath: Path.Combine(repoRoot, "docs", "translation-surface-catalog.json"),
    ValidateOnly: validateOnly);

IReadOnlyList<GeneratedDocument> generated = TranslationSurfaceDocsRunner.Generate(options);
Console.WriteLine($"Loaded catalog. Generated {generated.Count} document(s).");

static string ResolveRepositoryRoot()
{
    var current = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "Echoglossian.sln")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    throw new DirectoryNotFoundException("Unable to locate repository root.");
}
