// <copyright file="TargetLanguageRuntimeStateTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.LanguagesHandling;

using Xunit;

using PluginEntry = Echoglossian.Echoglossian;

namespace Echoglossian.Tests;

/// <summary>
/// Covers synchronization of runtime target-language state derived from the
/// persisted configuration language.
/// </summary>
public sealed class TargetLanguageRuntimeStateTests
{
    /// <summary>
    /// Ensures Arabic configuration repairs stale runtime mirrors and applies
    /// the current overlay presentation flags.
    /// </summary>
    [Fact]
    public void Synchronize_ArabicConfiguration_RepairsStaleLegacyMirrors()
    {
        var previousLanguageInt = PluginEntry.LanguageInt;
        var previousSelectedLanguage = PluginEntry.SelectedLanguage;
        var previousLanguages = PluginEntry.LangDict;

        try
        {
            var languages = PluginEntry.CreateLanguagesDictionary();
            var configuration = new Config { Lang = 2 };
            PluginEntry.LanguageInt = 28;
            PluginEntry.SelectedLanguage = languages[28];

            var selected = TargetLanguageRuntimeState.Synchronize(
                configuration,
                languages);

            Assert.Same(languages[2], selected);
            Assert.Equal(2, PluginEntry.LanguageInt);
            Assert.Same(languages[2], PluginEntry.SelectedLanguage);
            Assert.Same(languages, PluginEntry.LangDict);
            Assert.True(configuration.OverlayOnlyLanguage);
            Assert.False(configuration.UnsupportedLanguage);
        }
        finally
        {
            PluginEntry.LanguageInt = previousLanguageInt;
            PluginEntry.SelectedLanguage = previousSelectedLanguage;
            PluginEntry.LangDict = previousLanguages;
        }
    }

    /// <summary>
    /// Ensures an unknown language identifier fails before mutating any legacy
    /// runtime mirrors.
    /// </summary>
    [Fact]
    public void Synchronize_UnknownLanguage_DoesNotPartiallyMutateLegacyMirrors()
    {
        var previousLanguageInt = PluginEntry.LanguageInt;
        var previousSelectedLanguage = PluginEntry.SelectedLanguage;
        var previousLanguages = PluginEntry.LangDict;

        try
        {
            var languages = PluginEntry.CreateLanguagesDictionary();
            var configuration = new Config { Lang = 999 };
            PluginEntry.LanguageInt = 28;
            PluginEntry.SelectedLanguage = languages[28];
            PluginEntry.LangDict = languages;

            Assert.Throws<KeyNotFoundException>(() =>
                TargetLanguageRuntimeState.Synchronize(
                    configuration,
                    languages));

            Assert.Equal(28, PluginEntry.LanguageInt);
            Assert.Same(languages[28], PluginEntry.SelectedLanguage);
            Assert.Same(languages, PluginEntry.LangDict);
            Assert.False(configuration.OverlayOnlyLanguage);
            Assert.False(configuration.UnsupportedLanguage);
        }
        finally
        {
            PluginEntry.LanguageInt = previousLanguageInt;
            PluginEntry.SelectedLanguage = previousSelectedLanguage;
            PluginEntry.LangDict = previousLanguages;
        }
    }
}
