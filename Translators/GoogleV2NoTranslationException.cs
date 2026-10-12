// <copyright file="GoogleV2NoTranslationException.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators;

/// <summary>
///     Signals the Google V2 success response that explicitly has no
///     translation for the requested source text.
/// </summary>
internal sealed class GoogleV2NoTranslationException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="GoogleV2NoTranslationException" /> class.
    /// </summary>
    public GoogleV2NoTranslationException()
        : base("Google V2 returned no translation for the requested text.")
    {
    }
}
