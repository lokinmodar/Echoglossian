// <copyright file="TranslationFieldPipeCodec.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators.Helpers;

/// <summary>
///     Encodes named translation fields as opaque ordinal and text pairs for
///     direct machine-translation providers.
/// </summary>
internal static class TranslationFieldPipeCodec
{
  private const char Separator = '|';

  /// <summary>
  ///     Encodes fields using the established <c>key|text</c> convention.
  /// </summary>
  /// <param name="fields">The fields to encode in order.</param>
  /// <returns>The escaped pipe-delimited provider payload.</returns>
  internal static string Encode(IEnumerable<TranslationField> fields)
  {
    ArgumentNullException.ThrowIfNull(fields);
    var builder = new StringBuilder();
    var names = new HashSet<string>(StringComparer.Ordinal);
    var index = 0;
    foreach (var field in fields)
    {
      field.Validate();
      if (!names.Add(field.Name))
      {
        throw new ArgumentException(
            "Translation field names must be unique.",
            nameof(fields));
      }

      if (builder.Length > 0)
      {
        _ = builder.Append(Separator);
      }

      _ = builder
          .Append(index.ToString(CultureInfo.InvariantCulture))
          .Append(Separator)
          .Append(Escape(field.Text));
      index++;
    }

    return builder.ToString();
  }

  /// <summary>
  ///     Tries to decode a complete ordered pipe-delimited field response.
  /// </summary>
  /// <param name="text">The translated provider response.</param>
  /// <param name="expectedFields">The requested fields in required order.</param>
  /// <param name="fields">The decoded translated fields.</param>
  /// <returns>
  ///     <see langword="true" /> when every expected field is present in
  ///     order with a nonempty translated value; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  internal static bool TryDecode(
      string? text,
      IReadOnlyList<TranslationField> expectedFields,
      out IReadOnlyList<TranslationField> fields)
  {
    ArgumentNullException.ThrowIfNull(expectedFields);
    fields = [];
    if (string.IsNullOrWhiteSpace(text) ||
        !TrySplit(text, out var parts) ||
        parts.Count != expectedFields.Count * 2)
    {
      return false;
    }

    var decoded = new List<TranslationField>(expectedFields.Count);
    for (var index = 0; index < expectedFields.Count; index++)
    {
      if (!int.TryParse(
              parts[index * 2].Trim(),
              NumberStyles.None,
              CultureInfo.InvariantCulture,
              out var ordinal) ||
          ordinal != index ||
          !TryUnescape(parts[(index * 2) + 1].Trim(), out var value) ||
          string.IsNullOrWhiteSpace(value))
      {
        return false;
      }

      decoded.Add(new TranslationField(expectedFields[index].Name, value));
    }

    fields = decoded;
    return true;
  }

  /// <summary>
  ///     Escapes characters that could change the pipe-pair structure.
  /// </summary>
  /// <param name="value">The field value to escape.</param>
  /// <returns>The escaped field value.</returns>
  private static string Escape(string value)
  {
    return value.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("|", "\\|", StringComparison.Ordinal)
        .Replace("\t", "\\t", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal);
  }

  /// <summary>
  ///     Splits one pipe payload without treating escaped pipes as separators.
  /// </summary>
  /// <param name="text">The provider payload to split.</param>
  /// <param name="parts">The escaped key and value parts.</param>
  /// <returns>
  ///     <see langword="true" /> when the escape structure is valid;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private static bool TrySplit(string text, out IReadOnlyList<string> parts)
  {
    var result = new List<string>();
    var builder = new StringBuilder(text.Length);
    for (var index = 0; index < text.Length; index++)
    {
      var current = text[index];
      if (current == '\\')
      {
        if (++index >= text.Length)
        {
          parts = [];
          return false;
        }

        _ = builder.Append(current).Append(text[index]);
        continue;
      }

      if (current == Separator)
      {
        result.Add(builder.ToString());
        _ = builder.Clear();
        continue;
      }

      _ = builder.Append(current);
    }

    result.Add(builder.ToString());
    parts = result;
    return true;
  }

  /// <summary>
  ///     Restores the supported transport escape sequences in one value.
  /// </summary>
  /// <param name="value">The escaped value.</param>
  /// <param name="decoded">The restored value.</param>
  /// <returns>
  ///     <see langword="true" /> when every escape sequence is recognized;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private static bool TryUnescape(string value, out string decoded)
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
        '|' => '|',
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
