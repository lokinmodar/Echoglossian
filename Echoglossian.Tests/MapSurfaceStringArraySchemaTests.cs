// <copyright file="MapSurfaceStringArraySchemaTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the structured StringArrayData schema used by map-family
///     surfaces such as AreaMap and _NaviMap.
/// </summary>
public sealed class MapSurfaceStringArraySchemaTests
{
    /// <summary>
    ///     Ensures AreaMap structured payloads keep map identity slots while
    ///     translating only visible user-facing labels.
    /// </summary>
    [Fact]
    public void BuildPayload_keeps_map_identity_and_filters_control_slots()
    {
        var payload = MapSurfaceStringArraySchema.BuildPayload(
            "AreaMap",
            53,
            new Dictionary<int, string?>
            {
                [0] = "ui/map/w1f3/00/w1f300_m",
                [1] = "ui/map/w1f3/00/w1f300m_m",
                [2] = "> Thanalan",
                [3] = "> Eastern Thanalan",
                [4] = "> --",
                [5] = "> --",
                [6] = "...",
                [7] = "Lv. 80 Mind over Manor",
                [8] = "???",
            });

        Assert.Equal("MapSurface", payload.Type);
        Assert.Equal("addon:AreaMap:mapSurface", payload.ContextKey);
        Assert.Equal(1, payload.SchemaVersion);
        Assert.Equal("map:path:0", payload.Slots[0].SemanticKey);
        Assert.False(payload.Slots[0].IsTranslatable);
        Assert.Equal("map:path:1", payload.Slots[1].SemanticKey);
        Assert.False(payload.Slots[1].IsTranslatable);
        Assert.Equal("map:label:2", payload.Slots[2].SemanticKey);
        Assert.True(payload.Slots[2].IsTranslatable);
        Assert.Equal("> Thanalan", payload.Slots[2].OriginalText);
        Assert.True(payload.Slots[3].IsTranslatable);
        Assert.False(payload.Slots[4].IsTranslatable);
        Assert.False(payload.Slots[5].IsTranslatable);
        Assert.False(payload.Slots[6].IsTranslatable);
        Assert.False(payload.Slots[7].IsTranslatable);
        Assert.False(payload.Slots[8].IsTranslatable);
        Assert.Equal(
            [2, 3],
            MapSurfaceStringArraySchema.GetTranslatableSlotIndices(payload));
    }

    /// <summary>
    ///     Ensures visible map labels can be matched back to string-array
    ///     breadcrumb values that use a leading map marker.
    /// </summary>
    [Fact]
    public void NormalizeVisibleMapText_removes_breadcrumb_markers()
    {
        Assert.Equal(
            "Central Shroud",
            MapSurfaceStringArraySchema.NormalizeVisibleMapText(
                "> Central Shroud"));
        Assert.Equal(
            "The Black Shroud",
            MapSurfaceStringArraySchema.NormalizeVisibleMapText(
                "The Black Shroud"));
    }

    /// <summary>
    ///     Ensures AreaMap text-node labels are persisted in the same
    ///     structured payload while control, coordinate, and quest-row text is
    ///     filtered out.
    /// </summary>
    [Fact]
    public void BuildPayload_captures_visible_area_map_text_nodes()
    {
        var payload = MapSurfaceStringArraySchema.BuildPayload(
            "AreaMap",
            53,
            new Dictionary<int, string?>
            {
                [0] = "ui/map/f1f1/00/f1f100_m",
                [1] = "ui/map/f1f1/00/f1f100m_m",
                [2] = "> The Black Shroud",
                [3] = "> Central Shroud",
                [7] = "Lv. 80 Mind over Manor",
            },
            new Dictionary<string, string?>
            {
                [MapSurfaceStringArraySchema.BuildVisibleTextNodeKey(
                    "Bentbranch Meadows")] = "Bentbranch Meadows",
                [MapSurfaceStringArraySchema.BuildVisibleTextNodeKey(
                    "X: 27 Y: 17")] = "X: 27 Y: 17",
                [MapSurfaceStringArraySchema.BuildVisibleTextNodeKey(
                    "Lv. 80 Mind over Manor")] = "Lv. 80 Mind over Manor",
                [MapSurfaceStringArraySchema.BuildVisibleTextNodeKey(
                    "Central Shroud")] = "Central Shroud",
            });

        Assert.Contains(
            payload.TextNodes.Values,
            slot =>
                slot.OriginalText == "Bentbranch Meadows" &&
                slot.IsTranslatable);
        Assert.DoesNotContain(
            payload.TextNodes.Values,
            slot => slot.OriginalText == "X: 27 Y: 17");
        Assert.DoesNotContain(
            payload.TextNodes.Values,
            slot => slot.OriginalText == "Lv. 80 Mind over Manor");
        Assert.DoesNotContain(
            payload.TextNodes.Values,
            slot => slot.OriginalText == "Central Shroud");
    }

    /// <summary>
    ///     Ensures _NaviMap payloads translate the map label and weather but
    ///     ignore coordinate-only and shared compass arrays.
    /// </summary>
    [Fact]
    public void BuildPayload_translates_navi_map_status_and_weather()
    {
        var payload = MapSurfaceStringArraySchema.BuildPayload(
            "_NaviMap",
            52,
            new Dictionary<int, string?>
            {
                [0] = "ui/map/w1eb/00/w1eb00_m",
                [1] = "Air Force One\nTime remaining: 3:13",
                [2] = "Fair Skies",
                [3] = "X: 5 Y: 6",
            });

        Assert.True(MapSurfaceStringArraySchema.IsMapSurfacePayload(payload));
        Assert.False(payload.Slots[0].IsTranslatable);
        Assert.True(payload.Slots[1].IsTranslatable);
        Assert.True(payload.Slots[2].IsTranslatable);
        Assert.False(payload.Slots[3].IsTranslatable);
        Assert.Equal(
            [1, 2],
            MapSurfaceStringArraySchema.GetTranslatableSlotIndices(payload));
    }

    /// <summary>
    ///     Ensures shared non-map arrays are rejected before they can be queued
    ///     for translation.
    /// </summary>
    [Fact]
    public void BuildPayload_rejects_shared_compass_payloads()
    {
        var payload = MapSurfaceStringArraySchema.BuildPayload(
            "_NaviMap",
            22,
            new Dictionary<int, string?>
            {
                [0] = "Lock/unlock minimap compass.",
                [1] = "X: 5₉ Y: 6₁",
            });

        Assert.False(MapSurfaceStringArraySchema.IsMapSurfacePayload(payload));
        Assert.Empty(MapSurfaceStringArraySchema.GetTranslatableSlotIndices(payload));
    }
}
