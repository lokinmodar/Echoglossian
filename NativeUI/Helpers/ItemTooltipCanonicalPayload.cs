// <copyright file="ItemTooltipCanonicalPayload.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Security.Cryptography;
using System.Text;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Represents one canonical item-tooltip payload resolved from game data.
/// </summary>
public sealed class ItemTooltipCanonicalPayload
{
    /// <summary>
    ///     Gets or sets the payload schema version.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    ///     Gets or sets the item row identifier.
    /// </summary>
    public uint ItemId { get; set; }

    /// <summary>
    ///     Gets or sets the item icon identifier.
    /// </summary>
    public uint IconId { get; set; }

    /// <summary>
    ///     Gets or sets the item-action row identifier.
    /// </summary>
    public uint ItemActionId { get; set; }

    /// <summary>
    ///     Gets or sets the item-ui-category row identifier.
    /// </summary>
    public uint ItemUiCategoryId { get; set; }

    /// <summary>
    ///     Gets or sets the class-job-category row identifier.
    /// </summary>
    public uint ClassJobCategoryId { get; set; }

    /// <summary>
    ///     Gets or sets the original item name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the original item description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the translated item name.
    /// </summary>
    public string? TranslatedName { get; set; }

    /// <summary>
    ///     Gets or sets the translated item description.
    /// </summary>
    public string? TranslatedDescription { get; set; }

    /// <summary>
    ///     Gets whether the payload has every translated field required for UI usage.
    /// </summary>
    public bool HasCompleteTranslation =>
        !string.IsNullOrWhiteSpace(this.TranslatedName) &&
        (string.IsNullOrWhiteSpace(this.Description) ||
         !string.IsNullOrWhiteSpace(this.TranslatedDescription));

    /// <summary>
    ///     Serializes the payload using a stable JSON shape.
    /// </summary>
    /// <returns>The serialized payload.</returns>
    public string Serialize()
    {
        return JsonConvert.SerializeObject(this);
    }

    /// <summary>
    ///     Computes a stable source-content hash using only original source semantics.
    /// </summary>
    /// <returns>The stable source hash.</returns>
    public string ComputeSourceContentHash()
    {
        var builder = new StringBuilder();
        builder.Append(this.SchemaVersion)
            .Append('|')
            .Append(this.ItemId)
            .Append('|')
            .Append(this.IconId)
            .Append('|')
            .Append(this.ItemActionId)
            .Append('|')
            .Append(this.ItemUiCategoryId)
            .Append('|')
            .Append(this.ClassJobCategoryId)
            .Append('|')
            .Append(this.Name)
            .Append('|')
            .Append(this.Description);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    ///     Builds the fully assembled original tooltip text.
    /// </summary>
    /// <returns>The original tooltip text.</returns>
    public string BuildOriginalTooltipText()
    {
        return BuildTooltipText(this.Name, this.Description);
    }

    /// <summary>
    ///     Builds the fully assembled translated tooltip text when complete.
    /// </summary>
    /// <returns>The translated tooltip text, or <see langword="null" />.</returns>
    public string? BuildTranslatedTooltipText()
    {
        if (!this.HasCompleteTranslation)
        {
            return null;
        }

        return BuildTooltipText(
            this.TranslatedName ?? string.Empty,
            this.TranslatedDescription ?? string.Empty);
    }

    /// <summary>
    ///     Deserializes a canonical item-tooltip payload.
    /// </summary>
    /// <param name="serializedPayload">The serialized payload.</param>
    /// <returns>The deserialized payload, or <see langword="null" />.</returns>
    public static ItemTooltipCanonicalPayload? Deserialize(
        string? serializedPayload)
    {
        if (string.IsNullOrWhiteSpace(serializedPayload))
        {
            return null;
        }

        return JsonConvert.DeserializeObject<ItemTooltipCanonicalPayload>(
            serializedPayload);
    }

    /// <summary>
    ///     Builds one tooltip text block from title and body.
    /// </summary>
    /// <param name="title">The title line.</param>
    /// <param name="body">The body text.</param>
    /// <returns>The combined tooltip text.</returns>
    private static string BuildTooltipText(string title, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return title;
        }

        return $"{title}\n\n{body}";
    }
}
