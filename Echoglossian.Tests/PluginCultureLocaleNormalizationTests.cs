// <copyright file="PluginCultureLocaleNormalizationTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Guards the persisted plugin-culture locale mapping used for localized
///     plugin resources.
/// </summary>
public sealed class PluginCultureLocaleNormalizationTests
{
    /// <summary>
    ///     Ensures legacy neutral culture codes are promoted to the locale-
    ///     specific resource names now used by plugin UI resources.
    /// </summary>
    /// <param name="input">The persisted culture value.</param>
    /// <param name="expected">The normalized culture value.</param>
    [Theory]
    [InlineData("da", "da-DK")]
    [InlineData("de", "de-DE")]
    [InlineData("el", "el-GR")]
    [InlineData("es", "es-ES")]
    [InlineData("eu", "eu-ES")]
    [InlineData("fr", "fr-FR")]
    [InlineData("it", "it-IT")]
    [InlineData("pt", "pt-PT")]
    [InlineData("pt-BR", "pt-BR")]
    [InlineData("ru", "ru-RU")]
    [InlineData("en", "en")]
    public void NormalizePersistedCultureName_MapsLegacyNeutralCodes(
        string input,
        string expected)
    {
        var helperType = typeof(Config).Assembly.GetType("Echoglossian.PluginCultureLocaleHelper");
        Assert.NotNull(helperType);

        var method = helperType!.GetMethod(
            "NormalizePersistedCultureName",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var actual = Assert.IsType<string>(method!.Invoke(null, [input]));
        Assert.Equal(expected, actual);
    }
}
