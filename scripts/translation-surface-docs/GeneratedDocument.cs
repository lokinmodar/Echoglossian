// <copyright file="GeneratedDocument.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace TranslationSurfaceDocs;

/// <summary>
/// Represents one generated repository document.
/// </summary>
/// <param name="RelativePath">The document path relative to the repository root.</param>
/// <param name="Content">The generated document content.</param>
internal sealed record GeneratedDocument(string RelativePath, string Content);
