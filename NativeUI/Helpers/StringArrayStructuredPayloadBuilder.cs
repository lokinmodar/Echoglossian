// <copyright file="StringArrayStructuredPayloadBuilder.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Builds canonical <see cref="StringArrayStructuredPayload" /> instances
///     from typed slot schemas.
/// </summary>
public static class StringArrayStructuredPayloadBuilder
{
    /// <summary>
    ///     Builds one canonical payload from a typed schema and captured slot
    ///     values.
    /// </summary>
    /// <param name="schema">The typed schema.</param>
    /// <param name="contextKey">The semantic context key for this payload.</param>
    /// <param name="slotTexts">The captured slot texts keyed by array index.</param>
    /// <returns>The canonical structured payload.</returns>
    public static StringArrayStructuredPayload Build(
        IStringArrayStructuredSchema schema,
        string contextKey,
        IReadOnlyDictionary<int, string?> slotTexts)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(contextKey);
        ArgumentNullException.ThrowIfNull(slotTexts);

        var payload = new StringArrayStructuredPayload
        {
            Type = schema.Type,
            ContextKey = contextKey,
            SchemaVersion = schema.SchemaVersion,
        };

        foreach (var pair in slotTexts.OrderBy(pair => pair.Key))
        {
            if (!schema.TryDescribeSlot(
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
                IsTranslatable = description.IsTranslatable,
            };
        }

        return payload;
    }
}
