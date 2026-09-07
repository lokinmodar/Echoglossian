// <copyright file="TranslationFieldEnvelopeCodec.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators.Helpers;

/// <summary>
///     Encodes named translation fields in a deterministic escaped envelope.
/// </summary>
internal static class TranslationFieldEnvelopeCodec
{
    private const string Header = "EGLO-FIELDS-1";
    private const string Footer = "EGLO-END-FIELDS-1";
    private const char FieldSeparator = '\t';

    /// <summary>
    ///     Encodes fields into an invariant transport envelope.
    /// </summary>
    /// <param name="fields">The fields to encode in order.</param>
    /// <returns>The escaped transport envelope.</returns>
    internal static string Encode(IEnumerable<TranslationField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var builder = new StringBuilder(Header);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            field.Validate();
            if (!names.Add(field.Name))
            {
                throw new ArgumentException("Translation field names must be unique.", nameof(fields));
            }

            _ = builder.Append('\n')
                .Append(EncodeFieldName(field.Name))
                .Append(FieldSeparator)
                .Append(Escape(field.Text));
        }

        return builder.Append('\n').Append(Footer).ToString();
    }

    /// <summary>
    ///     Tries to decode an envelope without imposing an expected field set.
    /// </summary>
    /// <param name="text">The translated transport envelope.</param>
    /// <param name="fields">The decoded fields.</param>
    /// <returns>
    ///     <see langword="true" /> when the envelope is well formed;
    ///     otherwise, <see langword="false" />.
    /// </returns>
    internal static bool TryDecode(string? text, out IReadOnlyList<TranslationField> fields)
    {
        fields = [];
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var lines = text.Split('\n');
        if (lines.Length < 3 ||
            !string.Equals(lines[0], Header, StringComparison.Ordinal) ||
            !string.Equals(lines[^1], Footer, StringComparison.Ordinal))
        {
            return false;
        }

        var decoded = new List<TranslationField>(lines.Length - 2);
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 1; index < lines.Length - 1; index++)
        {
            if (!TrySplitEscapedField(lines[index], out var encodedName, out var value) ||
                !TryDecodeFieldName(encodedName, out var name) ||
                string.IsNullOrWhiteSpace(name) ||
                !names.Add(name))
            {
                return false;
            }

            decoded.Add(new TranslationField(name, value));
        }

        fields = decoded;
        return true;
    }

    /// <summary>
    ///     Tries to decode an envelope only when it contains exactly the
    ///     expected ordered fields and nonempty translated values.
    /// </summary>
    /// <param name="text">The translated transport envelope.</param>
    /// <param name="expectedFields">The requested fields in required order.</param>
    /// <param name="fields">The decoded translated fields.</param>
    /// <returns>
    ///     <see langword="true" /> when the envelope fully matches the
    ///     request; otherwise, <see langword="false" />.
    /// </returns>
    internal static bool TryDecode(
        string? text,
        IReadOnlyList<TranslationField> expectedFields,
        out IReadOnlyList<TranslationField> fields)
    {
        ArgumentNullException.ThrowIfNull(expectedFields);
        if (!TryDecode(text, out fields) || fields.Count != expectedFields.Count)
        {
            fields = [];
            return false;
        }

        for (var index = 0; index < expectedFields.Count; index++)
        {
            if (!string.Equals(
                    fields[index].Name,
                    expectedFields[index].Name,
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(fields[index].Text))
            {
                fields = [];
                return false;
            }
        }

        return true;
    }

    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static string EncodeFieldName(string name)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(name));
    }

    private static bool TryDecodeFieldName(string encodedName, out string name)
    {
        name = string.Empty;
        try
        {
            name = new UTF8Encoding(false, true).GetString(
                Convert.FromBase64String(encodedName));
            return !string.IsNullOrWhiteSpace(name);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static bool TrySplitEscapedField(
        string line,
        out string name,
        out string value)
    {
        name = string.Empty;
        value = string.Empty;
        var separatorIndex = line.IndexOf(FieldSeparator);
        if (separatorIndex < 1 ||
            line.IndexOf(FieldSeparator, separatorIndex + 1) >= 0)
        {
            return false;
        }

        return TryUnescape(line.AsSpan(0, separatorIndex), out name) &&
               TryUnescape(line.AsSpan(separatorIndex + 1), out value);
    }

    private static bool TryUnescape(ReadOnlySpan<char> value, out string decoded)
    {
        var builder = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (current != '\\')
            {
                _ = builder.Append(current);
                continue;
            }

            if (++index >= value.Length)
            {
                decoded = string.Empty;
                return false;
            }

            current = value[index];
            var replacement = current switch
            {
                '\\' => '\\',
                't' => '\t',
                'r' => '\r',
                'n' => '\n',
                _ => '\0',
            };
            if (replacement == '\0')
            {
                decoded = string.Empty;
                return false;
            }

            _ = builder.Append(replacement);
        }

        decoded = builder.ToString();
        return true;
    }
}
