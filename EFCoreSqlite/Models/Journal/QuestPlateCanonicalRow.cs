// <copyright file="QuestPlateCanonicalRow.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Newtonsoft.Json.Converters;

namespace Echoglossian.EFCoreSqlite.Models.Journal;

/// <summary>
///     Identifies one canonical quest-text row section inside the persisted
///     quest payload.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum QuestPlateCanonicalRowSection
{
    /// <summary>
    ///     A SEQ / journal summary row.
    /// </summary>
    Summary,

    /// <summary>
    ///     A TODO / objective row.
    /// </summary>
    Objective,

    /// <summary>
    ///     A SYSTEM / system-message row.
    /// </summary>
    System,
}

/// <summary>
///     Represents one canonical persisted quest row with section, row key,
///     ordering, original text, and translated text kept together.
/// </summary>
public sealed class QuestPlateCanonicalRow
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="QuestPlateCanonicalRow" />
    ///     class.
    /// </summary>
    public QuestPlateCanonicalRow()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="QuestPlateCanonicalRow" />
    ///     class.
    /// </summary>
    /// <param name="section">The row section.</param>
    /// <param name="rowKey">The canonical row key.</param>
    /// <param name="originalText">The original row text.</param>
    /// <param name="order">The stable section-relative order.</param>
    /// <param name="isCurrentSequence">Whether this row is the live current SEQ row.</param>
    /// <param name="translatedText">The translated row text when available.</param>
    public QuestPlateCanonicalRow(
        QuestPlateCanonicalRowSection section,
        string rowKey,
        string originalText,
        int order,
        bool isCurrentSequence = false,
        string? translatedText = null)
    {
        this.Section = section;
        this.RowKey = rowKey;
        this.OriginalText = originalText;
        this.Order = order;
        this.IsCurrentSequence = isCurrentSequence;
        this.TranslatedText = translatedText;
    }

    /// <summary>
    ///     Gets or sets the row section.
    /// </summary>
    public QuestPlateCanonicalRowSection Section { get; set; }

    /// <summary>
    ///     Gets or sets the canonical row key.
    /// </summary>
    public string RowKey { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the original row text.
    /// </summary>
    public string OriginalText { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the translated row text.
    /// </summary>
    public string? TranslatedText { get; set; }

    /// <summary>
    ///     Gets or sets the section-relative order.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether this row is the live current
    ///     SEQ row for the payload snapshot.
    /// </summary>
    public bool IsCurrentSequence { get; set; }
}
