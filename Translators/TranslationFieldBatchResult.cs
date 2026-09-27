// <copyright file="TranslationFieldBatchResult.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.ObjectModel;

namespace Echoglossian.Translators;

/// <summary>
///     Represents the complete result of translating named text fields.
/// </summary>
public sealed class TranslationFieldBatchResult
{
    private readonly IReadOnlyDictionary<string, string> translations;

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="TranslationFieldBatchResult" /> class.
    /// </summary>
    /// <param name="fields">The translated fields in their requested order.</param>
    /// <param name="usedIndividualFallback">
    ///     <see langword="true" /> when the batch response was rejected and
    ///     fields were translated individually; otherwise,
    ///     <see langword="false" />.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="fields" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     <paramref name="fields" /> contains duplicate field names.
    /// </exception>
    public TranslationFieldBatchResult(
        IEnumerable<TranslationField> fields,
        bool usedIndividualFallback)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            field.Validate();
            if (!values.TryAdd(field.Name, field.Text))
            {
                throw new ArgumentException(
                    "Translation field names must be unique.",
                    nameof(fields));
            }
        }

        this.translations = new ReadOnlyDictionary<string, string>(values);
        this.UsedIndividualFallback = usedIndividualFallback;
    }

    /// <summary>
    ///     Gets the translated values by their stable field names.
    /// </summary>
    public IReadOnlyDictionary<string, string> Translations => this.translations;

    /// <summary>
    ///     Gets a value that indicates whether individual translation was used
    ///     after batch validation failed.
    /// </summary>
    public bool UsedIndividualFallback { get; }

    /// <summary>
    ///     Gets one required translated field value.
    /// </summary>
    /// <param name="name">The stable field name.</param>
    /// <returns>The translated field value.</returns>
    /// <exception cref="KeyNotFoundException">
    ///     No translated value exists for <paramref name="name" />.
    /// </exception>
    public string GetTranslation(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return this.translations[name];
    }
}
