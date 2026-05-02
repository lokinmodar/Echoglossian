// <copyright file="LanguageEngineSupport.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.LanguagesHandling
{
    /// <summary>
    /// Provides supported engine lists per language code, computed from vendor-verified language sets
    /// and your runtime engine ordering. Broad-coverage LLM engines (ChatGPT, DeepSeek, Gemini,
    /// OpenRouter, Ollama, LmStudio, Claude) are added to all languages.
    /// <para>
    /// Engine indices (must match your <c>enginesList</c>):
    /// <list type="number">
    ///   <item><description>0 - Google</description></item>
    ///   <item><description>1 - DeepL</description></item>
    ///   <item><description>2 - ChatGPT</description></item>
    ///   <item><description>3 - YandexCloud</description></item>
    ///   <item><description>4 - GTranslate</description></item>
    ///   <item><description>5 - DeepSeek</description></item>
    ///   <item><description>6 - Ollama</description></item>
    ///   <item><description>7 - LibreTranslate</description></item>
    ///   <item><description>8 - Microsoft</description></item>
    ///   <item><description>9 - Amazon</description></item>
    ///   <item><description>10 - Gemini</description></item>
    ///   <item><description>11 - YandexPublic</description></item>
    ///   <item><description>12 - OpenRouter</description></item>
    ///   <item><description>13 - LmStudio</description></item>
    ///   <item><description>14 - Claude</description></item>
    /// </list>
    /// </para>
    /// <remarks>
    /// - Vendor sets reflect their official language pages as of 2025-10-09.
    /// - GTranslate follows Google’s language set; we mirror Google for that engine.
    /// - LibreTranslate’s list can vary by instance; we use the canonical set exposed in docs.
    /// - Region normalization is applied per engine (e.g., <c>pt-PT</c>, <c>pt-BR</c>, <c>zh-CN</c>, <c>zh-TW</c>).
    /// - Manual-inclusion hook retains niche codes you already use (e.g., <c>klingon</c>, <c>nqo</c>).
    /// </remarks>
    /// </summary>
    public static class LanguageEngineSupport
    {
        // Engine indices (keep in sync with enginesList).
        private const int Google = 0;
        private const int DeepL = 1;
        private const int ChatGPT = 2;
        private const int YandexCloud = 3;
        private const int GTranslate = 4;
        private const int DeepSeek = 5;
        private const int Ollama = 6;
        private const int LibreTranslate = 7;
        private const int Microsoft = 8;
        private const int Amazon = 9;
        private const int Gemini = 10;
        private const int YandexPublic = 11;
        private const int OpenRouter = 12;
        private const int LmStudio = 13;
        private const int Claude = 14;

        /// <summary>
        /// Engines treated as broadly multilingual (no fixed official "translation language" list).
        /// These are always added to every language.
        /// </summary>
        private static readonly int[] BroadCoverageLlms =
        {
            ChatGPT, DeepSeek, Gemini, OpenRouter, Ollama, LmStudio, Claude,
        };

        /// <summary>
        /// Manual niche inclusions that you previously supported even if vendors don't formally list them.
        /// Key: engine index; Value: set of language codes (case-insensitive).
        /// </summary>
        private static readonly Dictionary<int, HashSet<string>> ManualInclusionsPerEngine =
            new Dictionary<int, HashSet<string>>
            {
                { Google, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "klingon", "nqo" } },
                // Add other exceptions here if you discover vendor parity later.
            };

        /// <summary>
        /// Vendor-official sets (case-insensitive) of supported language codes. These are curated here
        /// so we can compute per-language engine arrays deterministically and quickly at runtime.
        /// </summary>
        private static readonly Dictionary<int, HashSet<string>> VendorSets =
            new Dictionary<int, HashSet<string>>
            {
                // GOOGLE — explicit common codes we track; anything else is assumed supported as well.
                { Google, new HashSet<string>(GetGoogleCommonCodes(), StringComparer.OrdinalIgnoreCase) },

                // GTRANSLATE — tracks Google (mirror Google logic).
                { GTranslate, new HashSet<string>(GetGoogleCommonCodes(), StringComparer.OrdinalIgnoreCase) },

                // DEEPL — official supported languages; includes 2025 expansion (he, vi, th, id, zh).
                { DeepL, new HashSet<string>(GetDeepLCodes(), StringComparer.OrdinalIgnoreCase) },

                // MICROSOFT TRANSLATOR — large official set (table).
                { Microsoft, new HashSet<string>(GetMicrosoftCodes(), StringComparer.OrdinalIgnoreCase) },

                // AMAZON TRANSLATE — official list.
                { Amazon, new HashSet<string>(GetAmazonCodes(), StringComparer.OrdinalIgnoreCase) },

                // YANDEX CLOUD — official supported languages.
                { YandexCloud, new HashSet<string>(GetYandexCloudCodes(), StringComparer.OrdinalIgnoreCase) },

                // YANDEX PUBLIC — align to Yandex Cloud for consistency.
                { YandexPublic, new HashSet<string>(GetYandexCloudCodes(), StringComparer.OrdinalIgnoreCase) },

                // LIBRETRANSLATE — canonical upstream set (varies by instance).
                { LibreTranslate, new HashSet<string>(GetLibreTranslateCodes(), StringComparer.OrdinalIgnoreCase) },
            };

        /// <summary>
        /// Applies supported engine indices to each language in the provided dictionary. This uses
        /// vendor sets, per-engine normalization, LLM broad coverage, and manual inclusions.
        /// </summary>
        /// <param name="dictionary">Map: language ID → language info with a <c>Code</c> property.</param>
        public static void ApplySupportTo(Dictionary<int, LanguageInfo> dictionary)
        {
            foreach (var pair in dictionary)
            {
                var code = pair.Value.Code ?? string.Empty;
                var engines = new HashSet<int>();

                // 1) Deterministic, vendor-driven engines.
                foreach (var kvp in VendorSets)
                {
                    var engine = kvp.Key;
                    var set = kvp.Value;

                    var normalized = NormalizeCodeForEngine(code, engine);

                    if (normalized.Count == 0)
                    {
                        continue;
                    }

                    if (normalized.Any(c => set.Contains(c)))
                    {
                        _ = engines.Add(engine);
                    }

                    // Manual exceptions.
                    if (ManualInclusionsPerEngine.TryGetValue(engine, out var manual)
                        && normalized.Any(c => manual.Contains(c)))
                    {
                        _ = engines.Add(engine);
                    }

                    // Google/GTranslate: include even if a rare code variant isn't in our common list.
                    if ((engine == Google || engine == GTranslate) && engines.Contains(engine) == false)
                    {
                        _ = engines.Add(engine);
                    }
                }

                // 2) Broad-coverage LLM engines.
                foreach (var llm in BroadCoverageLlms)
                {
                    _ = engines.Add(llm);
                }

                // 3) Normalize (deterministic order).
                pair.Value.SupportedEngines = engines.OrderBy(i => i).ToList();
            }
        }

        /// <summary>
        /// Produces normalization candidates for a language code per engine. This keeps regional variants
        /// consistent with vendor expectations (e.g., DeepL: <c>pt-PT</c>/<c>pt-BR</c>, Chinese variants).
        /// </summary>
        /// <param name="code">The raw language code from <c>LanguageInfo.Code</c>.</param>
        /// <param name="engine">The engine index.</param>
        /// <returns>One or more candidate codes to test for support.</returns>
        private static List<string> NormalizeCodeForEngine(string code, int engine)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(code))
            {
                return result;
            }

            var c = code.Trim();

            // Standard lower + BCP-47 normalize
            var lower = c.ToLowerInvariant();

            // Common folds
            // Chinese variants:
            if (lower is "zh-cn" or "zh-hans")
            {
                result.Add("zh-CN");
                result.Add("zh");
            }
            else if (lower is "zh-tw" or "zh-hant")
            {
                result.Add("zh-TW");
                result.Add("zh");
            }
            else if (lower is "pt-br")
            {
                result.Add("pt-BR");
                result.Add("pt");
            }
            else if (lower is "pt-pt")
            {
                result.Add("pt-PT");
                result.Add("pt");
            }
            else if (lower.StartsWith("sr-", StringComparison.Ordinal))
            {
                result.Add("sr");
                result.Add(c);
            }
            else
            {
                result.Add(c);
                var dash = c.IndexOf('-', StringComparison.Ordinal);
                if (dash > 0)
                {
                    result.Add(c[..dash]);
                }
            }

            // Engine-specific tweaks.
            switch (engine)
            {
                case DeepL:
                {
                    // DeepL uses EN-GB/EN-US as target variants, but we consider "en" too.
                    if (lower is "en-gb" or "en-us")
                    {
                        result.Add("EN-GB");
                        result.Add("EN-US");
                        result.Add("en");
                    }

                    // DeepL uses NB for Norwegian Bokmål; accept nb/no.
                    if (lower is "nb" or "no")
                    {
                        result.Add("nb");
                        result.Add("no");
                    }

                    // Unify "zh" family for DeepL.
                    if (lower is "zh" or "zh-cn" or "zh-tw" or "zh-hans" or "zh-hant")
                    {
                        result.Add("zh");
                        result.Add("zh-CN");
                        result.Add("zh-TW");
                    }

                    break;
                }

                case Microsoft:
                {
                    // Azure table commonly uses base codes; defaults above suffice.
                    break;
                }

                case Amazon:
                {
                    // Amazon uses BCP-47 (e.g., es-MX). We already added both region and base.
                    break;
                }

                default:
                {
                    break;
                }
            }

            // Distinct + preserve order.
            return result
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // ------------------------
        // Vendor language code sets
        // ------------------------

        /// <summary>
        /// Common Google/GTranslate codes we explicitly track; uncommon codes are still treated as supported.
        /// </summary>
        private static IEnumerable<string> GetGoogleCommonCodes()
        {
            // Practical superset covering codes you use today; Google supports many more.
            yield return "af";
            yield return "am";
            yield return "ar";
            yield return "az";
            yield return "be";
            yield return "bg";
            yield return "bn";
            yield return "bs";
            yield return "ca";
            yield return "ceb";
            yield return "cs";
            yield return "cy";
            yield return "da";
            yield return "de";
            yield return "el";
            yield return "en";
            yield return "eo";
            yield return "es";
            yield return "et";
            yield return "eu";
            yield return "fa";
            yield return "fi";
            yield return "fil";
            yield return "fr";
            yield return "fy";
            yield return "ga";
            yield return "gd";
            yield return "gl";
            yield return "gu";
            yield return "ha";
            yield return "haw";
            yield return "he";
            yield return "hi";
            yield return "hr";
            yield return "ht";
            yield return "hu";
            yield return "hy";
            yield return "id";
            yield return "ig";
            yield return "is";
            yield return "it";
            yield return "ja";
            yield return "jv";
            yield return "ka";
            yield return "kk";
            yield return "km";
            yield return "kn";
            yield return "ko";
            yield return "ku";
            yield return "ky";
            yield return "la";
            yield return "lb";
            yield return "lo";
            yield return "lt";
            yield return "lv";
            yield return "mk";
            yield return "ml";
            yield return "mn";
            yield return "mr";
            yield return "ms";
            yield return "mt";
            yield return "my";
            yield return "nb";
            yield return "ne";
            yield return "nl";
            yield return "nn";
            yield return "no";
            yield return "ny";
            yield return "or";
            yield return "pa";
            yield return "pl";
            yield return "ps";
            yield return "pt";
            yield return "pt-BR";
            yield return "pt-PT";
            yield return "ro";
            yield return "ru";
            yield return "rw";
            yield return "si";
            yield return "sk";
            yield return "sl";
            yield return "sm";
            yield return "sn";
            yield return "so";
            yield return "sq";
            yield return "sr";
            yield return "st";
            yield return "su";
            yield return "sv";
            yield return "sw";
            yield return "ta";
            yield return "te";
            yield return "tg";
            yield return "th";
            yield return "ti";
            yield return "tl";
            yield return "tr";
            yield return "tt";
            yield return "uk";
            yield return "ur";
            yield return "uz";
            yield return "vi";
            yield return "vo";
            yield return "xh";
            yield return "yi";
            yield return "yo";
            yield return "zh";
            yield return "zh-CN";
            yield return "zh-TW";
            yield return "zu";
        }

        /// <summary>
        /// DeepL supported languages (2025 set including Hebrew, Vietnamese, Thai, Indonesian, Chinese).
        /// Variants included where relevant (EN-GB/EN-US, PT-BR/PT-PT, ZH, etc.).
        /// </summary>
        private static IEnumerable<string> GetDeepLCodes()
        {
            yield return "bg";
            yield return "cs";
            yield return "da";
            yield return "de";
            yield return "el";
            yield return "en";
            yield return "EN-GB";
            yield return "EN-US";
            yield return "es";
            yield return "et";
            yield return "fi";
            yield return "fr";
            yield return "hu";
            yield return "id";
            yield return "it";
            yield return "ja";
            yield return "ko";
            yield return "lt";
            yield return "lv";
            yield return "nb";
            yield return "nl";
            yield return "pl";
            yield return "pt";
            yield return "pt-BR";
            yield return "pt-PT";
            yield return "ro";
            yield return "ru";
            yield return "sk";
            yield return "sl";
            yield return "sv";
            yield return "tr";
            yield return "uk";
            yield return "zh";
            yield return "he";
            yield return "th";
            yield return "vi";
        }

        /// <summary>
        /// Microsoft Translator (Azure) language codes covering the plugin’s common set.
        /// </summary>
        private static IEnumerable<string> GetMicrosoftCodes()
        {
            var codes = new[]
            {
                "af", "am", "ar", "az", "ba", "be", "bg", "bn", "bs", "ca", "ceb", "cs", "cy", "da", "de", "el", "en", "eo", "es", "et", "eu", "fa", "fi", "fil", "fr", "ga", "gd", "gl",
                "gu", "ha", "haw", "he", "hi", "hr", "ht", "hu", "hy", "id", "is", "it", "ja", "jv", "ka", "kk", "km", "kn", "ko", "ku", "ky", "la", "lb", "lo", "lt", "lv", "mk", "ml",
                "mn", "mr", "ms", "mt", "my", "ne", "nl", "no", "nb", "nn", "ny", "or", "pa", "pl", "ps", "pt", "pt-BR", "pt-PT", "ro", "ru", "rw", "si", "sk", "sl", "sm", "sn", "so",
                "sq", "sr", "st", "su", "sv", "sw", "ta", "te", "tg", "th", "tl", "tr", "tt", "uk", "ur", "uz", "vi", "xh", "yi", "yo", "zh", "zh-CN", "zh-TW", "zu",
                // Long tail you had under Microsoft:
                "ace", "ady", "alt", "arn", "az-Latn", "brx", "chr", "ckb", "doi", "dv", "dz", "ff", "gsw", "gom", "inh", "kab", "sah", "sat", "sr-Latn", "ti", "tzm",
            };

            foreach (var x in codes)
            {
                yield return x;
            }
        }

        /// <summary>
        /// Amazon Translate supported codes (mainstream coverage used in your plugin).
        /// </summary>
        private static IEnumerable<string> GetAmazonCodes()
        {
            var codes = new[]
            {
                "ar", "de", "en", "es", "fr", "it", "ja", "ko", "pt", "pt-BR", "pt-PT", "ru", "zh", "zh-CN", "zh-TW", "nl", "sv", "tr", "hi", "fa", "pl", "cs", "da", "fi", "no", "ro",
                "he", "id", "ms", "vi", "th", "uk",
            };

            foreach (var x in codes)
            {
                yield return x;
            }
        }

        /// <summary>
        /// Yandex Cloud Translate language codes (official list).
        /// </summary>
        private static IEnumerable<string> GetYandexCloudCodes()
        {
            var codes = new[]
            {
                "af", "am", "ar", "az", "ba", "be", "bg", "bn", "bs", "ca", "ceb", "cs", "cv", "cy", "da", "de", "el", "en", "eo", "es", "et", "eu", "fa", "fi", "fr", "gl", "gu", "ha",
                "haw", "he", "hi", "hr", "ht", "hu", "hy", "id", "ig", "is", "it", "ja", "jv", "ka", "kk", "km", "kn", "ko", "ku", "ky", "la", "lb", "lo", "lt", "lv", "mk", "ml", "mn",
                "mr", "ms", "mt", "my", "ne", "nl", "no", "oc", "pa", "pl", "ps", "pt", "ro", "ru", "rw", "sd", "si", "sk", "sl", "sm", "sn", "so", "sq", "sr", "st", "su", "sv", "sw",
                "ta", "te", "tg", "th", "tl", "tr", "tt", "uk", "ur", "uz", "vi", "xh", "yi", "yo", "zh", "zh-CN", "zh-TW", "zu", "emj",
            };

            foreach (var x in codes)
            {
                yield return x;
            }
        }

        /// <summary>
        /// LibreTranslate canonical codes (can vary per instance; upstream commonly lists these).
        /// </summary>
        private static IEnumerable<string> GetLibreTranslateCodes()
        {
            var codes = new[]
            {
                "en", "ar", "zh", "fr", "de", "it", "ja", "pt", "ru", "es", "pl", "bg", "ca", "cs", "nl", "eo", "eu", "gd", "hu", "ga", "kab", "ko", "uk",
            };

            foreach (var x in codes)
            {
                yield return x;
            }
        }
    }
}
