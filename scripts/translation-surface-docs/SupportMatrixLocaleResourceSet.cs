// <copyright file="SupportMatrixLocaleResourceSet.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace TranslationSurfaceDocs;

/// <summary>
/// Defines deterministic labels used to render one support-matrix locale.
/// </summary>
/// <param name="Title">The document title.</param>
/// <param name="ModeFamiliesHeading">The mode-family section heading.</param>
/// <param name="ModeFamilyHeader">The mode-family table header.</param>
/// <param name="SurfaceHeader">The surface table header.</param>
/// <param name="ConfigToggleHeader">The configuration-toggle table header.</param>
/// <param name="ModesHeader">The mode table header.</param>
/// <param name="NotesHeader">The notes table header.</param>
/// <param name="ReleaseStatusHeader">The release-status table header.</param>
/// <param name="ModeFamilyNames">The localized mode-family labels keyed by family identifier.</param>
/// <param name="ModeLabels">The localized mode labels keyed by canonical mode string.</param>
/// <param name="ReleaseStatuses">The localized release statuses keyed by canonical status string.</param>
/// <param name="SectionHeadings">The localized headings keyed by catalog section identifier.</param>
internal sealed record SupportMatrixLocaleResourceSet(
    string Title,
    string ModeFamiliesHeading,
    string ModeFamilyHeader,
    string SurfaceHeader,
    string ConfigToggleHeader,
    string ModesHeader,
    string NotesHeader,
    string ReleaseStatusHeader,
    IReadOnlyDictionary<string, string> ModeFamilyNames,
    IReadOnlyDictionary<string, string> ModeLabels,
    IReadOnlyDictionary<string, string> ReleaseStatuses,
    IReadOnlyDictionary<string, string> SectionHeadings);
