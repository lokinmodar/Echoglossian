// <copyright file="RuntimeLanguageHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
///     Normalizes the runtime game and target language values so DB-first
///     recovery code can compare persisted rows safely without hardcoding a
///     single target language.
/// </summary>
public static class RuntimeLanguageHelper
{
    private static readonly IReadOnlyDictionary<string, string> LanguageCodeAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["zh"] = "zh-CN",
            ["zh-Hans"] = "zh-CN",
            ["zh-Hant"] = "zh-TW",
            ["pt"] = "pt-BR",
            ["he"] = "iw",
            ["nb"] = "no",
            ["fil"] = "tl",
            ["jv"] = "jw",
        };

    /// <summary>
    ///     Gets the current native client language as a normalized language
    ///     code.
    /// </summary>
    /// <returns>The normalized current game language code.</returns>
    public static string GetCurrentGameLanguageCode()
    {
        return ClientStateInterface.ClientLanguage switch
        {
            ClientLanguage.Japanese => "ja",
            ClientLanguage.German => "de",
            ClientLanguage.French => "fr",
            _ => "en",
        };
    }

    /// <summary>
    ///     Resolves a client language into its stable persisted identity and
    ///     provider input code.
    /// </summary>
    /// <param name="clientLanguage">The raw Dalamud client language.</param>
    /// <param name="sourceLanguage">The resolved source-language contract.</param>
    /// <returns>
    ///     <see langword="true" /> when the client language has a known
    ///     identity; otherwise <see langword="false" />.
    /// </returns>
    public static bool TryResolveSourceLanguage(
        ClientLanguage clientLanguage,
        out SourceClientLanguage sourceLanguage)
    {
        sourceLanguage = (int)clientLanguage switch
        {
            0 => new SourceClientLanguage("ja", "ja"),
            1 => new SourceClientLanguage("en", "en"),
            2 => new SourceClientLanguage("de", "de"),
            3 => new SourceClientLanguage("fr", "fr"),
            4 => new SourceClientLanguage("chs", "zh-CN"),
            5 => new SourceClientLanguage("cht", "zh-CN"),
            6 => new SourceClientLanguage("ko", "ko"),
            7 => new SourceClientLanguage("tc", "zh-TW"),
            _ => default,
        };

        if (!string.IsNullOrWhiteSpace(sourceLanguage.PersistenceCode))
        {
            return true;
        }

        if (!Enum.IsDefined(clientLanguage))
        {
            return false;
        }

        try
        {
            var hostCode = NormalizeLanguage(clientLanguage.ToCode());
            if (!string.IsNullOrWhiteSpace(hostCode))
            {
                sourceLanguage = new SourceClientLanguage(hostCode, hostCode);
                return true;
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            // The current host does not expose an identity for this raw value.
        }

        return false;
    }

    /// <summary>
    ///     Resolves the currently active client language into its stable
    ///     persisted identity and provider input code.
    /// </summary>
    /// <param name="sourceLanguage">The resolved source-language contract.</param>
    /// <returns>
    ///     <see langword="true" /> when the current client language has a
    ///     known identity; otherwise <see langword="false" />.
    /// </returns>
    public static bool TryResolveCurrentSourceLanguage(
        out SourceClientLanguage sourceLanguage)
    {
        return TryResolveSourceLanguage(
            ClientStateInterface.ClientLanguage,
            out sourceLanguage);
    }

    /// <summary>
    ///     Gets the configured translation target language as a normalized
    ///     language code.
    /// </summary>
    /// <param name="languageIndex">The configured target-language index.</param>
    /// <returns>The normalized configured target language code.</returns>
    public static string GetConfiguredTargetLanguageCode(int languageIndex)
    {
        if (!LangDict.TryGetValue(languageIndex, out var languageInfo) ||
            string.IsNullOrWhiteSpace(languageInfo.Code))
        {
            return string.Empty;
        }

        return NormalizeLanguage(languageInfo.Code);
    }

    /// <summary>
    ///     Determines whether two language values represent the same effective
    ///     language.
    /// </summary>
    /// <param name="left">The first language value.</param>
    /// <param name="right">The second language value.</param>
    /// <returns>
    ///     <see langword="true" /> when both values normalize to the same
    ///     code; otherwise <see langword="false" />.
    /// </returns>
    public static bool LanguagesMatch(string? left, string? right)
    {
        var normalizedLeft = NormalizeLanguage(left);
        var normalizedRight = NormalizeLanguage(right);

        return !string.IsNullOrWhiteSpace(normalizedLeft) &&
               string.Equals(
                   normalizedLeft,
                   normalizedRight,
                   StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Normalizes one runtime language value to a stable comparison code.
    /// </summary>
    /// <param name="language">The raw language value.</param>
    /// <returns>The normalized language code, or an empty string.</returns>
    public static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return string.Empty;
        }

        var trimmed = language.Trim();
        var lowered = trimmed.ToLowerInvariant();

        return lowered switch
        {
            "english" => "en",
            "en" => "en",
            "german" => "de",
            "deutsch" => "de",
            "de" => "de",
            "french" => "fr",
            "français" => "fr",
            "francais" => "fr",
            "fr" => "fr",
            "japanese" => "ja",
            "日本語" => "ja",
            "ja" => "ja",
            _ => LanguageCodeAliases.TryGetValue(trimmed, out var normalizedCode)
                ? normalizedCode
                : trimmed,
        };
    }
}
