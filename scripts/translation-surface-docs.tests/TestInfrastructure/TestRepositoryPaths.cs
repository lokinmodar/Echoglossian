// <copyright file="TestRepositoryPaths.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace TranslationSurfaceDocs.Tests.TestInfrastructure;

/// <summary>
/// Resolves repository paths for standalone generator tests.
/// </summary>
internal static class TestRepositoryPaths
{
    /// <summary>
    /// Resolves the repository root from the test execution directory.
    /// </summary>
    /// <returns>The absolute repository root path.</returns>
    public static string ResolveRepoRoot()
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
}
