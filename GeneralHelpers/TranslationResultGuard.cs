// <copyright file="TranslationResultGuard.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
///     Provides shared guards for synthetic translation-error placeholders so
///     they never become accepted translated output.
/// </summary>
internal static class TranslationResultGuard
{
    private static readonly string[] KnownTranslationErrorCultureNames =
    {
        string.Empty,
        "da",
        "de",
        "el",
        "es",
        "eu",
        "fr",
        "it",
        "pt",
        "pt-BR",
        "ru",
    };

    private static readonly Lazy<string[]> SyntheticErrorPrefixes =
        new(BuildSyntheticErrorPrefixes);

    /// <summary>
    ///     Gets the shared failure reason used when an engine returns a
    ///     synthetic error placeholder instead of a real translation.
    /// </summary>
    public static string SyntheticErrorFailureReason => "synthetic-error-result";

    /// <summary>
    ///     Determines whether the specified text is safe to accept as a stored
    ///     translated result.
    /// </summary>
    /// <param name="text">The translated text candidate.</param>
    /// <returns>
    ///     <see langword="true" /> when the text is non-empty and not a
    ///     synthetic translation-error placeholder; otherwise,
    ///     <see langword="false" />.
    /// </returns>
    public static bool IsPersistableTranslation(string? text)
    {
        return !string.IsNullOrWhiteSpace(text) &&
               !ContainsSyntheticTranslationError(text);
    }

    /// <summary>
    ///     Determines whether the specified text is one of the synthetic
    ///     translation-error placeholders emitted by translator engines.
    /// </summary>
    /// <param name="text">The translated text candidate.</param>
    /// <returns>
    ///     <see langword="true" /> when the text matches a known localized
    ///     translation-error prefix; otherwise, <see langword="false" />.
    /// </returns>
    public static bool ContainsSyntheticTranslationError(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmedText = text.TrimStart();
        if (!trimmedText.StartsWith("[", StringComparison.Ordinal))
        {
            return false;
        }

        return SyntheticErrorPrefixes.Value.Any(prefix =>
            trimmedText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Gets the known localized synthetic error prefixes for cleanup tasks
    ///     such as database migrations.
    /// </summary>
    /// <returns>The localized synthetic error prefixes.</returns>
    public static IReadOnlyList<string> GetSyntheticErrorPrefixes()
    {
        return SyntheticErrorPrefixes.Value;
    }

    private static string[] BuildSyntheticErrorPrefixes()
    {
        var prefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "[Translation Error:",
        };

        foreach (var cultureName in KnownTranslationErrorCultureNames)
        {
            var culture = string.IsNullOrEmpty(cultureName)
                ? CultureInfo.InvariantCulture
                : CultureInfo.GetCultureInfo(cultureName);
            var localizedMarker = Resources.ResourceManager.GetString(
                nameof(Resources.TranslationError),
                culture);

            if (!string.IsNullOrWhiteSpace(localizedMarker))
            {
                prefixes.Add($"[{localizedMarker.Trim()}");
            }
        }

        return prefixes
            .OrderByDescending(static prefix => prefix.Length)
            .ToArray();
    }
}
