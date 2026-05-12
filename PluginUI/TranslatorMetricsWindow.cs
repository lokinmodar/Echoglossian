// <copyright file="TranslatorMetricsWindow.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators;
using System.Globalization;

namespace Echoglossian;

/// <summary>
///     Displays aggregated runtime translator metrics for debugging and
///     operator inspection.
/// </summary>
public sealed class TranslatorMetricsWindow
{
  /// <summary>
  ///     Gets or sets a value indicating whether the metrics window is open.
  /// </summary>
  public bool IsOpen { get; set; }

  /// <summary>
  ///     Draws the translator metrics window.
  /// </summary>
  public void Draw()
  {
    if (!this.IsOpen)
    {
      return;
    }

    ImGui.SetNextWindowSize(new Vector2(1100f, 480f), ImGuiCond.FirstUseEver);
    var isOpen = this.IsOpen;
    if (!ImGui.Begin(
            "Translator Debugger and Metrics",
            ref isOpen,
            ImGuiWindowFlags.NoCollapse))
    {
      this.IsOpen = isOpen;
      ImGui.End();
      return;
    }
    this.IsOpen = isOpen;

    ImGui.TextWrapped(
        "Aggregated runtime metrics for translator activity in this session.");
    ImGui.Separator();

    if (ImGui.Button("Clear Metrics"))
    {
      TranslatorMetricsCollector.Clear();
    }

    var snapshots = TranslatorMetricsCollector.GetSnapshots();
    if (snapshots.Count == 0)
    {
      ImGui.Spacing();
      ImGui.TextWrapped(
          "No translator metrics have been recorded in this session yet.");
      ImGui.End();
      return;
    }

    ImGui.Spacing();
    var totalLiveRequests = snapshots.Sum(snapshot => snapshot.LiveRequestCount);
    var totalFailures = snapshots.Sum(snapshot => snapshot.FailureCount);
    var totalShortCircuits = snapshots.Sum(snapshot => snapshot.ShortCircuitCount);
    ImGui.Text(
        $"Engines: {snapshots.Count}  |  Live requests: {totalLiveRequests}  |  Failures: {totalFailures}  |  Short circuits: {totalShortCircuits}");

    if (ImGui.BeginTable(
            "##TranslatorMetricsTable",
            10,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg |
            ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY |
            ImGuiTableFlags.SizingStretchProp,
            new Vector2(-1f, -1f)))
    {
      ImGui.TableSetupColumn("Engine");
      ImGui.TableSetupColumn("Live Requests");
      ImGui.TableSetupColumn("Successes");
      ImGui.TableSetupColumn("Failures");
      ImGui.TableSetupColumn("Short Circuits");
      ImGui.TableSetupColumn("Avg ms");
      ImGui.TableSetupColumn("Max ms");
      ImGui.TableSetupColumn("Last ms");
      ImGui.TableSetupColumn("Last Request UTC");
      ImGui.TableSetupColumn("Last Failure");
      ImGui.TableHeadersRow();

      foreach (var snapshot in snapshots)
      {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(snapshot.EngineName);
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(snapshot.LiveRequestCount.ToString(CultureInfo.InvariantCulture));
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(snapshot.SuccessCount.ToString(CultureInfo.InvariantCulture));
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(snapshot.FailureCount.ToString(CultureInfo.InvariantCulture));
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(snapshot.ShortCircuitCount.ToString(CultureInfo.InvariantCulture));
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(snapshot.AverageLatencyMs.ToString("F1", CultureInfo.InvariantCulture));
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(snapshot.MaxLatencyMs.ToString("F1", CultureInfo.InvariantCulture));
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(snapshot.LastLatencyMs.ToString("F1", CultureInfo.InvariantCulture));
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(
            snapshot.LastRequestAtUtc?.ToString("u", CultureInfo.InvariantCulture) ??
            "-");
        ImGui.TableNextColumn();
        ImGui.TextWrapped(snapshot.LastFailureReason ?? "-");
      }

      ImGui.EndTable();
    }

    ImGui.End();
  }
}
