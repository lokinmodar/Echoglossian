// <copyright file="GeminiTextModelDefaultsTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.Gemini;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the built-in Gemini model catalog used when live model fetching
///     is disabled.
/// </summary>
public class GeminiTextModelDefaultsTests
{
    /// <summary>
    ///     Ensures the bundled defaults reflect the current Google AI Studio /
    ///     Gemini API text model families instead of the retired
    ///     <c>gemini-pro</c> era identifiers.
    /// </summary>
    [Fact]
    public void PredefinedModels_UseCurrentGoogleAiStudioTextModels()
    {
        var modelIds = GeminiTextModelDefaults.PredefinedModels
            .Select(static model => model.Id)
            .ToArray();

        Assert.Contains("gemini-2.5-flash", modelIds);
        Assert.Contains("gemini-2.5-flash-lite", modelIds);
        Assert.Contains("gemini-2.5-pro", modelIds);
        Assert.DoesNotContain("gemini-pro", modelIds);
        Assert.DoesNotContain("gemini-1.5-pro", modelIds);
        Assert.DoesNotContain("gemini-1.5-flash", modelIds);
    }
}
