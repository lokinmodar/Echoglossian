// <copyright file="GTranslateTranslatorTargetLanguageTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.LanguagesHandling;
using Echoglossian.Translators;

using GTranslate;

using Xunit;

using PluginEntry = Echoglossian.Echoglossian;

namespace Echoglossian.Tests;

/// <summary>
/// Covers target-language resolution at the GTranslate provider boundary.
/// </summary>
public sealed class GTranslateTranslatorTargetLanguageTests
{
    /// <summary>
    /// Ensures provider target resolution uses the requested method argument
    /// instead of the mutable global selected language.
    /// </summary>
    /// <param name="requestedTargetLanguage">The requested target language code.</param>
    [Theory]
    [InlineData("en")]
    [InlineData("ar")]
    [InlineData("fa")]
    [InlineData("ur")]
    public void ResolveRequestedTargetLanguage_UsesMethodArgumentNotSelectedGlobal(
        string requestedTargetLanguage)
    {
        var previousSelectedLanguage = PluginEntry.SelectedLanguage;

        try
        {
            PluginEntry.SelectedLanguage = new LanguageInfo(
                "ar",
                "Arabic",
                "NotoSansArabic-Medium.ttf",
                string.Empty,
                []);

            var resolved =
                GTranslateTranslator.ResolveRequestedTargetLanguage(
                    requestedTargetLanguage);

            Assert.Equal(
                Language.GetLanguage(requestedTargetLanguage).Name,
                resolved.Name);
        }
        finally
        {
            PluginEntry.SelectedLanguage = previousSelectedLanguage;
        }
    }

    /// <summary>
    /// Ensures empty or whitespace requests fail before any provider call is attempted.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ResolveRequestedTargetLanguage_EmptyCode_ThrowsArgumentException(
        string requestedTargetLanguage)
    {
        Assert.Throws<ArgumentException>(() =>
            GTranslateTranslator.ResolveRequestedTargetLanguage(
                requestedTargetLanguage));
    }
}
