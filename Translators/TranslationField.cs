// <copyright file="TranslationField.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators;

/// <summary>
///     Represents one named source field in a structured translation request.
/// </summary>
/// <param name="Name">The stable field name.</param>
/// <param name="Text">The source text for the field.</param>
public readonly record struct TranslationField(string Name, string Text)
{
    /// <summary>
    ///     Validates that the field has a stable name and source value.
    /// </summary>
    /// <exception cref="ArgumentException">
    ///     The field name is blank.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///     The field text is <see langword="null" />.
    /// </exception>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(this.Name);
        ArgumentNullException.ThrowIfNull(this.Text);
    }
}
