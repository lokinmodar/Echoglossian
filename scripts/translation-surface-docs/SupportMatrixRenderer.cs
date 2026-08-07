// <copyright file="SupportMatrixRenderer.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Text;

namespace TranslationSurfaceDocs;

/// <summary>
/// Renders localized translation-surface support matrices from the canonical catalog.
/// </summary>
internal static class SupportMatrixRenderer
{
    /// <summary>
    /// Renders one support matrix for a declared catalog locale.
    /// </summary>
    /// <param name="catalog">The validated translation-surface catalog.</param>
    /// <param name="locale">The requested catalog locale.</param>
    /// <returns>The generated localized Markdown document.</returns>
    public static GeneratedDocument Render(TranslationSurfaceCatalog catalog, string locale)
    {
        TranslationSurfaceLocale target = catalog.Locales.SingleOrDefault(item => item.Id == locale)
            ?? new TranslationSurfaceLocale(locale, locale == "en" ? "docs/translation-surface-support-matrix.md" : string.Empty);
        SupportMatrixLocaleResourceSet labels = SupportMatrixLocaleResources.ForLocale(locale);
        IReadOnlyDictionary<string, string> modeFamilyNames = catalog.ModeFamilies
            .ToDictionary(family => family.Id, family => family.DisplayName, StringComparer.Ordinal);
        StringBuilder builder = new();
        builder.AppendLine("<!--");
        builder.AppendLine("  Copyright (c) lokinmodar. All rights reserved.");
        builder.AppendLine("  Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.");
        builder.AppendLine("-->");
        builder.AppendLine();
        builder.AppendLine($"# {labels.Title}");
        builder.AppendLine();
        builder.AppendLine($"## {labels.ModeFamiliesHeading}");
        builder.AppendLine();
        builder.AppendLine($"| {labels.ModeFamilyHeader} | {labels.ModesHeader} |");
        builder.AppendLine("| --- | --- |");
        foreach (TranslationSurfaceModeFamily family in catalog.ModeFamilies)
        {
            builder.AppendLine($"| {family.DisplayName} | {string.Join("; ", family.Modes)} |");
        }

        foreach (TranslationSurfaceSection section in catalog.Sections)
        {
            builder.AppendLine();
            builder.AppendLine($"## {labels.SectionHeadings.GetValueOrDefault(section.Id, section.DisplayName)}");
            builder.AppendLine();
            builder.AppendLine($"| {labels.SurfaceHeader} | {labels.ConfigToggleHeader} | {labels.ModesHeader} | {labels.NotesHeader} | {labels.ReleaseStatusHeader} |");
            builder.AppendLine("| --- | --- | --- | --- | --- |");
            foreach (TranslationSurfaceEntry surface in catalog.Surfaces.Where(item => item.Section == section.Id))
            {
                builder.AppendLine($"| {surface.DisplayName} | `{surface.ConfigToggle}` | {modeFamilyNames[surface.ModeFamilyId]} | {ResolveNote(catalog, target, surface, locale)} | {surface.ReleaseStatus} |");
            }
        }

        return new GeneratedDocument(target.Path, builder.ToString());
    }

    private static string ResolveNote(TranslationSurfaceCatalog catalog, TranslationSurfaceLocale target, TranslationSurfaceEntry surface, string locale)
    {
        if (surface.Notes.TryGetValue(locale, out string? localizedNote))
        {
            return localizedNote;
        }

        if ((locale == "en" || catalog.Locales.Any(item => item.Id == target.Id)) &&
            surface.Notes.TryGetValue("en", out string? englishNote))
        {
            return englishNote;
        }

        throw new InvalidOperationException($"Surface '{surface.Id}' does not provide a '{locale}' note and the catalog does not permit English fallback.");
    }
}
