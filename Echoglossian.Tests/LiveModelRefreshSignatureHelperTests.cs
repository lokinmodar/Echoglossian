// <copyright file="LiveModelRefreshSignatureHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.EngineConfigUI;
using FluentAssertions;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers stable live model-refresh signatures for engine configuration
///     UIs.
/// </summary>
public class LiveModelRefreshSignatureHelperTests
{
    /// <summary>
    ///     Ensures sensitive inputs are hashed so raw API keys are not stored
    ///     inside the signature string.
    /// </summary>
    [Fact]
    public void Build_WithSensitiveComponent_HashesRawSecret()
    {
        var signature = LiveModelRefreshSignatureHelper.Build(
            new LiveModelRefreshSignatureComponent(
                "apiKeyHash",
                "  super-secret-key  ",
                Sensitive: true),
            new LiveModelRefreshSignatureComponent(
                "baseUrl",
                " https://example.test/v1 "));

        signature.Should().NotContain("super-secret-key");
        signature.Should().Contain("apiKeyHash=");
        signature.Should().Contain("baseUrl=https://example.test/v1");
    }

    /// <summary>
    ///     Ensures signatures remain stable for equal normalized input and
    ///     change when the secret itself changes.
    /// </summary>
    [Fact]
    public void Build_WithSensitiveComponent_RemainsStableAcrossNormalizedInput()
    {
        var firstSignature = LiveModelRefreshSignatureHelper.Build(
            new LiveModelRefreshSignatureComponent(
                "apiKeyHash",
                "super-secret-key",
                Sensitive: true));
        var secondSignature = LiveModelRefreshSignatureHelper.Build(
            new LiveModelRefreshSignatureComponent(
                "apiKeyHash",
                " super-secret-key ",
                Sensitive: true));
        var differentSignature = LiveModelRefreshSignatureHelper.Build(
            new LiveModelRefreshSignatureComponent(
                "apiKeyHash",
                "different-secret",
                Sensitive: true));

        firstSignature.Should().Be(secondSignature);
        firstSignature.Should().NotBe(differentSignature);
    }
}
