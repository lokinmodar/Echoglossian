// <copyright file="HostedPreviewPluginSessionTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Mock.Hosting;
using FluentAssertions;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Echoglossian.Mock.Tests;

/// <summary>
/// Covers the reusable DalaMock hosted-session bootstrap.
/// </summary>
public sealed class HostedPreviewPluginSessionTests
{
    /// <summary>
    /// Verifies that hosted startup uses only explicitly supplied preview paths.
    /// </summary>
    /// <returns>A task that completes after the hosted session starts.</returns>
    [Fact]
    public async Task StartAsync_uses_explicit_preview_owned_paths()
    {
        using var fixture = PreviewOwnedHostedSessionFixture.Create();

        await using var session = await HostedPreviewPluginSessionFactory.StartAsync(
            fixture.Options);

        session.StateRoot.FullName.Should().Be(fixture.Options.StateRoot.FullName);
        session.PluginSavePath.FullName.Should().Be(fixture.Options.PluginSavePath.FullName);
        session.ConfigPath.FullName.Should().Be(fixture.Options.ConfigPath.FullName);
    }
}

/// <summary>
/// Owns isolated paths for hosted-session tests.
/// </summary>
internal sealed class PreviewOwnedHostedSessionFixture : IDisposable
{
    private PreviewOwnedHostedSessionFixture(DirectoryInfo stateRoot)
    {
        this.StateRoot = stateRoot;
        var pluginSavePath = stateRoot.CreateSubdirectory(".dalamock");
        this.Options = new HostedPreviewPluginOptions(
            stateRoot,
            pluginSavePath,
            new FileInfo(Path.Combine(stateRoot.FullName, "test.json")),
            DatabasePath: null,
            CreateWindow: false);
    }

    /// <summary>
    /// Gets the hosted-session options for the fixture.
    /// </summary>
    public HostedPreviewPluginOptions Options { get; }

    /// <summary>
    /// Gets the fixture's isolated state root.
    /// </summary>
    public DirectoryInfo StateRoot { get; }

    /// <summary>
    /// Creates an isolated fixture.
    /// </summary>
    /// <returns>The created fixture.</returns>
    public static PreviewOwnedHostedSessionFixture Create()
    {
        var stateRoot = new DirectoryInfo(Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Mock.Tests",
            Guid.NewGuid().ToString("N")));
        stateRoot.Create();
        return new PreviewOwnedHostedSessionFixture(stateRoot);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.StateRoot.Exists)
        {
            this.StateRoot.Delete(true);
        }
    }
}
