// <copyright file="TranslationActivationGuardTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Xunit;
using Echoglossian.LanguagesHandling;

namespace Echoglossian.Tests;

/// <summary>
/// Covers translation activation blocking rules shared by the UI and runtime
/// safeguard paths.
/// </summary>
public class TranslationActivationGuardTests
{
    /// <summary>
    /// Ensures unsupported languages short-circuit translation activation.
    /// </summary>
    [Fact]
    public void GetBlockReason_UnsupportedLanguage_ReturnsUnsupportedLanguage()
    {
        var config = new Config
        {
            UnsupportedLanguage = true,
        };

        var language = new LanguageInfo(
            "en",
            "English",
            "NotoSansJP-VF-1.ttf",
            string.Empty,
            [ (int)Echoglossian.TransEngines.Google ]);

        var result = TranslationActivationGuard.GetBlockReason(config, language);

        Assert.Equal(
            TranslationActivationGuard.BlockReason.UnsupportedLanguage,
            result);
    }

    /// <summary>
    /// Ensures engines that require missing credentials block activation even
    /// when the language itself is otherwise valid.
    /// </summary>
    [Fact]
    public void GetBlockReason_UnconfiguredEngine_ReturnsEngineConfigurationIncomplete()
    {
        var config = new Config
        {
            ChosenTransEngine = (int)Echoglossian.TransEngines.ChatGPT,
            UnsupportedLanguage = false,
            ChatGptApiKey = string.Empty,
            ChatGPTBaseUrl = string.Empty,
        };

        var language = new LanguageInfo(
            "en",
            "English",
            "NotoSansJP-VF-1.ttf",
            string.Empty,
            [ (int)Echoglossian.TransEngines.ChatGPT ]);

        var result = TranslationActivationGuard.GetBlockReason(config, language);

        Assert.Equal(
            TranslationActivationGuard.BlockReason.EngineConfigurationIncomplete,
            result);
    }
}
