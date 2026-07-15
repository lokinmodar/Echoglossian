// <copyright file="RtlLanguagePresentationPolicyTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.UIOverlays.TextPresentation;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers language-policy decisions for text presentation support.
/// </summary>
public class RtlLanguagePresentationPolicyTests
{
    /// <summary>
    /// Ensures known RTL language ids are not treated as unsupported but still
    /// require overlay-only texture-backed presentation.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(42)]
    [InlineData(78)]
    [InlineData(82)]
    [InlineData(106)]
    [InlineData(108)]
    [InlineData(116)]
    [InlineData(129)]
    [InlineData(144)]
    public void KnownRtlLanguages_AreSupportedOverlayOnly(int languageId)
    {
        Assert.True(LanguagePresentationPolicy.IsRtlLanguage(languageId));
        Assert.True(LanguagePresentationPolicy.ShouldRightAlign(languageId));
        Assert.True(LanguagePresentationPolicy.UsesTexturePresentation(languageId));
        Assert.True(LanguagePresentationPolicy.RequiresOverlayOnly(languageId));
        Assert.False(LanguagePresentationPolicy.IsUnsupportedLanguage(languageId));
    }

    /// <summary>
    /// Ensures a normal LTR language keeps the default policy.
    /// </summary>
    [Fact]
    public void English_IsNotRtlOrOverlayOnly()
    {
        Assert.False(LanguagePresentationPolicy.IsRtlLanguage(28));
        Assert.False(LanguagePresentationPolicy.ShouldRightAlign(28));
        Assert.False(LanguagePresentationPolicy.UsesTexturePresentation(28));
        Assert.False(LanguagePresentationPolicy.RequiresOverlayOnly(28));
        Assert.False(LanguagePresentationPolicy.IsUnsupportedLanguage(28));
    }

    /// <summary>
    /// Ensures script-sensitive legacy languages use the texture path without
    /// forcing RTL alignment semantics.
    /// </summary>
    [Theory]
    [InlineData(6)]
    [InlineData(40)]
    [InlineData(57)]
    public void ScriptSensitiveNonRtlLanguages_UseTextureOverlayOnly(
        int languageId)
    {
        Assert.False(LanguagePresentationPolicy.IsRtlLanguage(languageId));
        Assert.False(LanguagePresentationPolicy.ShouldRightAlign(languageId));
        Assert.True(LanguagePresentationPolicy.UsesTexturePresentation(languageId));
        Assert.True(LanguagePresentationPolicy.RequiresOverlayOnly(languageId));
        Assert.False(LanguagePresentationPolicy.IsUnsupportedLanguage(languageId));
    }

    /// <summary>
    /// Ensures legacy unsupported ids no longer stay hard-blocked now that the
    /// engine matrix and presentation policy are modeled separately.
    /// </summary>
    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(11)]
    [InlineData(13)]
    [InlineData(111)]
    [InlineData(112)]
    public void LegacyUnsupportedLanguages_AreNoLongerHardBlocked(
        int languageId)
    {
        Assert.False(LanguagePresentationPolicy.IsUnsupportedLanguage(languageId));
    }
}
