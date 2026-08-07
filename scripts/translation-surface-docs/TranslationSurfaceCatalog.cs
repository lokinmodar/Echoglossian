// <copyright file="TranslationSurfaceCatalog.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Text.Json.Serialization;

namespace TranslationSurfaceDocs;

/// <summary>
/// Represents the canonical translation-surface documentation catalog.
/// </summary>
internal sealed record TranslationSurfaceCatalog(
    [property: JsonPropertyName("modeFamilies")]
    IReadOnlyList<TranslationSurfaceModeFamily> ModeFamilies,
    [property: JsonPropertyName("sections")]
    IReadOnlyList<TranslationSurfaceSection> Sections,
    [property: JsonPropertyName("locales")]
    IReadOnlyList<TranslationSurfaceLocale> Locales,
    [property: JsonPropertyName("surfaces")]
    IReadOnlyList<TranslationSurfaceEntry> Surfaces)
{
    /// <summary>
    /// Gets the deterministic output timestamp policy.
    /// </summary>
    [JsonPropertyName("generatedAtPolicy")]
    public string GeneratedAtPolicy { get; init; } = string.Empty;
}

/// <summary>
/// Represents one named set of presentation modes.
/// </summary>
internal sealed record TranslationSurfaceModeFamily(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("modes")] IReadOnlyList<string> Modes);

/// <summary>
/// Represents one catalog section.
/// </summary>
internal sealed record TranslationSurfaceSection(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("displayName")] string DisplayName);

/// <summary>
/// Represents one localized generated document target.
/// </summary>
internal sealed record TranslationSurfaceLocale(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("allowEnglishNoteFallback")]
    bool AllowEnglishNoteFallback = false);

/// <summary>
/// Represents one documented translation surface.
/// </summary>
internal sealed record TranslationSurfaceEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("section")] string Section,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("configToggle")] string ConfigToggle,
    [property: JsonPropertyName("modeFamilyId")] string ModeFamilyId,
    [property: JsonPropertyName("releaseStatus")] string ReleaseStatus,
    [property: JsonPropertyName("notes")]
    IReadOnlyDictionary<string, string> Notes,
    [property: JsonPropertyName("runtime")] TranslationSurfaceRuntime Runtime,
    [property: JsonPropertyName("docs")] IReadOnlyList<string> Docs,
    [property: JsonPropertyName("requiredCodeAnchors")]
    IReadOnlyList<string> RequiredCodeAnchors);

/// <summary>
/// Represents the runtime and persistence behavior of one surface.
/// </summary>
internal sealed record TranslationSurfaceRuntime(
    [property: JsonPropertyName("translationModel")] string TranslationModel,
    [property: JsonPropertyName("cache")] string Cache,
    [property: JsonPropertyName("dbOwner")] string DbOwner,
    [property: JsonPropertyName("dbRead")] string DbRead,
    [property: JsonPropertyName("dbWrite")] string DbWrite);

/// <summary>
/// Represents one catalog validation result.
/// </summary>
/// <param name="Code">The stable validation code.</param>
/// <param name="Message">The diagnostic message.</param>
/// <param name="SurfaceId">The associated surface identifier, when available.</param>
internal sealed record ValidationIssue(string Code, string Message, string? SurfaceId);
