// <copyright file="MapSurfaceStringArrayHandlerContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Verifies map StringArrayData handler contracts that keep translated map
///     labels visible and safely restorable.
/// </summary>
public sealed class MapSurfaceStringArrayHandlerContractTests
{
    /// <summary>
    ///     Ensures map labels translated from StringArrayData are also applied
    ///     to matching visible text nodes, because AreaMap probes expose both
    ///     the backing array and readable label nodes.
    /// </summary>
    [Fact]
    public void MapSurface_applies_translated_payload_to_visible_text_nodes()
    {
        var source = ReadMapSurfaceHandlerSource();

        Assert.Contains("CaptureVisibleMapTextNodes", source);
        Assert.Contains("ApplyTranslatedTextNodeValues", source);
        Assert.Contains("projection.TextNodes", source);
        Assert.Contains("FindReadableTextNodeAddressesByText", source);
        Assert.Contains("textNode->NodeText.SetString", source);
        Assert.Contains("mapSurfaceTextNodeMutations", source);
    }

    /// <summary>
    ///     Ensures map text-node restoration only writes back when the visible
    ///     native field still contains the plugin-owned translated value.
    /// </summary>
    [Fact]
    public void MapSurface_restores_only_owned_text_node_mutations()
    {
        var source = ReadMapSurfaceHandlerSource();

        Assert.Contains("RestoreTextNodeMutationsIfNeeded", source);
        Assert.Contains("NativeMutationOwnership.TryRestore", source);
    }

    /// <summary>
    ///     Ensures map hover tooltips are anchored to matching text nodes when
    ///     possible and only fall back to the whole addon when no node can be
    ///     matched.
    /// </summary>
    [Fact]
    public void MapSurface_prefers_text_node_tooltips_over_addon_tooltip()
    {
        var source = ReadMapSurfaceHandlerSource();

        Assert.Contains("RegisterTextNodeHoverTooltips", source);
        Assert.Contains("RegisterAggregateHoverTooltip", source);
        Assert.Contains("this.ShouldAllowAggregateHoverTooltip", source);
    }

    /// <summary>
    ///     Ensures dense map addons are not fully rescanned on every PreDraw
    ///     after the payload has already been captured.
    /// </summary>
    [Fact]
    public void MapSurface_uses_predraw_short_circuit_for_stable_payloads()
    {
        var source = ReadMapSurfaceHandlerSource();

        Assert.Contains("lastProcessedPayloadSignature", source);
        Assert.Contains("ShouldSkipStablePreDrawPayload", source);
        Assert.Contains("ArmPreDrawRefreshWindow", source);
        Assert.Contains("AgentMap.Instance()", source);
        Assert.Contains("agentMap->CurrentMapId", source);
        Assert.Contains("agentMap->CurrentTerritoryId", source);
    }

    /// <summary>
    ///     Reads the map StringArrayData handler source.
    /// </summary>
    /// <returns>The handler source text.</returns>
    private static string ReadMapSurfaceHandlerSource()
    {
        var root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "Quest",
            "MapSurfaceStringArrayHandler.cs"));
    }

    /// <summary>
    ///     Finds the repository root from the test output directory.
    /// </summary>
    /// <returns>The repository root directory.</returns>
    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Echoglossian.sln")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root.");
    }
}
