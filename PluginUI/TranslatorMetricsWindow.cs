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
  /// <param name="config">The active plugin configuration.</param>
  /// <param name="retranslateVisibleDialogueAsync">
  ///     Delegate used to explicitly retranslate the currently visible dialogue
  ///     line and persist the refreshed result.
  /// </param>
  public TranslatorMetricsWindow(
      Config config,
      Func<Task<NativeUI.AddonHandlers.Talk.VisibleDialogueRetranslationResult>>
          retranslateVisibleDialogueAsync)
  {
    this.config = config;
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
            GetText("TranslatorDebuggerWindowTitle"),
            ref isOpen,
            ImGuiWindowFlags.NoCollapse))
    {
      this.IsOpen = isOpen;
      ImGui.End();
      return;
    }
    this.IsOpen = isOpen;

    ImGui.TextWrapped(
        GetText("TranslatorDebuggerWindowDescription"));
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
            GetText(
                "TranslatorDebuggerRetranslateVisibleDialogueAndPersist")))
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
    if (ImGui.Button(GetText("TranslatorDebuggerClearMetrics")))
    {
      TranslatorMetricsCollector.Clear();
    }

    ImGui.SameLine();
    if (ImGui.Button(GetText("TranslatorDebuggerClearDialogueSessions")))
    {
      DialogueTranslationSessionStore.Clear();
    }

    if (isRetranslationRunning)
    {
      ImGui.Spacing();
      ImGui.TextWrapped(
          GetText("TranslatorDebuggerRetranslatingVisibleDialogue"));
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
          GetText("TranslatorDebuggerNoMetricsRecorded"));
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
            GetText("TranslatorDebuggerEngineSummary"),
            snapshots.Count,
            totalLiveRequests,
            totalContextAwareRequests,
            totalFailures,
            totalShortCircuits));
    ImGui.Text(
        string.Format(
            CultureInfo.CurrentCulture,
            GetText("TranslatorDebuggerStructuredSummary"),
            totalStructuredRequests,
            totalStructuredSuccesses,
            totalGlossaryStructuredRequests));
    ImGui.Text(
        string.Format(
            CultureInfo.CurrentCulture,
            GetText("TranslatorDebuggerDialogueSessionsCount"),
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
      ImGui.TableSetupColumn(GetText("TranslatorDebuggerMetricsTableEngine"));
      ImGui.TableSetupColumn(GetText("TranslatorDebuggerMetricsTableProvider"));
      ImGui.TableSetupColumn(GetText("TranslatorDebuggerMetricsTableModel"));
      ImGui.TableSetupColumn(GetText("TranslatorDebuggerMetricsTableLiveRequests"));
      ImGui.TableSetupColumn(GetText("TranslatorDebuggerMetricsTableContextAware"));
      ImGui.TableSetupColumn(GetText("TranslatorDebuggerMetricsTableStructured"));
      ImGui.TableSetupColumn(GetText("TranslatorDebuggerMetricsTableStructuredOk"));
      ImGui.TableSetupColumn(GetText("TranslatorDebuggerMetricsTableGlossary"));
      ImGui.TableSetupColumn(GetText("TranslatorDebuggerMetricsTableSuccesses"));
      ImGui.TableSetupColumn(GetText("TranslatorDebuggerMetricsTableFailures"));
      ImGui.TableSetupColumn(GetText("TranslatorDebuggerMetricsTableShortCircuits"));
      ImGui.TableSetupColumn(GetText("TranslatorDebuggerMetricsTableAverageMs"));
      ImGui.TableSetupColumn(GetText("TranslatorDebuggerMetricsTableMaxMs"));
      ImGui.TableSetupColumn(GetText("TranslatorDebuggerMetricsTableLastMs"));
      ImGui.TableSetupColumn(GetText("TranslatorDebuggerMetricsTableLastRequestUtc"));
      ImGui.TableSetupColumn(GetText("TranslatorDebuggerMetricsTableLastFailure"));
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
          GetText("TranslatorDebuggerDialogueSessionDescription"));
      if (ImGui.BeginTable(
              "##DialogueSessionTable",
              5,
              ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg |
              ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY |
              ImGuiTableFlags.SizingStretchProp,
              new Vector2(-1f, dialogueTableHeight)))
      {
        ImGui.TableSetupColumn(
            GetText("TranslatorDebuggerDialogueSessionTableNamespace"));
        ImGui.TableSetupColumn(
            GetText("TranslatorDebuggerDialogueSessionTableSessionKey"));
        ImGui.TableSetupColumn(
            GetText("TranslatorDebuggerDialogueSessionTableLastSpeaker"));
        ImGui.TableSetupColumn(
            GetText("TranslatorDebuggerDialogueSessionTableRetainedTurns"));
        ImGui.TableSetupColumn(
            GetText("TranslatorDebuggerDialogueSessionTableLastObservedUtc"));
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

  /// <summary>
  ///     Draws the current dialogue-family LLM override routing state.
  /// </summary>
  private void DrawDialogueOverrideStatus()
  {
    var state = LlmSurfaceGroupRoutingPolicy.GetDialogueOverrideState(this.config);
    ImGui.TextWrapped(
        string.Format(
            CultureInfo.CurrentCulture,
            GetText("TranslatorDebuggerDialogueOverridePrimaryAndEffective"),
            state.PrimaryEngine,
            state.EffectiveDialogueEngine));

    if (!state.OverrideEnabled)
    {
      ImGui.TextWrapped(
          GetText("TranslatorDebuggerDialogueOverrideDisabled"));
      return;
    }

    var statusColor = state.OverrideActive
        ? new Vector4(0.45f, 0.9f, 0.55f, 1f)
        : new Vector4(0.95f, 0.6f, 0.35f, 1f);
    var statusMessage = state.OverrideActive
        ? state.SelectedOverrideEngine == state.PrimaryEngine
            ? GetText("TranslatorDebuggerDialogueOverrideMatchesPrimary")
            : string.Format(
                CultureInfo.CurrentCulture,
                GetText("TranslatorDebuggerDialogueOverrideActive"),
                state.SelectedOverrideEngine)
        : state.OverrideConfigured
            ? GetText("TranslatorDebuggerDialogueOverrideInactiveFallback")
            : string.Format(
                CultureInfo.CurrentCulture,
                GetText("TranslatorDebuggerDialogueOverrideNotConfigured"),
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
    var refreshSnapshot = OpenAIModelManager.GetRefreshSnapshot();
    var providerVariantDisplayName = ResolveProviderVariantDisplayName(settings.Variant);

    ImGui.TextWrapped(
        string.Format(
            CultureInfo.CurrentCulture,
            GetText("TranslatorDebuggerOpenAiProviderSummary"),
            settings.ProviderName,
            providerVariantDisplayName,
            isConfigured
                ? GetText("TranslatorDebuggerYes")
                : GetText("TranslatorDebuggerNo")));
    ImGui.TextWrapped(
        string.Format(
            CultureInfo.CurrentCulture,
            GetText("TranslatorDebuggerOpenAiEndpoint"),
            string.IsNullOrWhiteSpace(settings.BaseUrl) ? "-" : settings.BaseUrl));
    ImGui.TextWrapped(
        string.Format(
            CultureInfo.CurrentCulture,
            GetText("TranslatorDebuggerOpenAiModelSummary"),
            string.IsNullOrWhiteSpace(settings.Model) ? "-" : settings.Model,
            settings.UseLiveModelList
                ? GetText("TranslatorDebuggerEnabled")
                : GetText("TranslatorDebuggerDisabled")));

    var refreshStatus = refreshSnapshot.LastRefreshSucceeded switch
    {
      true => GetText("TranslatorDebuggerSucceeded"),
      false => GetText("TranslatorDebuggerFailed"),
      _ => GetText("TranslatorDebuggerNeverAttempted"),
    };
    ImGui.TextWrapped(
        string.Format(
            CultureInfo.CurrentCulture,
            GetText("TranslatorDebuggerOpenAiRefreshSummary"),
            refreshStatus,
            refreshSnapshot.LastRefreshProviderName ?? "-",
            refreshSnapshot.CurrentModelCount));
    ImGui.TextWrapped(
        string.Format(
            CultureInfo.CurrentCulture,
            GetText("TranslatorDebuggerOpenAiRefreshUtc"),
            refreshSnapshot.LastRefreshObservedAtUtc?.ToString("u", CultureInfo.InvariantCulture) ?? "-"));

    if (!string.IsNullOrWhiteSpace(refreshSnapshot.LastRefreshUrl))
    {
      ImGui.TextWrapped(
          string.Format(
              CultureInfo.CurrentCulture,
              GetText("TranslatorDebuggerOpenAiRefreshEndpoint"),
              refreshSnapshot.LastRefreshUrl));
    }

    if (!string.IsNullOrWhiteSpace(refreshSnapshot.LastRefreshFailureDetail))
    {
      ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.95f, 0.6f, 0.35f, 1f));
      ImGui.TextWrapped(
          string.Format(
              CultureInfo.CurrentCulture,
              GetText("TranslatorDebuggerOpenAiRefreshFailure"),
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
            GetText("TranslatorDebuggerDialogueGlossarySummary"),
            this.config.EnableDialogueGlossaryInjection
                ? GetText("TranslatorDebuggerEnabled")
                : GetText("TranslatorDebuggerDisabled"),
            snapshot.EntryCount,
            snapshot.SkippedEntryCount));
    ImGui.TextWrapped(
        string.Format(
            CultureInfo.CurrentCulture,
            GetText("TranslatorDebuggerDialogueGlossaryPath"),
            string.IsNullOrWhiteSpace(this.config.DialogueGlossaryFilePath)
                ? "-"
                : this.config.DialogueGlossaryFilePath));

    var loadStatus = snapshot.LastLoadSucceeded switch
    {
      true => GetText("TranslatorDebuggerSucceeded"),
      false => GetText("TranslatorDebuggerFailed"),
      _ => GetText("TranslatorDebuggerNeverAttempted"),
    };
    ImGui.TextWrapped(
        string.Format(
            CultureInfo.CurrentCulture,
            GetText("TranslatorDebuggerDialogueGlossaryLoadStatus"),
            loadStatus,
            snapshot.LastLoadObservedAtUtc?.ToString("u", CultureInfo.InvariantCulture) ??
            "-"));

    if (ImGui.Button(
            GetText("TranslatorDebuggerReloadDialogueGlossary")))
    {
      StructuredDialogueGlossaryStore.Refresh(
          this.config.DialogueGlossaryFilePath);
    }

    ImGui.SameLine();
    if (ImGui.Button(
            GetText("TranslatorDebuggerClearDialogueGlossary")))
    {
      StructuredDialogueGlossaryStore.Clear();
    }

    if (!string.IsNullOrWhiteSpace(snapshot.LastLoadFailureDetail))
    {
      ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.95f, 0.6f, 0.35f, 1f));
      ImGui.TextWrapped(
          string.Format(
              CultureInfo.CurrentCulture,
              GetText("TranslatorDebuggerDialogueGlossaryFailure"),
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
        ? GetText("OpenAiProviderVariantCustomOpenAiCompatible")
        : GetText("OpenAiProviderVariantOfficialOpenAi");
  }

  /// <summary>
  ///     Resolves a localized text resource.
  /// </summary>
  /// <param name="key">The resource key.</param>
  /// <returns>The localized string or the key when missing.</returns>
  private static string GetText(string key)
  {
    return Resources.ResourceManager.GetString(key, Resources.Culture) ??
           key;
  }
}
