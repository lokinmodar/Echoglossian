// <copyright file="TranslationToggleCommandParserTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Tests;

using Xunit;

/// <summary>
/// Covers parsing for the global translation command surface.
/// </summary>
public sealed class TranslationToggleCommandParserTests
{
    /// <summary>
    /// Verifies supported command forms and whitespace normalization.
    /// </summary>
    /// <param name="arguments">The raw arguments.</param>
    /// <param name="expected">The expected parsed action.</param>
    [Theory]
    [InlineData("", "OpenConfiguration")]
    [InlineData("   ", "OpenConfiguration")]
    [InlineData("translations on", "Enable")]
    [InlineData("TRANSLATIONS OFF", "Disable")]
    [InlineData(" translations   toggle ", "Toggle")]
    public void Parse_SupportedArguments_ReturnsExpectedAction(
        string arguments,
        string expected)
    {
        Assert.Equal(
            Enum.Parse<TranslationToggleCommand>(expected),
            TranslationToggleCommandParser.Parse(arguments));
    }

    /// <summary>
    /// Verifies malformed or unsupported arguments return the usage path.
    /// </summary>
    /// <param name="arguments">The raw invalid arguments.</param>
    [Theory]
    [InlineData("translations")]
    [InlineData("translations maybe")]
    [InlineData("translations on extra")]
    [InlineData("other on")]
    public void Parse_InvalidArguments_ReturnsInvalid(string arguments)
    {
        Assert.Equal(
            TranslationToggleCommand.Invalid,
            TranslationToggleCommandParser.Parse(arguments));
    }
}
