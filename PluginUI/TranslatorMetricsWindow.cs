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

  /// <summary>
  ///     Draws the current dialogue-family LLM override routing state.
  /// </summary>
  private void DrawDialogueOverrideStatus()
  {
    var state = LlmSurfaceGroupRoutingPolicy.GetDialogueOverrideState(this.config);
    ImGui.TextWrapped(
        string.Format(
            CultureInfo.CurrentCulture,
            GetText(
                "TranslatorDebuggerDialogueOverridePrimaryAndEffective",
                "Primary engine: {0}  |  Effective dialogue engine: {1}"),
            state.PrimaryEngine,
            state.EffectiveDialogueEngine));

    if (!state.OverrideEnabled)
    {
      ImGui.TextWrapped(
          GetText(
              "TranslatorDebuggerDialogueOverrideDisabled",
              "Dialogue LLM override is currently disabled. Dialogue-family surfaces use the primary engine."));
      return;
    }

    var statusColor = state.OverrideActive
        ? new Vector4(0.45f, 0.9f, 0.55f, 1f)
        : new Vector4(0.95f, 0.6f, 0.35f, 1f);
    var statusMessage = state.OverrideActive
        ? state.SelectedOverrideEngine == state.PrimaryEngine
            ? GetText(
                "TranslatorDebuggerDialogueOverrideMatchesPrimary",
                "Dialogue LLM override is enabled, but it currently matches the primary engine selection.")
            : string.Format(
                CultureInfo.CurrentCulture,
                GetText(
                    "TranslatorDebuggerDialogueOverrideActive",
                    "Dialogue LLM override is active and routes dialogue-family surfaces through {0}."),
                state.SelectedOverrideEngine)
        : state.OverrideConfigured
            ? GetText(
                "TranslatorDebuggerDialogueOverrideInactiveFallback",
                "Dialogue LLM override is enabled, but it is not currently active. Dialogue-family surfaces keep using the primary engine.")
            : string.Format(
                CultureInfo.CurrentCulture,
                GetText(
                    "TranslatorDebuggerDialogueOverrideNotConfigured",
                    "Dialogue LLM override is enabled, but {0} is not fully configured yet. Dialogue-family surfaces keep using the primary engine."),
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
            GetText(
                "TranslatorDebuggerOpenAiProviderSummary",
                "OpenAI-family provider: {0}  |  Variant: {1}  |  Configured: {2}"),
            settings.ProviderName,
            providerVariantDisplayName,
            isConfigured
                ? GetText("TranslatorDebuggerYes", "Yes")
                : GetText("TranslatorDebuggerNo", "No")));
    ImGui.TextWrapped(
        string.Format(
            CultureInfo.CurrentCulture,
            GetText("TranslatorDebuggerOpenAiEndpoint", "Endpoint: {0}"),
            string.IsNullOrWhiteSpace(settings.BaseUrl) ? "-" : settings.BaseUrl));
    ImGui.TextWrapped(
        string.Format(
            CultureInfo.CurrentCulture,
            GetText("TranslatorDebuggerOpenAiModelSummary", "Model: {0}  |  Live models: {1}"),
            string.IsNullOrWhiteSpace(settings.Model) ? "-" : settings.Model,
            settings.UseLiveModelList
                ? GetText("TranslatorDebuggerEnabled", "Enabled")
                : GetText("TranslatorDebuggerDisabled", "Disabled")));

    var refreshStatus = refreshSnapshot.LastRefreshSucceeded switch
    {
      true => GetText("TranslatorDebuggerSucceeded", "Succeeded"),
      false => GetText("TranslatorDebuggerFailed", "Failed"),
      _ => GetText("TranslatorDebuggerNeverAttempted", "Never attempted"),
    };
    ImGui.TextWrapped(
        string.Format(
            CultureInfo.CurrentCulture,
            GetText(
                "TranslatorDebuggerOpenAiRefreshSummary",
                "Last model refresh: {0}  |  Provider: {1}  |  Current model count: {2}"),
            refreshStatus,
            refreshSnapshot.LastRefreshProviderName ?? "-",
            refreshSnapshot.CurrentModelCount));
    ImGui.TextWrapped(
        string.Format(
            CultureInfo.CurrentCulture,
            GetText("TranslatorDebuggerOpenAiRefreshUtc", "Last refresh UTC: {0}"),
            refreshSnapshot.LastRefreshObservedAtUtc?.ToString("u", CultureInfo.InvariantCulture) ?? "-"));

    if (!string.IsNullOrWhiteSpace(refreshSnapshot.LastRefreshUrl))
    {
      ImGui.TextWrapped(
          string.Format(
              CultureInfo.CurrentCulture,
              GetText("TranslatorDebuggerOpenAiRefreshEndpoint", "Refresh endpoint: {0}"),
              refreshSnapshot.LastRefreshUrl));
    }

    if (!string.IsNullOrWhiteSpace(refreshSnapshot.LastRefreshFailureDetail))
    {
      ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.95f, 0.6f, 0.35f, 1f));
      ImGui.TextWrapped(
          string.Format(
              CultureInfo.CurrentCulture,
              GetText("TranslatorDebuggerOpenAiRefreshFailure", "Last model refresh failure: {0}"),
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
            GetText(
                "TranslatorDebuggerDialogueGlossarySummary",
                "Dialogue glossary enabled: {0}  |  Entries: {1}  |  Skipped rows: {2}"),
            this.config.EnableDialogueGlossaryInjection
                ? GetText("TranslatorDebuggerEnabled", "Enabled")
                : GetText("TranslatorDebuggerDisabled", "Disabled"),
            snapshot.EntryCount,
            snapshot.SkippedEntryCount));
    ImGui.TextWrapped(
        string.Format(
            CultureInfo.CurrentCulture,
            GetText(
                "TranslatorDebuggerDialogueGlossaryPath",
                "Configured file path: {0}"),
            string.IsNullOrWhiteSpace(this.config.DialogueGlossaryFilePath)
                ? "-"
                : this.config.DialogueGlossaryFilePath));

    var loadStatus = snapshot.LastLoadSucceeded switch
    {
      true => GetText("TranslatorDebuggerSucceeded", "Succeeded"),
      false => GetText("TranslatorDebuggerFailed", "Failed"),
      _ => GetText("TranslatorDebuggerNeverAttempted", "Never attempted"),
    };
    ImGui.TextWrapped(
        string.Format(
            CultureInfo.CurrentCulture,
            GetText(
                "TranslatorDebuggerDialogueGlossaryLoadStatus",
                "Last glossary load: {0}  |  Last load UTC: {1}"),
            loadStatus,
            snapshot.LastLoadObservedAtUtc?.ToString("u", CultureInfo.InvariantCulture) ??
            "-"));

    if (ImGui.Button(
            GetText(
                "TranslatorDebuggerReloadDialogueGlossary",
                "Reload Dialogue Glossary")))
    {
      StructuredDialogueGlossaryStore.Refresh(
          this.config.DialogueGlossaryFilePath);
    }

    ImGui.SameLine();
    if (ImGui.Button(
            GetText(
                "TranslatorDebuggerClearDialogueGlossary",
                "Clear Loaded Glossary")))
    {
      StructuredDialogueGlossaryStore.Clear();
    }

    if (!string.IsNullOrWhiteSpace(snapshot.LastLoadFailureDetail))
    {
      ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.95f, 0.6f, 0.35f, 1f));
      ImGui.TextWrapped(
          string.Format(
              CultureInfo.CurrentCulture,
              GetText(
                  "TranslatorDebuggerDialogueGlossaryFailure",
                  "Last glossary load failure: {0}"),
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
        ? GetText(
            "OpenAiProviderVariantCustomOpenAiCompatible",
            "Custom OpenAI-Compatible")
        : GetText(
            "OpenAiProviderVariantOfficialOpenAi",
            "Official OpenAI");
  }

  /// <summary>
  ///     Resolves a localized text resource with a fallback.
  /// </summary>
  /// <param name="key">The resource key.</param>
  /// <param name="fallback">The fallback text.</param>
  /// <returns>The localized string or the fallback.</returns>
  private static string GetText(string key, string fallback)
  {
    return Resources.ResourceManager.GetString(key, Resources.Culture) ??
           fallback;
  }
}
