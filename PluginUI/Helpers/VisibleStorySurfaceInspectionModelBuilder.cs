// <copyright file="VisibleStorySurfaceInspectionModelBuilder.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Helpers;

/// <summary>
///     Builds reusable inspection-table models for the latest visible
///     story-surface diagnostics snapshot.
/// </summary>
public static class VisibleStorySurfaceInspectionModelBuilder
{
  /// <summary>
  /// Builds the shared inspection table columns used by the debugger.
  /// </summary>
  /// <returns>The shared debugger columns.</returns>
  public static IReadOnlyList<InspectionColumnDefinition> BuildColumns()
  {
    return
    [
      new InspectionColumnDefinition(
          Resources.TranslatorDebuggerVisibleStorySurfaceFieldColumn),
      new InspectionColumnDefinition(
          Resources.TranslatorDebuggerVisibleStorySurfaceValueColumn,
          ImGuiTableColumnFlags.WidthStretch),
    ];
  }

  /// <summary>
  /// Builds display-ready rows for a visible story-surface diagnostics snapshot.
  /// </summary>
  /// <param name="snapshot">The snapshot to render.</param>
  /// <returns>The display-ready inspection rows.</returns>
  public static IReadOnlyList<InspectionRow> BuildRows(
      VisibleStorySurfaceDiagnosticsSnapshot snapshot)
  {
    var rows = new List<InspectionRow>
    {
      BuildRow(
          Resources.TranslatorDebuggerVisibleStorySurfaceFieldSurface,
          VisibleStorySurfaceText.ResolveSurfaceName(snapshot.Surface)),
      BuildRow(
          Resources.TranslatorDebuggerVisibleStorySurfaceFieldProvenance,
          VisibleStorySurfaceText.ResolveProvenanceLabel(snapshot.Provenance)),
      BuildRow(
          Resources.TranslatorDebuggerVisibleStorySurfaceFieldDbTable,
          snapshot.TableName),
      BuildRow(
          Resources.TranslatorDebuggerVisibleStorySurfaceFieldEffectiveEngine,
          snapshot.EffectiveTranslationEngineId.ToString(CultureInfo.InvariantCulture)),
      BuildRow(
          Resources.TranslatorDebuggerVisibleStorySurfaceFieldRuntimeOnlyDialogueContext,
          snapshot.UsedRuntimeOnlyDialogueContext
              ? Resources.TranslatorDebuggerYes
              : Resources.TranslatorDebuggerNo),
      BuildRow(
          Resources.TranslatorDebuggerVisibleStorySurfaceFieldObservedUtc,
          snapshot.ObservedAtUtc.ToString("u", CultureInfo.InvariantCulture)),
    };

    AddOptionalRow(
        rows,
        Resources.TranslatorDebuggerVisibleStorySurfaceFieldOriginalSpeaker,
        snapshot.OriginalSpeakerText);
    AddOptionalRow(
        rows,
        Resources.TranslatorDebuggerVisibleStorySurfaceFieldOriginalText,
        snapshot.OriginalText);
    AddOptionalRow(
        rows,
        Resources.TranslatorDebuggerVisibleStorySurfaceFieldOriginalOptions,
        snapshot.OriginalOptionsText);
    AddOptionalRow(
        rows,
        Resources.TranslatorDebuggerVisibleStorySurfaceFieldTranslatedSpeaker,
        snapshot.TranslatedSpeakerText);
    AddOptionalRow(
        rows,
        Resources.TranslatorDebuggerVisibleStorySurfaceFieldTranslatedText,
        snapshot.TranslatedText);
    AddOptionalRow(
        rows,
        Resources.TranslatorDebuggerVisibleStorySurfaceFieldTranslatedOptions,
        snapshot.TranslatedOptionsText);

    if (!string.IsNullOrWhiteSpace(snapshot.LastRetranslationMessage))
    {
      var retranslationStatus = snapshot.LastRetranslationSuccess switch
      {
        true => Resources.TranslatorDebuggerSucceeded,
        false => Resources.TranslatorDebuggerFailed,
        _ => Resources.TranslatorDebuggerUnknown,
      };
      rows.Add(
          BuildRow(
              Resources.TranslatorDebuggerVisibleStorySurfaceFieldLastRetranslation,
              $"{retranslationStatus}: {snapshot.LastRetranslationMessage}"));
    }

    return rows;
  }

  /// <summary>
  /// Builds one debugger inspection row.
  /// </summary>
  /// <param name="field">The field label.</param>
  /// <param name="value">The field value.</param>
  /// <returns>The display-ready row.</returns>
  private static InspectionRow BuildRow(string field, string value)
  {
    return new InspectionRow(
      [
        new InspectionCell(field),
        InspectionCellFormatter.Format(value),
      ]);
  }

  /// <summary>
  /// Adds one optional debugger inspection row when a value is available.
  /// </summary>
  /// <param name="rows">The row list to append to.</param>
  /// <param name="field">The field label.</param>
  /// <param name="value">The optional field value.</param>
  private static void AddOptionalRow(
      ICollection<InspectionRow> rows,
      string field,
      string value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return;
    }

    rows.Add(BuildRow(field, value));
  }
}
