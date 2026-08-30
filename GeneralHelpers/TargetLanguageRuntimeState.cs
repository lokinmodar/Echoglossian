// <copyright file="TargetLanguageRuntimeState.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.LanguagesHandling;
using Echoglossian.UIOverlays.TextPresentation;

namespace Echoglossian;

/// <summary>
/// Synchronizes the authoritative configured target language into the legacy
/// runtime mirrors consumed across the plugin.
/// </summary>
internal static class TargetLanguageRuntimeState
{
    /// <summary>
    /// Synchronizes the configured target language into the shared runtime
    /// language dictionary, legacy integer identifier, selected language, and
    /// presentation flags.
    /// </summary>
    /// <param name="configuration">The active plugin configuration.</param>
    /// <param name="languages">The registered runtime language metadata.</param>
    /// <returns>The resolved selected language.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when the configured language identifier is not registered.
    /// </exception>
    internal static LanguageInfo Synchronize(
        Config configuration,
        Dictionary<int, LanguageInfo> languages)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(languages);

        if (!languages.TryGetValue(configuration.Lang, out var selectedLanguage))
        {
            throw new KeyNotFoundException(
                $"Configured target language id {configuration.Lang} is not registered.");
        }

        Echoglossian.LangDict = languages;
        Echoglossian.LanguageInt = configuration.Lang;
        Echoglossian.SelectedLanguage = selectedLanguage;
        LanguagePresentationPolicy.ApplyLanguageFlags(configuration);
        return selectedLanguage;
    }
}
