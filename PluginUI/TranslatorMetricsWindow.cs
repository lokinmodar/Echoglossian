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
  private readonly Func<Task<NativeUI.AddonHandlers.Talk.VisibleDialogueRetranslationResult>>
      retranslateVisibleDialogueAsync;
  private Task<NativeUI.AddonHandlers.Talk.VisibleDialogueRetranslationResult>?
      activeRetranslationTask;
  private string? lastRetranslationMessage;
  private bool? lastRetranslationSucceeded;

  /// <summary>
  ///     Initializes a new instance of the <see cref="TranslatorMetricsWindow" />
  ///     class.
  /// </summary>
  /// <param name="retranslateVisibleDialogueAsync">
  ///     Delegate used to explicitly retranslate the currently visible dialogue
  ///     line and persist the refreshed result.
  /// </param>
  public TranslatorMetricsWindow(
      Func<Task<NativeUI.AddonHandlers.Talk.VisibleDialogueRetranslationResult>>
          retranslateVisibleDialogueAsync)
  {
    this.retranslateVisibleDialogueAsync = retranslateVisibleDialogueAsync;
  }

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

    this.ResolveCompletedRetranslationTask();

    var isRetranslationRunning = this.activeRetranslationTask is { IsCompleted: false };
    if (isRetranslationRunning)
    {
      ImGui.BeginDisabled();
    }

    if (ImGui.Button("Retranslate Visible Dialogue And Persist"))
    {
      this.lastRetranslationMessage = null;
      this.lastRetranslationSucceeded = null;
      this.activeRetranslationTask = this.retranslateVisibleDialogueAsync();
    }

    if (isRetranslationRunning)
    {
      ImGui.EndDisabled();
    }

    ImGui.SameLine();
    if (ImGui.Button("Clear Metrics"))
    {
      TranslatorMetricsCollector.Clear();
    }

    ImGui.SameLine();
    if (ImGui.Button("Clear Dialogue Sessions"))
    {
      DialogueTranslationSessionStore.Clear();
    }

    if (isRetranslationRunning)
    {
      ImGui.Spacing();
      ImGui.TextWrapped(
          "Retranslating the currently visible dialogue line...");
    }
    else if (!string.IsNullOrWhiteSpace(this.lastRetranslationMessage))
    {
      var messageColor = this.lastRetranslationSucceeded == true
          ? new Vector4(0.45f, 0.9f, 0.55f, 1f)
          : new Vector4(0.95f, 0.6f, 0.35f, 1f);
      ImGui.Spacing();
      ImGui.PushStyleColor(ImGuiCol.Text, messageColor);
      ImGui.TextWrapped(this.lastRetranslationMessage);
      ImGui.PopStyleColor();
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
    var totalContextAwareRequests = snapshots.Sum(snapshot => snapshot.ContextAwareRequestCount);
    var totalFailures = snapshots.Sum(snapshot => snapshot.FailureCount);
    var totalShortCircuits = snapshots.Sum(snapshot => snapshot.ShortCircuitCount);
    var dialogueSessionSnapshots = DialogueTranslationSessionStore.GetSnapshots();
    ImGui.Text(
        $"Engines: {snapshots.Count}  |  Live requests: {totalLiveRequests}  |  Context-aware: {totalContextAwareRequests}  |  Failures: {totalFailures}  |  Short circuits: {totalShortCircuits}");
    ImGui.Text(
        $"Dialogue sessions: {dialogueSessionSnapshots.Count}");

    var availableHeight = ImGui.GetContentRegionAvail().Y;
    var dialogueTableHeight = dialogueSessionSnapshots.Count == 0
        ? 0f
        : 150f;
    var metricsTableHeight = Math.Max(
        availableHeight - dialogueTableHeight - (dialogueSessionSnapshots.Count == 0 ? 0f : 36f),
        180f);

    if (ImGui.BeginTable(
            "##TranslatorMetricsTable",
            13,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg |
            ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY |
            ImGuiTableFlags.SizingStretchProp,
            new Vector2(-1f, metricsTableHeight)))
    {
      ImGui.TableSetupColumn("Engine");
      ImGui.TableSetupColumn("Provider");
      ImGui.TableSetupColumn("Model");
      ImGui.TableSetupColumn("Live Requests");
      ImGui.TableSetupColumn("Context-Aware");
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
        ImGui.TextUnformatted(snapshot.ProviderName ?? "-");
        ImGui.TableNextColumn();
        ImGui.TextWrapped(snapshot.ModelName ?? "-");
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(snapshot.LiveRequestCount.ToString(CultureInfo.InvariantCulture));
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(snapshot.ContextAwareRequestCount.ToString(CultureInfo.InvariantCulture));
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

    if (dialogueSessionSnapshots.Count > 0)
    {
      ImGui.Spacing();
      ImGui.TextWrapped(
          "Runtime-only dialogue sessions currently retained for context-aware translation.");
      if (ImGui.BeginTable(
              "##DialogueSessionTable",
              5,
              ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg |
              ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY |
              ImGuiTableFlags.SizingStretchProp,
              new Vector2(-1f, dialogueTableHeight)))
      {
        ImGui.TableSetupColumn("Namespace");
        ImGui.TableSetupColumn("Session Key");
        ImGui.TableSetupColumn("Last Speaker");
        ImGui.TableSetupColumn("Retained Turns");
        ImGui.TableSetupColumn("Last Observed UTC");
        ImGui.TableHeadersRow();

        foreach (var sessionSnapshot in dialogueSessionSnapshots)
        {
          ImGui.TableNextRow();
          ImGui.TableNextColumn();
          ImGui.TextUnformatted(sessionSnapshot.SessionNamespace);
          ImGui.TableNextColumn();
          ImGui.TextWrapped(sessionSnapshot.SessionKey);
          ImGui.TableNextColumn();
          ImGui.TextUnformatted(string.IsNullOrWhiteSpace(sessionSnapshot.LastSpeakerName)
              ? "-"
              : sessionSnapshot.LastSpeakerName);
          ImGui.TableNextColumn();
          ImGui.TextUnformatted(sessionSnapshot.RetainedTurnCount.ToString(CultureInfo.InvariantCulture));
          ImGui.TableNextColumn();
          ImGui.TextUnformatted(
              sessionSnapshot.LastObservedAtUtc.ToString("u", CultureInfo.InvariantCulture));
        }

        ImGui.EndTable();
      }
    }

    ImGui.End();
  }

  /// <summary>
  ///     Resolves a completed visible-dialogue retranslation task into the
  ///     session-scoped status message shown by the debugger window.
  /// </summary>
  private void ResolveCompletedRetranslationTask()
  {
    if (this.activeRetranslationTask is not { IsCompleted: true } completedTask)
    {
      return;
    }

    this.activeRetranslationTask = null;

    if (completedTask.IsFaulted)
    {
      this.lastRetranslationSucceeded = false;
      this.lastRetranslationMessage =
          "Visible dialogue retranslation failed unexpectedly before a result could be reported.";
      return;
    }

    if (completedTask.IsCanceled)
    {
      this.lastRetranslationSucceeded = false;
      this.lastRetranslationMessage =
          "Visible dialogue retranslation was canceled before completion.";
      return;
    }

    var result = completedTask.GetAwaiter().GetResult();
    this.lastRetranslationSucceeded = result.Success;
    this.lastRetranslationMessage = result.Message;
  }
}
