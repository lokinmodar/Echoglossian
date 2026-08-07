// <copyright file="GenerationOptions.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace TranslationSurfaceDocs;

/// <summary>
/// Defines the inputs for one documentation generation run.
/// </summary>
/// <param name="RepoRoot">The absolute path to the repository root.</param>
/// <param name="CatalogPath">The absolute path to the canonical catalog.</param>
/// <param name="ValidateOnly">Whether to validate without rendering documents.</param>
internal sealed record GenerationOptions(
    string RepoRoot,
    string CatalogPath,
    bool ValidateOnly);
