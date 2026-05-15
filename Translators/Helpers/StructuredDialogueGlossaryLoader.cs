// <copyright file="StructuredDialogueGlossaryLoader.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Text.Json;

namespace Echoglossian.Translators.Helpers;

/// <summary>
///     Loads and normalizes one structured dialogue glossary document from a
///     plugin-managed JSON file.
/// </summary>
public static class StructuredDialogueGlossaryLoader
{
    /// <summary>
    ///     Loads one glossary file from disk.
    /// </summary>
    /// <param name="filePath">The operator-configured glossary file path.</param>
    /// <returns>The normalized glossary load result.</returns>
    public static StructuredDialogueGlossaryLoadResult LoadFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return new StructuredDialogueGlossaryLoadResult(
                false,
                [],
                0,
                "Missing glossary file path.");
        }

        if (!File.Exists(filePath))
        {
            return new StructuredDialogueGlossaryLoadResult(
                false,
                [],
                0,
                "Glossary file does not exist.");
        }

        try
        {
            var json = File.ReadAllText(filePath);
            return LoadFromJson(json);
        }
        catch (Exception ex)
        {
            return new StructuredDialogueGlossaryLoadResult(
                false,
                [],
                0,
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    ///     Loads one glossary document from a raw JSON payload.
    /// </summary>
    /// <param name="json">The raw JSON payload.</param>
    /// <returns>The normalized glossary load result.</returns>
    public static StructuredDialogueGlossaryLoadResult LoadFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new StructuredDialogueGlossaryLoadResult(
                false,
                [],
                0,
                "Glossary file was empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var entriesElement = ResolveEntriesElement(document.RootElement);
            if (!entriesElement.HasValue ||
                entriesElement.Value.ValueKind != JsonValueKind.Array)
            {
                return new StructuredDialogueGlossaryLoadResult(
                    false,
                    [],
                    0,
                    "Glossary JSON must be an array or an object with an entries array.");
            }

            var entries = new List<StructuredDialogueGlossaryEntry>();
            var skippedEntryCount = 0;
            foreach (var element in entriesElement.Value.EnumerateArray())
            {
                if (TryNormalizeEntry(element, out var entry))
                {
                    entries.Add(entry);
                }
                else
                {
                    skippedEntryCount++;
                }
            }

            return new StructuredDialogueGlossaryLoadResult(
                true,
                entries,
                skippedEntryCount,
                null);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return new StructuredDialogueGlossaryLoadResult(
                false,
                [],
                0,
                $"JsonException: {ex.Message}");
        }
    }

    /// <summary>
    ///     Resolves the effective entries array from a glossary root element.
    /// </summary>
    /// <param name="rootElement">The parsed JSON root element.</param>
    /// <returns>The entries array element when present.</returns>
    private static JsonElement? ResolveEntriesElement(JsonElement rootElement)
    {
        if (rootElement.ValueKind == JsonValueKind.Array)
        {
            return rootElement;
        }

        if (rootElement.ValueKind == JsonValueKind.Object &&
            rootElement.TryGetProperty("entries", out var entriesElement))
        {
            return entriesElement;
        }

        return null;
    }

    /// <summary>
    ///     Attempts to normalize one glossary row into the shared runtime
    ///     record contract.
    /// </summary>
    /// <param name="element">The raw glossary row element.</param>
    /// <param name="entry">The normalized glossary entry.</param>
    /// <returns>
    ///     <see langword="true" /> when the row was valid enough to keep;
    ///     otherwise <see langword="false" />.
    /// </returns>
    private static bool TryNormalizeEntry(
        JsonElement element,
        out StructuredDialogueGlossaryEntry entry)
    {
        entry = default;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var sourceText = ReadString(
            element,
            "source_text",
            "source");
        var targetText = ReadString(
            element,
            "target_text",
            "target");
        if (string.IsNullOrWhiteSpace(sourceText) ||
            string.IsNullOrWhiteSpace(targetText))
        {
            return false;
        }

        entry = new StructuredDialogueGlossaryEntry(
            sourceText.Trim(),
            targetText.Trim(),
            ReadString(element, "comment"),
            ReadString(element, "source_language"),
            ReadString(element, "target_language"));
        return true;
    }

    /// <summary>
    ///     Reads the first matching string property from one JSON object.
    /// </summary>
    /// <param name="element">The source JSON object.</param>
    /// <param name="propertyNames">The candidate property names.</param>
    /// <returns>The string value when present.</returns>
    private static string? ReadString(
        JsonElement element,
        params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!element.TryGetProperty(propertyName, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }
        }

        return null;
    }
}
