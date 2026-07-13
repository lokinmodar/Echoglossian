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
    /// - Vendor sets reflect official public endpoints or official language pages as of 2026-07-12.
    /// - GTranslate follows Google’s language set; we mirror Google for that engine.
    /// - LibreTranslate’s list can vary by instance; we use the canonical list exposed by the public upstream instance.
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
                // GOOGLE — plugin-exposed codes verified against the official supported-language page.
                { Google, new HashSet<string>(GetGoogleCommonCodes(), StringComparer.OrdinalIgnoreCase) },

                // GTRANSLATE — tracks Google.
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
                        result.Add("ZH-HANS");
                        result.Add("ZH-HANT");
                        result.Add("zh-CN");
                        result.Add("zh-TW");
                    }

                    break;
                }

                case Microsoft:
                {
                    if (lower is "zh" or "zh-cn" or "zh-hans")
                    {
                        result.Add("zh-Hans");
                    }

                    if (lower is "zh" or "zh-tw" or "zh-hant")
                    {
                        result.Add("zh-Hant");
                    }

                    if (lower is "nb" or "nn" or "no")
                    {
                        result.Add("nb");
                    }

                    if (lower == "sr")
                    {
                        result.Add("sr-Cyrl");
                        result.Add("sr-Latn");
                    }

                    if (lower is "tl" or "fil")
                    {
                        result.Add("fil");
                    }

                    if (lower == "klingon")
                    {
                        result.Add("tlh-Latn");
                        result.Add("tlh-Piqd");
                    }

                    break;
                }

                case LibreTranslate:
                {
                    if (lower is "zh" or "zh-cn" or "zh-hans")
                    {
                        result.Add("zh-Hans");
                    }

                    if (lower is "zh" or "zh-tw" or "zh-hant")
                    {
                        result.Add("zh-Hant");
                    }

                    if (lower is "nb" or "nn" or "no")
                    {
                        result.Add("nb");
                    }

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
        /// Google/GTranslate codes verified against the current official supported-language page for
        /// the languages exposed by the plugin.
        /// </summary>
        private static IEnumerable<string> GetGoogleCommonCodes()
        {
            var codes = new[]
            {
                "ace", "af", "am", "ar", "as", "az", "ba", "be", "bg", "bho", "bn", "br", "bs",
                "bua", "ca", "ceb", "ckb", "co", "cs", "cy", "da", "de", "doi", "dv", "dz", "el",
                "en", "eo", "es", "es-MX", "et", "eu", "fa", "ff", "fi", "fil", "fr", "fr-CA",
                "fy", "ga", "gd", "gl", "gom", "gu", "ha", "haw", "he", "hi", "hr", "ht", "hu",
                "hy", "id", "ig", "is", "it", "ja", "jv", "ka", "kk", "km", "kn", "ko", "ku",
                "ky", "la", "lb", "lmo", "lo", "lt", "lv", "mg", "mi", "mk", "ml", "mn", "mr",
                "ms", "mt", "my", "nb", "ne", "nl", "nn", "no", "nso", "ny", "oc", "or", "pa",
                "pap", "pl", "ps", "pt", "pt-BR", "pt-PT", "ro", "ru", "rw", "sd", "si", "sk",
                "sl", "sm", "sn", "so", "sq", "sr", "st", "su", "sv", "sw", "ta", "te", "tg",
                "th", "ti", "tk", "tl", "tr", "tt", "ug", "uk", "ur", "uz", "vi", "xh", "yi",
                "yo", "yua", "yue", "zh", "zh-CN", "zh-TW", "zu",
            };

            foreach (var code in codes)
            {
                yield return code;
            }
        }

        /// <summary>
        /// DeepL translation target languages from the official supported-languages documentation.
        /// Includes current beta target languages and target-only regional variants where relevant.
        /// </summary>
        private static IEnumerable<string> GetDeepLCodes()
        {
            yield return "ACE";
            yield return "AF";
            yield return "AN";
            yield return "AR";
            yield return "AS";
            yield return "AY";
            yield return "AZ";
            yield return "BA";
            yield return "BE";
            yield return "bg";
            yield return "BHO";
            yield return "BN";
            yield return "BR";
            yield return "BS";
            yield return "CA";
            yield return "CEB";
            yield return "CKB";
            yield return "cs";
            yield return "CY";
            yield return "da";
            yield return "de";
            yield return "el";
            yield return "en";
            yield return "EN-GB";
            yield return "EN-US";
            yield return "EO";
            yield return "es";
            yield return "ES-419";
            yield return "et";
            yield return "EU";
            yield return "FA";
            yield return "fi";
            yield return "fr";
            yield return "GA";
            yield return "GL";
            yield return "GN";
            yield return "GOM";
            yield return "GU";
            yield return "HA";
            yield return "hu";
            yield return "HE";
            yield return "HI";
            yield return "HR";
            yield return "HT";
            yield return "HY";
            yield return "id";
            yield return "IG";
            yield return "it";
            yield return "ja";
            yield return "JV";
            yield return "KA";
            yield return "KK";
            yield return "KMR";
            yield return "ko";
            yield return "KY";
            yield return "LA";
            yield return "LB";
            yield return "LMO";
            yield return "LN";
            yield return "lt";
            yield return "lv";
            yield return "MAI";
            yield return "MG";
            yield return "MI";
            yield return "MK";
            yield return "ML";
            yield return "MN";
            yield return "MR";
            yield return "MS";
            yield return "MT";
            yield return "MY";
            yield return "nb";
            yield return "NE";
            yield return "nl";
            yield return "OC";
            yield return "OM";
            yield return "PA";
            yield return "PAG";
            yield return "PAM";
            yield return "pl";
            yield return "PRS";
            yield return "PS";
            yield return "pt";
            yield return "pt-BR";
            yield return "pt-PT";
            yield return "QU";
            yield return "ro";
            yield return "ru";
            yield return "SA";
            yield return "SCN";
            yield return "sk";
            yield return "sl";
            yield return "SQ";
            yield return "SR";
            yield return "ST";
            yield return "SU";
            yield return "sv";
            yield return "SW";
            yield return "TA";
            yield return "TE";
            yield return "TG";
            yield return "tr";
            yield return "TK";
            yield return "TL";
            yield return "TN";
            yield return "TS";
            yield return "TT";
            yield return "uk";
            yield return "UR";
            yield return "UZ";
            yield return "zh";
            yield return "ZH-HANS";
            yield return "ZH-HANT";
            yield return "he";
            yield return "th";
            yield return "vi";
            yield return "WO";
            yield return "XH";
            yield return "YI";
            yield return "YUE";
            yield return "ZU";
        }

        /// <summary>
        /// Microsoft Translator language codes from the official public languages endpoint.
        /// </summary>
        private static IEnumerable<string> GetMicrosoftCodes()
        {
            var codes = new[]
            {
                "af", "am", "ar", "as", "az", "ba", "be", "bg", "bho", "bn", "bo", "brx", "bs", "ca", "cs", "cy", "da", "de", "doi", "dsb", "dv", "el", "en", "es", "es-MX", "et", "eu", "fa",
                "fi", "fil", "fj", "fo", "fr", "fr-CA", "ga", "gl", "gom", "gu", "ha", "he", "hi", "hne", "hr", "hsb", "ht", "hu", "hy", "id", "ig", "ikt", "is", "it", "iu",
                "iu-Latn", "ja", "ka", "kk", "km", "kmr", "kn", "ko", "ks", "ku", "ky", "lb", "ln", "lo", "lt", "lug", "lv", "lzh", "mai", "mg", "mi", "mk", "ml", "mn-Cyrl",
                "mn-Mong", "mni", "mr", "ms", "mt", "mww", "my", "nb", "ne", "nl", "nso", "nya", "or", "otq", "pa", "pl", "prs", "ps", "pt", "pt-PT", "ro", "ru", "run", "rw",
                "sd", "si", "sk", "sl", "sm", "sn", "so", "sq", "sr-Cyrl", "sr-Latn", "st", "sv", "sw", "ta", "te", "th", "ti", "tk", "tlh-Latn", "tlh-Piqd", "tn", "to", "tr",
                "tt", "ty", "ug", "uk", "ur", "uz", "vi", "xh", "yo", "yua", "yue", "zh-Hans", "zh-Hant", "zu",
            };

            foreach (var x in codes)
            {
                yield return x;
            }
        }

        /// <summary>
        /// Amazon Translate supported language codes from the official Developer Guide page.
        /// </summary>
        private static IEnumerable<string> GetAmazonCodes()
        {
            var codes = new[]
            {
                "af", "am", "ar", "az", "bg", "bn", "bs", "ca", "cs", "cy", "da", "de", "el", "en", "es", "es-MX", "et", "fa", "fa-AF", "fi", "fr", "fr-CA", "ga", "gu", "ha", "he",
                "hi", "hr", "ht", "hu", "hy", "id", "is", "it", "ja", "ka", "kk", "kn", "ko", "lt", "lv", "mk", "ml", "mn", "mr", "ms", "mt", "nl", "no", "pa", "pl", "ps", "pt",
                "pt-PT", "ro", "ru", "si", "sk", "sl", "so", "sq", "sr", "sv", "sw", "ta", "te", "th", "tl", "tr", "uk", "ur", "uz", "vi", "zh", "zh-TW",
            };

            foreach (var x in codes)
            {
                yield return x;
            }
        }

        /// <summary>
        /// Yandex Translate language codes from the current official supported-languages page.
        /// The page notes that exact live API results require the authenticated ListLanguages method.
        /// </summary>
        private static IEnumerable<string> GetYandexCloudCodes()
        {
            var codes = new[]
            {
                "af", "am", "ar", "az", "ba", "be", "bg", "bn", "bs", "bua", "ca", "ceb", "cs", "cv", "cy", "da", "de", "el", "emj", "en", "eo", "es", "et", "eu", "fa", "fi", "fr",
                "ga", "gd", "gl", "gu", "he", "hi", "hr", "ht", "hu", "hy", "id", "is", "it", "ja", "jv", "ka", "kazlat", "kbd", "kk", "km", "kn", "ko", "krc", "kv", "ky", "la",
                "lb", "lo", "lt", "lv", "mdf", "mg", "mhr", "mi", "mk", "ml", "mn", "mr", "mrj", "ms", "mt", "my", "myv", "ne", "nl", "no", "os", "pa", "pap", "pl", "pt-BR", "pt",
                "ro", "ru", "sah", "si", "sk", "sl", "sq", "sr-Latn", "sr", "su", "sv", "sw", "ta", "te", "tg", "th", "tl", "tr", "tt", "tyv", "udm", "uk", "ur", "uz", "uzbcyr",
                "vi", "xh", "yi", "zh", "zu",
            };

            foreach (var x in codes)
            {
                yield return x;
            }
        }

        /// <summary>
        /// LibreTranslate codes from the current public upstream instance.
        /// </summary>
        private static IEnumerable<string> GetLibreTranslateCodes()
        {
            var codes = new[]
            {
                "en", "sq", "ar", "az", "eu", "bn", "bg", "ca", "zh-Hans", "zh-Hant", "cs", "da", "nl", "eo", "et", "fi", "fr", "gl", "de", "el", "he", "hi", "hu", "id", "ga", "it",
                "ja", "ko", "ky", "lv", "lt", "ms", "nb", "fa", "pl", "pt", "pt-BR", "ro", "ru", "sr", "sk", "sl", "es", "sv", "tl", "th", "tr", "uk", "ur", "vi",
                "sw",
            };

            foreach (var x in codes)
            {
                yield return x;
            }
        }
    }
}
