// <copyright file="TranslatorHelpers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

public partial class Echoglossian
{
    private static readonly Dictionary<string, string> LanguageCodeMap = new()
    {
        { "zh", "zh-CN" },
        { "pt", "pt-BR" },
        { "he", "iw" },
        { "nb", "no" },
        { "fil", "tl" },
        { "jv", "jw" },
    };

    public static string NormalizeLanguageCode(string code)
    {
        return LanguageCodeMap.TryGetValue(code, out var normalized)
            ? normalized
            : code;
    }

    public static string FixText(string text)
    {
        var fixedText = text.Replace("\u200B", string.Empty)
            .Replace("\u005C\u0022", "\"").Replace("\u005C\u002F", "/")
            .Replace("\\u003C", "<").Replace("&#39;", "'");

        fixedText = Regex.Replace(fixedText, @"(?<=.)(─)(?=.)", " \u2015 ");
        return fixedText;
    }

    public static string FormatStreamReader(string read)
    {
        string finalText;
        if (read.StartsWith("[\""))
        {
            char[] start = { '[', '\"' };
            char[] end = { '\"', ']' };
            var dialogueText = read.TrimStart(start);
            finalText = dialogueText.TrimEnd(end);
        }
        else
        {
            finalText = ParseHtml(read);
        }

        finalText = FixText(finalText);
        PluginRuntimeLog.Debug($"FinalTranslatedText: {finalText}");

        return finalText;
    }

    public static string ParseHtml(string html)
    {
        using StringWriter stringWriter = new();

        HtmlDocument doc = new();
        doc.LoadHtml(html);

        var text = doc.DocumentNode.Descendants()
            .Where(n => n.HasClass("result-container")).ToList();

        var parsedText = text.Single(n => n.InnerText.Length > 0).InnerText;

        HttpUtility.HtmlDecode(parsedText, stringWriter);

        var decodedString = stringWriter.ToString();
        PluginRuntimeLog.Debug("In parser: " + parsedText);

        return decodedString;
    }
}

