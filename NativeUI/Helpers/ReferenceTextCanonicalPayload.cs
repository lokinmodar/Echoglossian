// <copyright file="ReferenceTextCanonicalPayload.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Security.Cryptography;
using System.Text;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Represents one canonical reference-text payload resolved from action-
///     adjacent Excel sheets.
/// </summary>
public sealed class ReferenceTextCanonicalPayload
{
    /// <summary>
    ///     Gets or sets the payload schema version.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    ///     Gets or sets the stable sheet-row identifier.
    /// </summary>
    public uint ReferenceId { get; set; }

    /// <summary>
    ///     Gets or sets the linked Action row identifier, when the source sheet
    ///     resolves text through the Action sheet.
    /// </summary>
    public uint? ActionId { get; set; }

    /// <summary>
    ///     Gets or sets the resolved icon identifier, when available.
    /// </summary>
    public uint? IconId { get; set; }

    /// <summary>
    ///     Gets or sets the optional sheet category identifier used by
    ///     <c>MainCommand</c>.
    /// </summary>
    public uint? CategoryId { get; set; }

    /// <summary>
    ///     Gets or sets the optional linked MainCommandCategory row identifier.
    /// </summary>
    public uint? MainCommandCategoryId { get; set; }

    /// <summary>
    ///     Gets or sets the optional sheet-specific <c>Unknown0</c> value.
    /// </summary>
    public uint? Unknown0 { get; set; }

    /// <summary>
    ///     Gets or sets the optional sheet sort identifier.
    /// </summary>
    public uint? SortId { get; set; }

    /// <summary>
    ///     Gets or sets the original visible name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the original visible description, when available.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Gets or sets the translated visible name.
    /// </summary>
    public string? TranslatedName { get; set; }

    /// <summary>
    ///     Gets or sets the translated visible description, when available.
    /// </summary>
    public string? TranslatedDescription { get; set; }

    /// <summary>
    ///     Serializes the payload using a stable JSON shape.
    /// </summary>
    /// <returns>The serialized payload.</returns>
    public string Serialize()
    {
        return JsonConvert.SerializeObject(this);
    }

    /// <summary>
    ///     Computes a stable source-content hash using only original source
    ///     semantics.
    /// </summary>
    /// <returns>The stable source hash.</returns>
    public string ComputeSourceContentHash()
    {
        var builder = new StringBuilder();
        builder.Append(this.SchemaVersion)
            .Append('|')
            .Append(this.ReferenceId)
            .Append('|')
            .Append(this.ActionId?.ToString() ?? string.Empty)
            .Append('|')
            .Append(this.IconId?.ToString() ?? string.Empty)
            .Append('|')
            .Append(this.Name)
            .Append('|')
            .Append(this.Description ?? string.Empty);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    ///     Deserializes one canonical reference-text payload.
    /// </summary>
    /// <param name="serializedPayload">The serialized payload.</param>
    /// <returns>The deserialized payload, or <see langword="null" />.</returns>
    public static ReferenceTextCanonicalPayload? Deserialize(
        string? serializedPayload)
    {
        if (string.IsNullOrWhiteSpace(serializedPayload))
        {
            return null;
        }

        return JsonConvert.DeserializeObject<ReferenceTextCanonicalPayload>(
            serializedPayload);
    }
}
