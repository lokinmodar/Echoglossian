// <copyright file="TranslationReuseScope.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
///     Defines the source, target, and engine compatibility required to reuse
///     a persisted translation.
/// </summary>
/// <param name="SourceLanguageCode">The requested persisted source identity.</param>
/// <param name="TargetLanguageCode">The requested normalized target code.</param>
/// <param name="TranslationEngine">The selected translation engine.</param>
/// <param name="RequireMatchingEngine">
///     Whether reuse requires the selected translation engine.
/// </param>
public readonly record struct TranslationReuseScope(
    string SourceLanguageCode,
    string TargetLanguageCode,
    int? TranslationEngine,
    bool RequireMatchingEngine)
{
    /// <summary>
    ///     Creates the current translation reuse scope from runtime language
    ///     and configuration state.
    /// </summary>
    /// <param name="config">The active plugin configuration.</param>
    /// <param name="scope">The resolved reuse scope when available.</param>
    /// <returns>
    ///     <see langword="true" /> when a complete source and target scope
    ///     can be resolved; otherwise <see langword="false" />.
    /// </returns>
    public static bool TryCreate(Config config, out TranslationReuseScope scope)
    {
        if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
                out var sourceLanguage))
        {
            scope = default;
            return false;
        }

        var targetLanguage =
            RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(config.Lang);
        if (string.IsNullOrWhiteSpace(targetLanguage))
        {
            scope = default;
            return false;
        }

        scope = new TranslationReuseScope(
            sourceLanguage.PersistenceCode,
            targetLanguage,
            config.ChosenTransEngine,
            config.TranslateAlreadyTranslatedTexts);
        return true;
    }

    /// <summary>
    ///     Determines whether one stored translation belongs to this reuse
    ///     scope.
    /// </summary>
    /// <param name="storedSourceLanguage">The persisted source identity.</param>
    /// <param name="storedTargetLanguage">The persisted target language.</param>
    /// <param name="storedEngine">The persisted translation engine.</param>
    /// <returns>
    ///     <see langword="true" /> when the stored values are compatible with
    ///     this scope; otherwise <see langword="false" />.
    /// </returns>
    public bool Matches(
        string? storedSourceLanguage,
        string? storedTargetLanguage,
        int? storedEngine)
    {
        return !string.IsNullOrWhiteSpace(storedSourceLanguage) &&
               RuntimeLanguageHelper.LanguagesMatch(
                   storedSourceLanguage,
                   this.SourceLanguageCode) &&
               RuntimeLanguageHelper.LanguagesMatch(
                   storedTargetLanguage,
                   this.TargetLanguageCode) &&
               (!this.RequireMatchingEngine ||
                storedEngine == this.TranslationEngine);
    }
}
