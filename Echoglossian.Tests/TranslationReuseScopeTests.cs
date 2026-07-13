// <copyright file="TranslationReuseScopeTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;

using Dalamud.Game;
using Dalamud.Plugin.Services;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the source-language, target-language, and engine contract used
///     by persisted translation reuse.
/// </summary>
public class TranslationReuseScopeTests
{
    /// <summary>
    ///     Ensures legacy source names match their equivalent persisted code.
    /// </summary>
    /// <param name="storedSource">The legacy source name stored in a row.</param>
    /// <param name="requestedSource">The requested canonical source code.</param>
    [Theory]
    [InlineData("English", "en")]
    [InlineData("Deutsch", "de")]
    [InlineData("Japanese", "ja")]
    [InlineData("French", "fr")]
    public void Matches_LegacyStoredSourceName_AcceptsEquivalentCode(
        string storedSource,
        string requestedSource)
    {
        var scope = new TranslationReuseScope(requestedSource, "iw", 4, false);

        Assert.True(scope.Matches(storedSource, "iw", 9));
    }

    /// <summary>
    ///     Ensures source and target mismatches are never reusable.
    /// </summary>
    [Fact]
    public void Matches_DifferentSourceOrTarget_ReturnsFalse()
    {
        var scope = new TranslationReuseScope("ja", "iw", 4, false);

        Assert.False(scope.Matches("en", "iw", 4));
        Assert.False(scope.Matches("ja", "fa", 4));
    }

    /// <summary>
    ///     Ensures distinct persisted Chinese and Traditional Chinese client
    ///     identities cannot cross-reuse translations.
    /// </summary>
    [Fact]
    public void Matches_DistinctExtendedSourceIdentities_ReturnsFalse()
    {
        var scope = new TranslationReuseScope("chs", "iw", 4, false);

        Assert.False(scope.Matches("cht", "iw", 4));
        Assert.False(scope.Matches("tc", "iw", 4));
    }

    /// <summary>
    ///     Ensures retranslation only reuses rows made by the active engine.
    /// </summary>
    [Fact]
    public void Matches_RetranslationEnabled_RequiresActiveEngine()
    {
        var scope = new TranslationReuseScope("en", "iw", 4, true);

        Assert.False(scope.Matches("en", "iw", 7));
    }

    /// <summary>
    ///     Ensures known raw client values retain their persisted source
    ///     identities while exposing provider-supported language codes.
    /// </summary>
    /// <param name="rawClientLanguage">The raw client language value.</param>
    /// <param name="expectedPersistenceCode">The expected stored identity.</param>
    /// <param name="expectedProviderCode">The expected provider input code.</param>
    [Theory]
    [InlineData(0, "ja", "ja")]
    [InlineData(1, "en", "en")]
    [InlineData(2, "de", "de")]
    [InlineData(3, "fr", "fr")]
    [InlineData(4, "chs", "zh-CN")]
    [InlineData(5, "cht", "zh-CN")]
    [InlineData(6, "ko", "ko")]
    [InlineData(7, "tc", "zh-TW")]
    public void TryResolveSourceLanguage_KnownClientValue_ReturnsDistinctIdentity(
        int rawClientLanguage,
        string expectedPersistenceCode,
        string expectedProviderCode)
    {
        var resolved = RuntimeLanguageHelper.TryResolveSourceLanguage(
            (ClientLanguage)rawClientLanguage,
            out var sourceLanguage);

        Assert.True(resolved);
        Assert.Equal(expectedPersistenceCode, sourceLanguage.PersistenceCode);
        Assert.Equal(expectedProviderCode, sourceLanguage.ProviderCode);
    }

    /// <summary>
    ///     Ensures client values without a known source identity fail closed.
    /// </summary>
    [Fact]
    public void TryResolveSourceLanguage_UnknownClientValue_ReturnsFalse()
    {
        var resolved = RuntimeLanguageHelper.TryResolveSourceLanguage(
            (ClientLanguage)99,
            out _);

        Assert.False(resolved);
    }

    /// <summary>
    ///     Ensures callers retaining the legacy string return type fail closed
    ///     when the active client language cannot be resolved.
    /// </summary>
    [Fact]
    public void GetCurrentGameLanguageCode_UnresolvedClientLanguage_ReturnsEmpty()
    {
        var originalClientState = global::Echoglossian.Echoglossian.ClientStateInterface;

        try
        {
            global::Echoglossian.Echoglossian.ClientStateInterface =
                CreateClientState((ClientLanguage)99);

            var languageCode = RuntimeLanguageHelper.GetCurrentGameLanguageCode();

            Assert.Equal(string.Empty, languageCode);
        }
        finally
        {
            global::Echoglossian.Echoglossian.ClientStateInterface = originalClientState;
        }
    }

    /// <summary>
    ///     Creates a client-state proxy that supplies one client-language
    ///     value without requiring a live Dalamud runtime.
    /// </summary>
    /// <param name="clientLanguage">The client language returned by the proxy.</param>
    /// <returns>The configured client-state proxy.</returns>
    private static IClientState CreateClientState(ClientLanguage clientLanguage)
    {
        var clientState = DispatchProxy.Create<IClientState, ClientStateProxy>();
        ((ClientStateProxy)(object)clientState).ClientLanguage = clientLanguage;
        return clientState;
    }

    /// <summary>
    ///     Supplies the client-language member required by this focused test.
    /// </summary>
    private class ClientStateProxy : DispatchProxy
    {
        /// <summary>
        ///     Gets or sets the client language returned by the proxy.
        /// </summary>
        public ClientLanguage ClientLanguage { get; set; }

        /// <summary>
        ///     Dispatches the client-language getter used by the helper.
        /// </summary>
        /// <param name="targetMethod">The invoked interface method.</param>
        /// <param name="args">The invoked method arguments.</param>
        /// <returns>The configured client language.</returns>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "get_ClientLanguage")
            {
                return this.ClientLanguage;
            }

            throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
