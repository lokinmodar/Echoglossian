// <copyright file="TranslatorMetricsWindow.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Helpers;
using Echoglossian.Translators;
using Echoglossian.Translators.OpenAI;
using System.Globalization;

namespace Echoglossian;

/// <summary>
///     Displays aggregated runtime translator metrics for debugging and
///     operator inspection.
/// </summary>
public sealed class TranslatorMetricsWindow
{
  private readonly Config config;
  private readonly Action<string> openDbEditorForTable;
  private readonly Func<Task<NativeUI.AddonHandlers.Talk.VisibleDialogueRetranslationResult>>
      retranslateVisibleDialogueAsync;
  private readonly InspectionTableView visibleStorySurfaceInspectionTable;
  private Task<NativeUI.AddonHandlers.Talk.VisibleDialogueRetranslationResult>?
      activeRetranslationTask;
  private VisibleStorySurfaceDiagnosticsSnapshot? activeVisibleStorySurfaceSnapshot;
  private string? lastRetranslationMessage;
  private bool? lastRetranslationSucceeded;

  /// <summary>
  ///     Initializes a new instance of the <see cref="TranslatorMetricsWindow" />
  ///     class.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <param name="openDbEditorForTable">
  ///     Delegate used to open the DB manager on the requested table.
  /// </param>
  /// <param name="retranslateVisibleDialogueAsync">
  ///     Delegate used to explicitly retranslate the currently visible
  ///     story-facing text and persist the refreshed result.
  /// </param>
  public TranslatorMetricsWindow(
      Config config,
      Action<string> openDbEditorForTable,
      Func<Task<NativeUI.AddonHandlers.Talk.VisibleDialogueRetranslationResult>>
          retranslateVisibleDialogueAsync)
  {
    this.config = config;
    this.openDbEditorForTable = openDbEditorForTable;
    this.retranslateVisibleDialogueAsync = retranslateVisibleDialogueAsync;
    this.visibleStorySurfaceInspectionTable = new InspectionTableView(
        tableId: "visibleStorySurfaceInspection",
        getColumns: VisibleStorySurfaceInspectionModelBuilder.BuildColumns,
        getRows: this.BuildVisibleStorySurfaceInspectionRows,
        noRecordsLoadedMessage: Resources.TranslatorDebuggerVisibleStorySurfaceNoSnapshot,
        noRecordsFoundMessage: Resources.TranslatorDebuggerVisibleStorySurfaceNoSnapshot,
        noColumnsMessage: Resources.TranslatorDebuggerVisibleStorySurfaceNoColumns);
  }

  /// <summary>
  ///     Gets or sets a value indicating whether the metrics window is open.
  /// </summary>
  public bool IsOpen { get; set; }

  /// <summary>
  ///     Gets the bounds captured during the most recent successful draw.
  /// </summary>
  internal RectangleF? LastWindowBounds { get; private set; }

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
            Resources.TranslatorDebuggerWindowTitle,
            ref isOpen,
            ImGuiWindowFlags.NoCollapse))
    {
      this.IsOpen = isOpen;
      this.CaptureWindowBounds();
      ImGui.End();
      return;
    }
    this.IsOpen = isOpen;

    ImGui.TextWrapped(
        Resources.TranslatorDebuggerWindowDescription);
    ImGui.Separator();
    this.DrawDialogueOverrideStatus();
    ImGui.Separator();
    this.DrawDialogueGlossaryStatus();
    ImGui.Separator();
    this.DrawOpenAiProviderStatus();
    ImGui.Separator();

    this.ResolveCompletedRetranslationTask();

    var isRetranslationRunning = this.activeRetranslationTask is { IsCompleted: false };
    if (isRetranslationRunning)
    {
      ImGui.BeginDisabled();
    }

    if (ImGui.Button(
            Resources.TranslatorDebuggerRetranslateVisibleDialogueAndPersist))
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
    if (ImGui.Button(Resources.TranslatorDebuggerClearMetrics))
    {
      TranslatorMetricsCollector.Clear();
    }

    ImGui.SameLine();
    if (ImGui.Button(Resources.TranslatorDebuggerClearDialogueSessions))
    {
      DialogueTranslationSessionStore.Clear();
    }

    if (isRetranslationRunning)
    {
      ImGui.Spacing();
      ImGui.TextWrapped(
          Resources.TranslatorDebuggerRetranslatingVisibleDialogue);
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

    ImGui.Spacing();
    this.DrawVisibleStorySurfaceDiagnostics();
    ImGui.Separator();

    var snapshots = TranslatorMetricsCollector.GetSnapshots();
    if (snapshots.Count == 0)
    {
      ImGui.Spacing();
      ImGui.TextWrapped(
          Resources.TranslatorDebuggerNoMetricsRecorded);
      this.CaptureWindowBounds();
      ImGui.End();
      return;
    }

    ImGui.Spacing();
    var totalLiveRequests = snapshots.Sum(snapshot => snapshot.LiveRequestCount);
    var totalContextAwareRequests = snapshots.Sum(snapshot => snapshot.ContextAwareRequestCount);
    var totalStructuredRequests = snapshots.Sum(snapshot => snapshot.StructuredRequestCount);
    var totalStructuredSuccesses = snapshots.Sum(snapshot => snapshot.StructuredSuccessCount);
    var totalGlossaryStructuredRequests =
        snapshots.Sum(snapshot => snapshot.GlossaryAugmentedStructuredRequestCount);
    var totalFailures = snapshots.Sum(snapshot => snapshot.FailureCount);
    var totalShortCircuits = snapshots.Sum(snapshot => snapshot.ShortCircuitCount);
    var dialogueSessionSnapshots = DialogueTranslationSessionStore.GetSnapshots();
    ImGui.Text(
        string.Format(
            CultureInfo.CurrentCulture,
            Resources.TranslatorDebuggerEngineSummary,
            snapshots.Count,
            totalLiveRequests,
            totalContextAwareRequests,
            totalFailures,
            totalShortCircuits));
    ImGui.Text(
        string.Format(
            CultureInfo.CurrentCulture,
            Resources.TranslatorDebuggerStructuredSummary,
            totalStructuredRequests,
            totalStructuredSuccesses,
            totalGlossaryStructuredRequests));
    ImGui.Text(
        string.Format(
            CultureInfo.CurrentCulture,
            Resources.TranslatorDebuggerDialogueSessionsCount,
            dialogueSessionSnapshots.Count));

    var availableHeight = ImGui.GetContentRegionAvail().Y;
    var dialogueTableHeight = dialogueSessionSnapshots.Count == 0
        ? 0f
        : 150f;
    var metricsTableHeight = Math.Max(
        availableHeight - dialogueTableHeight - (dialogueSessionSnapshots.Count == 0 ? 0f : 36f),
        180f);

    if (ImGui.BeginTable(
            "##TranslatorMetricsTable",
            17,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg |
            ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY |
            ImGuiTableFlags.SizingStretchProp,
            new Vector2(-1f, metricsTableHeight)))
    {
      ImGui.TableSetupColumn(Resources.TranslatorDebuggerMetricsTableEngine);
      ImGui.TableSetupColumn(Resources.TranslatorDebuggerMetricsTableProvider);
      ImGui.TableSetupColumn(Resources.TranslatorDebuggerMetricsTableModel);
      ImGui.TableSetupColumn(Resources.TranslatorDebuggerMetricsTableLiveRequests);
      ImGui.TableSetupColumn(Resources.TranslatorDebuggerMetricsTableContextAware);
      ImGui.TableSetupColumn(Resources.TranslatorDebuggerMetricsTableStructured);
      ImGui.TableSetupColumn(Resources.TranslatorDebuggerMetricsTableStructuredOk);
      ImGui.TableSetupColumn(Resources.TranslatorDebuggerMetricsTableGlossary);
      ImGui.TableSetupColumn(Resources.TranslatorDebuggerMetricsTableSuccesses);
      ImGui.TableSetupColumn(Resources.TranslatorDebuggerMetricsTableFailures);
      ImGui.TableSetupColumn(Resources.TranslatorDebuggerMetricsTableShortCircuits);
      ImGui.TableSetupColumn(Resources.TranslatorDebuggerMetricsTableAverageMs);
      ImGui.TableSetupColumn(Resources.TranslatorDebuggerMetricsTableMaxMs);
      ImGui.TableSetupColumn(Resources.TranslatorDebuggerMetricsTableLastMs);
      ImGui.TableSetupColumn(Resources.TranslatorDebuggerMetricsTableLastRequestUtc);
      ImGui.TableSetupColumn(Resources.TranslatorDebuggerMetricsTableLastFailure);
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
        ImGui.TextUnformatted(snapshot.StructuredRequestCount.ToString(CultureInfo.InvariantCulture));
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(snapshot.StructuredSuccessCount.ToString(CultureInfo.InvariantCulture));
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(snapshot.GlossaryAugmentedStructuredRequestCount.ToString(CultureInfo.InvariantCulture));
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
          Resources.TranslatorDebuggerDialogueSessionDescription);
      if (ImGui.BeginTable(
              "##DialogueSessionTable",
              5,
              ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg |
              ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY |
              ImGuiTableFlags.SizingStretchProp,
              new Vector2(-1f, dialogueTableHeight)))
      {
        ImGui.TableSetupColumn(
            Resources.TranslatorDebuggerDialogueSessionTableNamespace);
        ImGui.TableSetupColumn(
            Resources.TranslatorDebuggerDialogueSessionTableSessionKey);
        ImGui.TableSetupColumn(
            Resources.TranslatorDebuggerDialogueSessionTableLastSpeaker);
        ImGui.TableSetupColumn(
            Resources.TranslatorDebuggerDialogueSessionTableRetainedTurns);
        ImGui.TableSetupColumn(
            Resources.TranslatorDebuggerDialogueSessionTableLastObservedUtc);
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

    this.CaptureWindowBounds();
    ImGui.End();
  }

  /// <summary>
  ///     Captures the current metrics window bounds for screenshot cropping.
  /// </summary>
  private void CaptureWindowBounds()
  {
    this.LastWindowBounds = new RectangleF(
        ImGui.GetWindowPos().X,
        ImGui.GetWindowPos().Y,
        ImGui.GetWindowSize().X,
        ImGui.GetWindowSize().Y);
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
          VisibleStorySurfaceText.GetUnexpectedFailureMessage();
      return;
    }

    if (completedTask.IsCanceled)
    {
      this.lastRetranslationSucceeded = false;
      this.lastRetranslationMessage =
          VisibleStorySurfaceText.GetCanceledMessage();
      return;
    }

    var result = completedTask.GetAwaiter().GetResult();
    this.lastRetranslationSucceeded = result.Success;
    this.lastRetranslationMessage = result.Message;
  }

  /// <summary>
  ///     Builds the reusable inspection rows for the latest retained visible
  ///     story-surface diagnostics snapshot.
  /// </summary>
  /// <returns>The reusable inspection rows for the latest snapshot.</returns>
  private IReadOnlyList<InspectionRow> BuildVisibleStorySurfaceInspectionRows()
  {
    var snapshot = this.activeVisibleStorySurfaceSnapshot ??
        VisibleStorySurfaceDiagnosticsStore.GetLatestSnapshot();
    if (snapshot == null)
    {
      return [];
    }

    return VisibleStorySurfaceInspectionModelBuilder.BuildRows(
        snapshot.Value);
  }

  /// <summary>
  ///     Draws the latest visible story-surface provenance snapshot and DB
  ///     manager handoff controls.
  /// </summary>
  private void DrawVisibleStorySurfaceDiagnostics()
  {
    ImGui.TextWrapped(
        Resources.TranslatorDebuggerVisibleStorySurfaceSectionTitle);
    ImGui.TextWrapped(
        Resources.TranslatorDebuggerVisibleStorySurfaceDescription);

    var snapshot = VisibleStorySurfaceDiagnosticsStore.GetLatestSnapshot();
    if (snapshot == null)
    {
      ImGui.TextWrapped(
          Resources.TranslatorDebuggerVisibleStorySurfaceNoSnapshot);
      return;
    }

    this.activeVisibleStorySurfaceSnapshot = snapshot;
    try
    {
      this.visibleStorySurfaceInspectionTable.Draw();
      if (ImGui.Button(
              Resources.TranslatorDebuggerVisibleStorySurfaceViewInDbManager))
      {
        this.openDbEditorForTable(snapshot.Value.TableName);
      }
    }
    finally
    {
      this.activeVisibleStorySurfaceSnapshot = null;
    }
  }

  /// <summary>
  ///     Draws the current dialogue-family LLM override routing state.
  /// </summary>
  private void DrawDialogueOverrideStatus()
  {
    var state = LlmSurfaceGroupRoutingPolicy.GetDialogueOverrideState(this.config);
    ImGui.TextWrapped(
        string.Format(
            CultureInfo.CurrentCulture,
            Resources.TranslatorDebuggerDialogueOverridePrimaryAndEffective,
            state.PrimaryEngine,
            state.EffectiveDialogueEngine));

    if (!state.OverrideEnabled)
    {
      ImGui.TextWrapped(
          Resources.TranslatorDebuggerDialogueOverrideDisabled);
      return;
    }

    var statusColor = state.OverrideActive
        ? new Vector4(0.45f, 0.9f, 0.55f, 1f)
        : new Vector4(0.95f, 0.6f, 0.35f, 1f);
    var statusMessage = state.OverrideActive
        ? state.SelectedOverrideEngine == state.PrimaryEngine
            ? Resources.TranslatorDebuggerDialogueOverrideMatchesPrimary
            : string.Format(
                CultureInfo.CurrentCulture,
                Resources.TranslatorDebuggerDialogueOverrideActive,
                state.SelectedOverrideEngine)
        : state.OverrideConfigured
            ? Resources.TranslatorDebuggerDialogueOverrideInactiveFallback
            : string.Format(
                CultureInfo.CurrentCulture,
                Resources.TranslatorDebuggerDialogueOverrideNotConfigured,
                state.SelectedOverrideEngine);

    ImGui.PushStyleColor(ImGuiCol.Text, statusColor);
    ImGui.TextWrapped(statusMessage);
    ImGui.PopStyleColor();
  }

  /// <summary>
  ///     Draws the current OpenAI-family provider configuration and live model
  ///     refresh status for operator inspection.
  /// </summary>
  private void DrawOpenAiProviderStatus()
  {
    var settings = OpenAiProviderVariantHelper.ResolveActiveSettings(this.config);
    var isConfigured = TranslationEngineConfigurationHelper.IsConfigured(
        this.config,
        Echoglossian.TransEngines.ChatGPT);
    var refreshSnapshot = OpenAIModelManager.GetRefreshSnapshot(settings.ProviderName);
    var providerVariantDisplayName = ResolveProviderVariantDisplayName(settings.Variant);

    ImGui.TextWrapped(
        string.Format(
            CultureInfo.CurrentCulture,
            Resources.TranslatorDebuggerOpenAiProviderSummary,
            settings.ProviderName,
            providerVariantDisplayName,
            isConfigured
                ? Resources.TranslatorDebuggerYes
                : Resources.TranslatorDebuggerNo));
    ImGui.TextWrapped(
        string.Format(
            CultureInfo.CurrentCulture,
            Resources.TranslatorDebuggerOpenAiEndpoint,
            string.IsNullOrWhiteSpace(settings.BaseUrl) ? "-" : settings.BaseUrl));
    ImGui.TextWrapped(
        string.Format(
            CultureInfo.CurrentCulture,
            Resources.TranslatorDebuggerOpenAiModelSummary,
            string.IsNullOrWhiteSpace(settings.Model) ? "-" : settings.Model,
            settings.UseLiveModelList
                ? Resources.TranslatorDebuggerEnabled
                : Resources.TranslatorDebuggerDisabled));

    var refreshStatus = refreshSnapshot.LastRefreshSucceeded switch
    {
      true => Resources.TranslatorDebuggerSucceeded,
      false => Resources.TranslatorDebuggerFailed,
      _ => Resources.TranslatorDebuggerNeverAttempted,
    };
    ImGui.TextWrapped(
        string.Format(
            CultureInfo.CurrentCulture,
            Resources.TranslatorDebuggerOpenAiRefreshSummary,
            refreshStatus,
            refreshSnapshot.LastRefreshProviderName ?? "-",
            refreshSnapshot.CurrentModelCount));
    ImGui.TextWrapped(
        string.Format(
            CultureInfo.CurrentCulture,
            Resources.TranslatorDebuggerOpenAiRefreshUtc,
            refreshSnapshot.LastRefreshObservedAtUtc?.ToString("u", CultureInfo.InvariantCulture) ?? "-"));

    if (!string.IsNullOrWhiteSpace(refreshSnapshot.LastRefreshUrl))
    {
      ImGui.TextWrapped(
          string.Format(
              CultureInfo.CurrentCulture,
              Resources.TranslatorDebuggerOpenAiRefreshEndpoint,
              refreshSnapshot.LastRefreshUrl));
    }

    if (!string.IsNullOrWhiteSpace(refreshSnapshot.LastRefreshFailureDetail))
    {
      ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.95f, 0.6f, 0.35f, 1f));
      ImGui.TextWrapped(
          string.Format(
              CultureInfo.CurrentCulture,
              Resources.TranslatorDebuggerOpenAiRefreshFailure,
              refreshSnapshot.LastRefreshFailureDetail));
      ImGui.PopStyleColor();
    }
  }

  /// <summary>
  ///     Draws the current structured dialogue glossary configuration and load
  ///     snapshot for operator inspection.
  /// </summary>
  private void DrawDialogueGlossaryStatus()
  {
    var snapshot = StructuredDialogueGlossaryStore.GetSnapshot();
    ImGui.TextWrapped(
        string.Format(
            CultureInfo.CurrentCulture,
            Resources.TranslatorDebuggerDialogueGlossarySummary,
            this.config.EnableDialogueGlossaryInjection
                ? Resources.TranslatorDebuggerEnabled
                : Resources.TranslatorDebuggerDisabled,
            snapshot.EntryCount,
            snapshot.SkippedEntryCount));
    ImGui.TextWrapped(
        string.Format(
            CultureInfo.CurrentCulture,
            Resources.TranslatorDebuggerDialogueGlossaryPath,
            string.IsNullOrWhiteSpace(this.config.DialogueGlossaryFilePath)
                ? "-"
                : this.config.DialogueGlossaryFilePath));

    var loadStatus = snapshot.LastLoadSucceeded switch
    {
      true => Resources.TranslatorDebuggerSucceeded,
      false => Resources.TranslatorDebuggerFailed,
      _ => Resources.TranslatorDebuggerNeverAttempted,
    };
    ImGui.TextWrapped(
        string.Format(
            CultureInfo.CurrentCulture,
            Resources.TranslatorDebuggerDialogueGlossaryLoadStatus,
            loadStatus,
            snapshot.LastLoadObservedAtUtc?.ToString("u", CultureInfo.InvariantCulture) ??
            "-"));

    if (ImGui.Button(
            Resources.TranslatorDebuggerReloadDialogueGlossary))
    {
      StructuredDialogueGlossaryStore.Refresh(
          this.config.DialogueGlossaryFilePath);
    }

    ImGui.SameLine();
    if (ImGui.Button(
            Resources.TranslatorDebuggerClearDialogueGlossary))
    {
      StructuredDialogueGlossaryStore.Clear();
    }

    if (!string.IsNullOrWhiteSpace(snapshot.LastLoadFailureDetail))
    {
      ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.95f, 0.6f, 0.35f, 1f));
      ImGui.TextWrapped(
          string.Format(
              CultureInfo.CurrentCulture,
              Resources.TranslatorDebuggerDialogueGlossaryFailure,
              snapshot.LastLoadFailureDetail));
      ImGui.PopStyleColor();
    }
  }

  /// <summary>
  ///     Resolves a localized display name for the selected OpenAI-family
  ///     provider variant.
  /// </summary>
  /// <param name="variant">The provider variant.</param>
  /// <returns>The localized display name.</returns>
  private static string ResolveProviderVariantDisplayName(OpenAiProviderVariant variant)
  {
    return variant == OpenAiProviderVariant.CustomOpenAICompatible
        ? Resources.OpenAiProviderVariantCustomOpenAiCompatible
        : Resources.OpenAiProviderVariantOfficialOpenAi;
  }
}
