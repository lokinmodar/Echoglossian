// <copyright file="CombinedTranslationData.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Handlers;

/// <summary>
///     Represents a combined translated payload for addon-local ATK and string
///     array data.
/// </summary>
public class CombinedTranslationData
{
    /// <summary>
    ///     Gets or sets the translated ATK-value payload by slot index.
    /// </summary>
    public Dictionary<int, string>? AtkValues { get; set; }

    /// <summary>
    ///     Gets or sets the translated string-array payload by slot index.
    /// </summary>
    public Dictionary<int, string>? StringArrayData { get; set; }

    /// <summary>
    ///     Gets or sets the translated text-node payload by stable node key.
    /// </summary>
    public Dictionary<string, string>? TextNodes { get; set; }
}
