// <copyright file="RuntimeMapRenderer.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Text;
using System.Text.Json;

namespace TranslationSurfaceDocs;

/// <summary>
/// Renders the deterministic runtime-map documentation artifacts.
/// </summary>
internal static class RuntimeMapRenderer
{
    /// <summary>
    /// Renders the human-readable runtime map.
    /// </summary>
    /// <param name="catalog">The validated translation-surface catalog.</param>
    /// <returns>The generated Markdown document.</returns>
    public static GeneratedDocument RenderMarkdown(TranslationSurfaceCatalog catalog)
    {
        StringBuilder builder = new();
        builder.AppendLine("# Translation Surface Runtime Map");
        builder.AppendLine();
        builder.AppendLine("## Runtime Families");
        builder.AppendLine();
        builder.AppendLine("| Family | Presentation Modes |");
        builder.AppendLine("| --- | --- |");

        foreach (TranslationSurfaceModeFamily family in catalog.ModeFamilies)
        {
            builder.AppendLine($"| {family.DisplayName} (`{family.Id}`) | {string.Join("; ", family.Modes)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Surface Runtime Details");
        builder.AppendLine();
        builder.AppendLine("| Surface | Family | Translation Model | Cache | DB Owner | DB Read | DB Write | Supporting Docs |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");

        foreach (TranslationSurfaceEntry surface in catalog.Surfaces)
        {
            builder.AppendLine(
                $"| {surface.DisplayName} | {surface.ModeFamilyId} | {surface.Runtime.TranslationModel} | {surface.Runtime.Cache} | {surface.Runtime.DbOwner} | {surface.Runtime.DbRead} | {surface.Runtime.DbWrite} | {string.Join("<br>", surface.Docs)} |");
        }

        return new GeneratedDocument("docs/translation-surface-runtime-map.md", builder.ToString());
    }

    /// <summary>
    /// Renders the machine-readable runtime map.
    /// </summary>
    /// <param name="catalog">The validated translation-surface catalog.</param>
    /// <returns>The generated JSON document.</returns>
    public static GeneratedDocument RenderJson(TranslationSurfaceCatalog catalog)
    {
        string content = JsonSerializer.Serialize(catalog, new JsonSerializerOptions
        {
            WriteIndented = true,
        });

        return new GeneratedDocument("docs/translation-surface-runtime-map.json", content + Environment.NewLine);
    }
}
