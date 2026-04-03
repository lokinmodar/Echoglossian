using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Echoglossian.Tests;

public partial class TestableEchoglossian
{
    public static string CleanString(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        bool endsWithFiveSpaces = input.EndsWith("     ");

        string result = input.Replace("\r", string.Empty).Replace("\n", string.Empty);

        result = Regex.Replace(result, @"(?<=\S) {2,}(?=\S)", " ");

        if (endsWithFiveSpaces)
        {
            result += "     ";
        }

        return result;
    }

    private static readonly Dictionary<char, string> CustomReplacements = new()
    {
        { 'Ł', "L" }, { 'ł', "l" }, { 'Ć', "C" }, { 'ć', "c" }
    };

    public string RemoveDiacritics(string text, HashSet<char> supportedChars)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var stringBuilder = new StringBuilder();
        foreach (var c in text)
        {
            if (supportedChars.Contains(c))
            {
                stringBuilder.Append(c);
            }
            else if (CustomReplacements.ContainsKey(c))
            {
                stringBuilder.Append(CustomReplacements[c]);
            }
            else
            {
                var normalizedChar = c.ToString().Normalize(NormalizationForm.FormD);
                foreach (var nc in normalizedChar)
                {
                    var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(nc);
                    if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                    {
                        stringBuilder.Append(nc);
                    }
                }
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }
}
