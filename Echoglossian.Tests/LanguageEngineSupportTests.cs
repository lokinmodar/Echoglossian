// <copyright file="LanguageEngineSupportTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.LanguagesHandling;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers language-to-engine support mapping for broad-coverage LLM engines.
/// </summary>
public class LanguageEngineSupportTests
{
    /// <summary>
    ///     Ensures Claude is treated as a broad-coverage LLM engine and is added to language support lists.
    /// </summary>
    [Fact]
    public void ApplySupportTo_AddsClaudeToSupportedEngines()
    {
        Dictionary<int, LanguageInfo> languages = new()
        {
            [0] = new LanguageInfo("pt-BR", "Portuguese", "NotoSans-Medium.ttf", string.Empty, new List<int>()),
        };

        LanguageEngineSupport.ApplySupportTo(languages);

        Assert.Contains((int)Echoglossian.TransEngines.Claude, languages[0].SupportedEngines!);
    }
}
