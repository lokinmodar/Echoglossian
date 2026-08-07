// <copyright file="GeneratedDocumentWriterTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using FluentAssertions;
using Xunit;

namespace TranslationSurfaceDocs.Tests;

/// <summary>
/// Tests generated-document writing safety.
/// </summary>
public sealed class GeneratedDocumentWriterTests
{
    /// <summary>
    /// Ensures catalog-controlled output paths cannot escape the repository root.
    /// </summary>
    [Fact]
    public void WriteAll_WhenDocumentPathEscapesRepo_Throws()
    {
        string repoRoot = Path.GetTempPath();
        GeneratedDocument document = new("..\\outside.md", "content");

        Action action = () => GeneratedDocumentWriter.WriteAll(repoRoot, [document]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*escapes repository root*");
    }
}
