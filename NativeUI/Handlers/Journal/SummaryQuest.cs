// <copyright file="SummaryQuest.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Echoglossian;

/// <summary>
/// Represents a journal summary quest entry and its translation state.
/// </summary>
public unsafe class SummaryQuest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SummaryQuest"/> class.
    /// </summary>
    /// <param name="originalText">The original source text.</param>
    /// <param name="translatedText">The translated text.</param>
    /// <param name="node">The backing text node.</param>
    /// <param name="isTranslated">Whether the text has already been translated.</param>
    public SummaryQuest(
        string originalText,
        string translatedText,
        AtkTextNode* node,
        bool isTranslated)
    {
        this.OriginalText = originalText;
        this.TranslatedText = translatedText;
        this.Node = node;
        this.IsTranslated = isTranslated;
    }

    /// <summary>
    /// Gets or sets the original source text.
    /// </summary>
    public string OriginalText { get; set; }

    /// <summary>
    /// Gets or sets the translated text.
    /// </summary>
    public string TranslatedText { get; set; }

    /// <summary>
    /// Gets or sets the backing text node.
    /// </summary>
    public AtkTextNode* Node { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the text has already been translated.
    /// </summary>
    public bool IsTranslated { get; set; }
}
