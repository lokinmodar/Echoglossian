// <copyright file="TooltipConfigCopyTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Xml.Linq;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Guards user-facing tooltip copy against leaking implementation-detail
///     references into the configuration UI.
/// </summary>
public sealed class TooltipConfigCopyTests
{
    /// <summary>
    ///     Ensures the action and item tooltip overlay description does not
    ///     mention internal library names in localized config copy.
    /// </summary>
    /// <param name="resourceFileName">The resource file to verify.</param>
    [Theory]
    [InlineData("Resources.resx")]
    public void ActionAndItemTooltipsOverlayOnlyDescription_DoesNotMentionFfxivClientStructs(
        string resourceFileName)
    {
        var root = FindRepositoryRoot();
        var document = XDocument.Load(Path.Combine(
            root.FullName,
            "Properties",
            resourceFileName));
        var value = document.Root!
            .Elements("data")
            .Single(element => string.Equals(
                (string?)element.Attribute("name"),
                "ActionAndItemTooltipsOverlayOnlyDescription",
                StringComparison.Ordinal))
            .Element("value")!
            .Value;

        Assert.DoesNotContain(
            "FFXIVClientStructs",
            value,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Finds the repository root from the test output directory.
    /// </summary>
    /// <returns>The repository root directory.</returns>
    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Echoglossian.sln")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root.");
    }
}
