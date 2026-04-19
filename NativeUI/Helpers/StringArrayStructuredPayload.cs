// <copyright file="StringArrayStructuredPayload.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Security.Cryptography;
using System.Text;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Represents one canonical structured payload for a StringArrayData-backed
///     surface.
/// </summary>
public sealed class StringArrayStructuredPayload
{
    /// <summary>
    ///     Gets or sets the backing string-array type or logical surface family.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the stable semantic context key for this payload.
    /// </summary>
    public string ContextKey { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the schema version used by this payload.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    ///     Gets the captured slots keyed by their source array index.
    /// </summary>
    public SortedDictionary<int, StringArrayStructuredSlot> Slots { get; } = [];

    /// <summary>
    ///     Gets the captured visible text nodes keyed by their stable node key.
    /// </summary>
    public SortedDictionary<string, StringArrayStructuredSlot> TextNodes { get; } =
        new(StringComparer.Ordinal);

    /// <summary>
    ///     Serializes the payload using a stable JSON shape.
    /// </summary>
    /// <returns>The serialized payload.</returns>
    public string Serialize()
    {
        return JsonConvert.SerializeObject(this);
    }

    /// <summary>
    ///     Computes the stable source-content hash for the payload using only
    ///     original source semantics.
    /// </summary>
    /// <returns>The stable hash string.</returns>
    public string ComputeSourceContentHash()
    {
        var builder = new StringBuilder();
        builder.Append(this.Type)
            .Append('|')
            .Append(this.ContextKey)
            .Append('|')
            .Append(this.SchemaVersion);

        foreach (var (index, slot) in this.Slots)
        {
            builder.Append('|')
                .Append(index)
                .Append(':')
                .Append(slot.SemanticKey)
                .Append(':')
                .Append(slot.OriginalText);
        }

        foreach (var (key, slot) in this.TextNodes)
        {
            builder.Append('|')
                .Append(key)
                .Append(':')
                .Append(slot.SemanticKey)
                .Append(':')
                .Append(slot.OriginalText);
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    ///     Deserializes a payload from its JSON form.
    /// </summary>
    /// <param name="serializedPayload">The serialized payload.</param>
    /// <returns>The deserialized payload, or <see langword="null" />.</returns>
    public static StringArrayStructuredPayload? Deserialize(
        string? serializedPayload)
    {
        if (string.IsNullOrWhiteSpace(serializedPayload))
        {
            return null;
        }

        return JsonConvert.DeserializeObject<StringArrayStructuredPayload>(
            serializedPayload);
    }
}

/// <summary>
///     Represents one canonical structured slot inside a StringArrayData-backed
///     payload.
/// </summary>
public sealed class StringArrayStructuredSlot
{
    /// <summary>
    ///     Gets or sets the stable semantic key for this slot.
    /// </summary>
    public string SemanticKey { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the original text captured from the game.
    /// </summary>
    public string OriginalText { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the translated text for this slot.
    /// </summary>
    public string? TranslatedText { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the slot is user-visible.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether the slot is translatable.
    /// </summary>
    public bool IsTranslatable { get; set; } = true;
}
