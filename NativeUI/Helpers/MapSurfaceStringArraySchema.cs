// <copyright file="MapSurfaceStringArraySchema.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Defines the canonical structured payload for map-family
///     <c>StringArrayData</c> surfaces.
/// </summary>
public sealed class MapSurfaceStringArraySchema : IStringArrayStructuredSchema
{
    private const string TypeName = "MapSurface";

    private static readonly Regex CoordinatePattern = new(
        @"^\s*X:\s*\S+(?:\s+\S+)?\s+Y:\s*\S+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    ///     Gets the shared map-surface schema instance.
    /// </summary>
    public static MapSurfaceStringArraySchema Instance { get; } = new();

    /// <inheritdoc />
    public string Type => TypeName;

    /// <inheritdoc />
    public int SchemaVersion => 1;

    /// <summary>
    ///     Builds a canonical payload for one map-family string array.
    /// </summary>
    /// <param name="addonName">The owning addon name.</param>
    /// <param name="arrayIndex">The live string-array index.</param>
    /// <param name="slotTexts">The captured slot values.</param>
    /// <returns>The canonical structured payload.</returns>
    public static StringArrayStructuredPayload BuildPayload(
        string addonName,
        int arrayIndex,
        IReadOnlyDictionary<int, string?> slotTexts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addonName);
        ArgumentNullException.ThrowIfNull(slotTexts);

        var hasMapPath = slotTexts.Values.Any(IsMapResourcePath);
        var payload = new StringArrayStructuredPayload
        {
            Type = TypeName,
            ContextKey = BuildContextKey(addonName),
            SchemaVersion = Instance.SchemaVersion,
        };

        foreach (var pair in slotTexts.OrderBy(pair => pair.Key))
        {
            if (!Instance.TryDescribeSlot(
                    pair.Key,
                    pair.Value,
                    out var description))
            {
                continue;
            }

            payload.Slots[pair.Key] = new StringArrayStructuredSlot
            {
                SemanticKey = description.SemanticKey,
                OriginalText = pair.Value ?? string.Empty,
                IsVisible = description.IsVisible,
                IsTranslatable =
                    hasMapPath &&
                    description.IsTranslatable,
            };
        }

        return payload;
    }

    /// <summary>
    ///     Builds the stable context key for one map-surface addon.
    /// </summary>
    /// <param name="addonName">The owning addon name.</param>
    /// <returns>The canonical context key.</returns>
    public static string BuildContextKey(string addonName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addonName);
        return $"addon:{addonName}:mapSurface";
    }

    /// <summary>
    ///     Determines whether one payload is a usable map-surface payload.
    /// </summary>
    /// <param name="payload">The payload to inspect.</param>
    /// <returns>
    ///     <see langword="true" /> when the payload carries map identity and
    ///     at least one translatable user-facing slot.
    /// </returns>
    public static bool IsMapSurfacePayload(StringArrayStructuredPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return string.Equals(payload.Type, TypeName, StringComparison.Ordinal) &&
               payload.Slots.Values.Any(slot =>
                   slot.SemanticKey.StartsWith(
                       "map:path:",
                       StringComparison.Ordinal)) &&
               payload.Slots.Values.Any(slot => slot.IsTranslatable);
    }

    /// <summary>
    ///     Returns the translatable slot indices from one map payload.
    /// </summary>
    /// <param name="payload">The payload to inspect.</param>
    /// <returns>The translatable slot indices.</returns>
    public static IReadOnlyList<int> GetTranslatableSlotIndices(
        StringArrayStructuredPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return payload.Slots
            .Where(pair => pair.Value.IsTranslatable)
            .Select(pair => pair.Key)
            .ToArray();
    }

    /// <inheritdoc />
    public bool TryDescribeSlot(
        int slotIndex,
        string? slotText,
        out StringArrayStructuredSlotDescription description)
    {
        if (string.IsNullOrWhiteSpace(slotText))
        {
            description = new StringArrayStructuredSlotDescription(
                $"map:empty:{slotIndex.ToString(CultureInfo.InvariantCulture)}",
                IsVisible: false,
                IsTranslatable: false);
            return false;
        }

        description = new StringArrayStructuredSlotDescription(
            BuildSemanticKey(slotIndex, slotText),
            IsVisible: true,
            IsTranslatable: ShouldTranslateMapText(slotText));
        return true;
    }

    /// <summary>
    ///     Determines whether one string-array value is user-facing map text.
    /// </summary>
    /// <param name="text">The captured text.</param>
    /// <returns><c>true</c> when the value should be translated.</returns>
    internal static bool ShouldTranslateMapText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            IsMapResourcePath(text) ||
            CoordinatePattern.IsMatch(text))
        {
            return false;
        }

        var trimmed = StripMapMarker(text).Trim();
        return !string.IsNullOrWhiteSpace(trimmed) &&
               !string.Equals(trimmed, "--", StringComparison.Ordinal) &&
               !string.Equals(trimmed, "---", StringComparison.Ordinal) &&
               !string.Equals(trimmed, "...", StringComparison.Ordinal) &&
               !string.Equals(trimmed, "???", StringComparison.Ordinal) &&
               !trimmed.All(char.IsPunctuation);
    }

    private static string BuildSemanticKey(int slotIndex, string slotText)
    {
        var prefix = IsMapResourcePath(slotText)
            ? "path"
            : ShouldTranslateMapText(slotText)
                ? "label"
                : "control";
        return $"map:{prefix}:{slotIndex.ToString(CultureInfo.InvariantCulture)}";
    }

    private static bool IsMapResourcePath(string? text)
    {
        return !string.IsNullOrWhiteSpace(text) &&
               text.Trim().StartsWith("ui/map/", StringComparison.Ordinal);
    }

    private static string StripMapMarker(string text)
    {
        var trimmed = text.Trim();
        return trimmed.StartsWith('>')
            ? trimmed[1..].TrimStart()
            : trimmed;
    }
}
