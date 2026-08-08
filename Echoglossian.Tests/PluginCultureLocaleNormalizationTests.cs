// <copyright file="PluginCultureLocaleNormalizationTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Globalization;
using System.Reflection;

using Echoglossian.Properties;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Guards the persisted plugin-culture locale mapping used for localized
///     plugin resources.
/// </summary>
public sealed class PluginCultureLocaleNormalizationTests
{
    /// <summary>
    ///     Ensures applying a persisted neutral culture also configures the
    ///     strongly typed resource accessor used throughout the plugin UI.
    /// </summary>
    [Fact]
    public void ApplyPersistedCultureName_SetsStronglyTypedResourceCulture()
    {
        var previousCulture = Resources.Culture;

        try
        {
            var helperType = typeof(Config).Assembly.GetType("Echoglossian.PluginCultureLocaleHelper");
            Assert.NotNull(helperType);

            var method = helperType!.GetMethod(
                "ApplyPersistedCultureName",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var culture = Assert.IsType<CultureInfo>(method!.Invoke(null, ["fr"]));

            Assert.Equal("fr-FR", culture.Name);
            Assert.Equal("fr-FR", Resources.Culture?.Name);

            var resourceSet = Resources.ResourceManager.GetResourceSet(
                culture,
                true,
                false);
            Assert.NotNull(resourceSet);
            Assert.NotNull(resourceSet!.GetString("ConfigWindowTitle"));
        }
        finally
        {
            Resources.Culture = previousCulture;
        }
    }

    /// <summary>
    ///     Ensures legacy neutral culture codes are promoted to the locale-
    ///     specific resource names now used by plugin UI resources.
    /// </summary>
    /// <param name="input">The persisted culture value.</param>
    /// <param name="expected">The normalized culture value.</param>
    [Theory]
    [InlineData("ca", "ca-ES")]
    [InlineData("da", "da-DK")]
    [InlineData("de", "de-DE")]
    [InlineData("el", "el-GR")]
    [InlineData("es", "es-ES")]
    [InlineData("eu", "eu-ES")]
    [InlineData("fr", "fr-FR")]
    [InlineData("it", "it-IT")]
    [InlineData("nl", "nl-NL")]
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
