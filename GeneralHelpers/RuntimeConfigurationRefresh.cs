// <copyright file="RuntimeConfigurationRefresh.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Common;

using Newtonsoft.Json;

namespace Echoglossian;

/// <summary>
///     Handles runtime refresh of mutable plugin configuration without requiring
///     a full plugin reload.
/// </summary>
public partial class Echoglossian
{
  /// <summary>
  ///     Marks the live runtime configuration as dirty after the active config is
  ///     persisted so the next framework tick can refresh state safely.
  /// </summary>
  /// <param name="config">The configuration instance that was just saved.</param>
  private void OnConfigurationSaved(Config config)
  {
    if (!this.runtimeConfigurationReady ||
        !ReferenceEquals(config, this.configuration))
    {
      return;
    }

    this.runtimeConfigurationDirty = true;
  }

  /// <summary>
  ///     Applies pending runtime configuration changes on the framework thread.
  /// </summary>
  private void ApplyPendingRuntimeConfigurationChanges()
  {
    if (!this.runtimeConfigurationReady || !this.runtimeConfigurationDirty)
    {
      return;
    }

    this.runtimeConfigurationDirty = false;
    this.EnforceTranslationActivationConstraints();
    this.TryShowTranslationActivationBlockedNotification();
    PluginInterface.UiBuilder.DisableCutsceneUiHide =
        this.configuration.ShowInCutscenes;
    var glossarySignature =
        this.ComputeStructuredDialogueGlossaryRuntimeSignature();
    if (!string.Equals(
            glossarySignature,
            this.structuredDialogueGlossaryRuntimeSignature,
            StringComparison.Ordinal))
    {
      this.RefreshStructuredDialogueGlossaryRuntime();
      this.structuredDialogueGlossaryRuntimeSignature = glossarySignature;
    }

    var translationSignature = this.ComputeTranslationRuntimeSignature();
    var translationChanged = !string.Equals(
        translationSignature,
        this.translationRuntimeSignature,
        StringComparison.Ordinal);

    if (translationChanged)
    {
      this.RestoreVisibleAddonPresentationStateBeforeRuntimeReset();
      this.ResetRuntimeTranslationPresentationState();
      this.RebuildTranslationServiceSafely();
      this.RebuildQueuedTranslationBroker();
      this.RebuildToastGuiRuntimes();
      this.RebuildNamePlateTranslationRuntime();
      this.translationRuntimeSignature = translationSignature;
      this.addonHandlerRegistrationSignature = null;
    }

    var namePlatePresentationSignature =
        this.ComputeNamePlatePresentationSignature();
    if (!string.Equals(
            namePlatePresentationSignature,
            this.namePlatePresentationSignature,
            StringComparison.Ordinal))
    {
      NamePlateGuiInterface.RequestRedraw();
      this.namePlatePresentationSignature = namePlatePresentationSignature;
    }

    var addonHandlerSignature =
        this.ComputeAddonHandlerRegistrationSignature();
    if (!string.Equals(
            addonHandlerSignature,
            this.addonHandlerRegistrationSignature,
            StringComparison.Ordinal))
    {
      this.RefreshAddonHandlerRegistrations();
      this.addonHandlerRegistrationSignature = addonHandlerSignature;
    }
  }

  /// <summary>
  ///     Computes a signature for config values that affect translator
  ///     construction or broker pacing.
  /// </summary>
  /// <returns>A stable serialized signature.</returns>
  private string ComputeTranslationRuntimeSignature()
  {
    return JsonConvert.SerializeObject(
        new
        {
          this.configuration.ChosenTransEngine,
          this.configuration.Lang,
          this.configuration.Translate,
          this.configuration.TranslateAlreadyTranslatedTexts,
          this.configuration.AiTranslatorPrompt,
          this.configuration.GoogleTranslateVersion,
          this.configuration.DeeplTranslatorApiKey,
          this.configuration.DeeplTranslatorUsingApiKey,
          this.configuration.ChatGptApiKey,
          this.configuration.ChatGPTBaseUrl,
          this.configuration.ChatGptEngine,
          this.configuration.ChatGptModel,
          this.configuration.OpenAiProviderVariant,
          this.configuration.OpenAILlmModel,
          this.configuration.ChatGptTemperature,
          this.configuration.ChatGptPrompt,
          this.configuration.UseLiveOpenAIModelList,
          this.configuration.CustomOpenAiCompatibleApiKey,
          this.configuration.CustomOpenAiCompatibleBaseUrl,
          this.configuration.CustomOpenAiCompatibleModel,
          this.configuration.UseLiveCustomOpenAiCompatibleModelList,
          this.configuration.ClaudeApiKey,
          this.configuration.ClaudeBaseUrl,
          this.configuration.ClaudeModel,
          this.configuration.ClaudeTemperature,
          this.configuration.ClaudePrompt,
          this.configuration.DeepSeekTranslatorApiKey,
          this.configuration.DeepSeekBaseUrl,
          this.configuration.DeepSeekModel,
          this.configuration.DeepSeekTemperature,
          this.configuration.DeepSeekPrompt,
          this.configuration.GeminiTranslatorApiKey,
          this.configuration.GeminiModel,
          this.configuration.GeminiModelId,
          this.configuration.GeminiTemperature,
          this.configuration.GeminiPrompt,
          this.configuration.OpenRouterApiKey,
          this.configuration.OpenRouterBaseUrl,
          this.configuration.OpenRouterModel,
          this.configuration.OpenRouterTemperature,
          this.configuration.OpenRouterPrompt,
          this.configuration.UseLiveOpenRouterModelList,
          this.configuration.AwsAccessKey,
          this.configuration.AwsSecretKey,
          this.configuration.AwsRegion,
          this.configuration.AwsTranslateModel,
          this.configuration.AmazonPrompt,
          this.configuration.MicrosoftTranslatorApiKey,
          this.configuration.MicrosoftTranslatorEndpoint,
          this.configuration.MicrosoftTranslatorModel,
          this.configuration.MicrosoftTranslatorRegion,
          this.configuration.MicrosoftTranslatorPrompt,
          this.configuration.UsePaidYandexApi,
          this.configuration.UseYandexV2ForFreeApi,
          this.configuration.YandexFreeApiKey,
          this.configuration.YandexPaidApiKey,
          this.configuration.YandexFolderId,
          this.configuration.YandexCloudPrompt,
          this.configuration.LibreTranslateApiKey,
          this.configuration.LibreTranslateUrl,
          this.configuration.LibreTranslateInstanceType,
          this.configuration.OllamaUrl,
          this.configuration.OllamaModel,
          this.configuration.OllamaTemperature,
          this.configuration.OllamaPrompt,
          this.configuration.UseLiveOllamaModelList,
          this.configuration.LmStudioApiKey,
          this.configuration.LmStudioBaseUrl,
          this.configuration.LmStudioModel,
          this.configuration.LmStudioTemperature,
          this.configuration.LmStudioPrompt,
          this.configuration.UseLiveLmStudioModelList,
          this.configuration.UseLmStudioAuth,
        });
  }

  /// <summary>
  ///     Computes a signature for runtime-only structured dialogue glossary
  ///     settings so glossary reloads happen only when the operator-facing
  ///     glossary state changes.
  /// </summary>
  /// <returns>A stable serialized signature.</returns>
  private string ComputeStructuredDialogueGlossaryRuntimeSignature()
  {
    return JsonConvert.SerializeObject(
        new
        {
          this.configuration.EnableDialogueGlossaryInjection,
          DialogueGlossaryFilePath =
              this.configuration.DialogueGlossaryFilePath?.Trim() ??
              string.Empty,
        });
  }

  /// <summary>
  ///     Computes a signature for config values that change how NamePlate
  ///     translations are presented so the runtime can request a redraw only
  ///     when its native title-field semantics change.
  /// </summary>
  /// <returns>A stable serialized signature.</returns>
  private string ComputeNamePlatePresentationSignature()
  {
    return JsonConvert.SerializeObject(
        new
        {
          this.configuration.Translate,
          this.configuration.TranslateNamePlates,
          this.configuration.NamePlateTranslationDisplayMode,
          this.configuration.OverlayOnlyLanguage,
        });
  }

  /// <summary>
  ///     Refreshes the shared structured dialogue glossary store from the
  ///     current configuration.
  /// </summary>
  private void RefreshStructuredDialogueGlossaryRuntime()
  {
    if (!this.configuration.EnableDialogueGlossaryInjection)
    {
      StructuredDialogueGlossaryStore.Clear();
      return;
    }

    if (string.IsNullOrWhiteSpace(this.configuration.DialogueGlossaryFilePath))
    {
      StructuredDialogueGlossaryStore.Clear();
      return;
    }

    StructuredDialogueGlossaryStore.Refresh(
        this.configuration.DialogueGlossaryFilePath);
  }

  /// <summary>
  ///     Computes a signature for config values that determine which addon
  ///     handlers should be registered.
  /// </summary>
  /// <returns>A stable serialized signature.</returns>
  private string ComputeAddonHandlerRegistrationSignature()
  {
    return ComputeAddonHandlerRegistrationSignature(this.configuration);
  }

  /// <summary>
  ///     Computes a signature for config values that determine which addon
  ///     handlers should be registered.
  /// </summary>
  /// <param name="configuration">The configuration to evaluate.</param>
  /// <returns>A stable serialized signature.</returns>
  internal static string ComputeAddonHandlerRegistrationSignature(
      Config configuration)
  {
    return JsonConvert.SerializeObject(
        new
        {
          configuration.TranslateOperationGuideWindow,
          configuration.TranslateHudWindow,
          configuration.TranslateGameMainMenu,
          configuration.TranslateActionMenuWindow,
          configuration.TranslateContextMenu,
          configuration.TranslateCharacterWindow,
          configuration.TranslateTalk,
          configuration.TranslateBattleTalk,
          configuration.TranslateTalkSubtitle,
          configuration.TranslateMiniTalk,
          configuration.TranslateCutSceneSelectString,
          configuration.TranslateYesNoScreen,
          configuration.TranslateSelectOk,
          configuration.TranslateSelectString,
          configuration.TranslateSelectIconString,
          configuration.TranslateToDoList,
          configuration.TranslateToDo,
          configuration.TranslateScenarioTree,
          configuration.TranslateTooltipAddon,
          configuration.TranslateToast,
          configuration.TranslateWideTextToast,
          configuration.TranslateErrorToast,
          configuration.TranslateAreaToast,
          configuration.TranslateClassChangeToast,
          configuration.TranslateTextGimmickHint,
        });
  }

  /// <summary>
  ///     Recreates the shared queued-translation broker for the current engine.
  /// </summary>
  private void RebuildQueuedTranslationBroker()
  {
    this.queuedTranslationBroker.Dispose();
    this.queuedTranslationBroker = new QueuedTranslationBroker(
        (TransEngines)this.configuration.ChosenTransEngine,
        message => PluginRuntimeLog.Warning(message),
        message => PluginRuntimeLog.Error(message));
  }

  /// <summary>
  ///     Recreates and re-registers the quest-toast runtime so it uses the
  ///     current translation service.
  /// </summary>
  private void RebuildToastGuiRuntimes()
  {
    this.UnregisterQuestToastRuntime();
    this.UnregisterToastGuiSupportedToastRuntime();
    this.UnregisterToastGuiCaptureRuntime();
    this.questToastRuntime = this.CreateQuestToastRuntime();
    this.toastGuiSupportedToastRuntime = this.CreateToastGuiSupportedToastRuntime();
    this.toastGuiCaptureRuntime = this.CreateToastGuiCaptureRuntime();
    this.RegisterQuestToastRuntime();
    this.RegisterToastGuiSupportedToastRuntime();
    this.RegisterToastGuiCaptureRuntime();
  }

  /// <summary>
  ///     Restores visible addon-owned presentation state before shared
  ///     translation caches and runtimes are cleared for a live translation
  ///     configuration change.
  /// </summary>
  private void RestoreVisibleAddonPresentationStateBeforeRuntimeReset()
  {
    if (this.translationRefreshRestoreApplied ||
        this.registeredAddonHandlers == null)
    {
      return;
    }

    foreach (var (_, handler) in this.registeredAddonHandlers)
    {
      if (handler is IPluginUnloadAwareAddonHandler unloadAwareHandler)
      {
        unloadAwareHandler.OnPluginUnload();
      }
    }

    this.translationRefreshRestoreApplied = true;
  }

  /// <summary>
  ///     Re-registers addon handlers according to the current config.
  /// </summary>
  private void RefreshAddonHandlerRegistrations()
  {
    if (this.registeredAddonHandlers != null)
    {
      if (!this.translationRefreshRestoreApplied)
      {
        foreach (var (_, handler) in this.registeredAddonHandlers)
        {
          if (handler is IPluginUnloadAwareAddonHandler unloadAwareHandler)
          {
            unloadAwareHandler.OnPluginUnload();
          }
        }
      }

      AddonHandlerRegistrar.UnregisterMany(
          this.registeredAddonHandlers,
          AddonLifecycle);
    }

    this.hoverTooltipManager.Clear();
    this.EgloAddonHandler();
    this.translationRefreshRestoreApplied = false;
  }

  /// <summary>
  ///     Clears live translation presentation state and runtime-only caches so
  ///     language or engine changes do not leave stale translated UI state on
  ///     screen.
  /// </summary>
  private void ResetRuntimeTranslationPresentationState()
  {
    this.hoverTooltipManager.Clear();
    this.rtlTexturePresentationService.Clear();
    this.ClearOverlay(this.talkOverlay, clearText: true);
    this.ClearOverlay(this.battleTalkOverlay, clearText: true);
    this.ClearOverlay(this.talkSubtitleOverlay, clearText: true);
    this.ClearOverlay(this.toastOverlay, clearText: true);
    this.ClearOverlay(this.errorToastOverlay, clearText: true);
    this.ClearOverlay(this.areaToastOverlay, clearText: true);
    this.ClearOverlay(this.classChangeToastOverlay, clearText: true);
    this.ClearOverlay(this.questToastOverlay, clearText: true);
    this.ClearOverlay(this.cutSceneSelectStringOverlay, clearText: true);
    this.ClearOverlay(this.textGimmickHintOverlay, clearText: true);
    this.ClearOverlay(this.chatBubbleOverlay, clearText: true);
    this.ClearOverlay(this.actionDetailOverlay, clearText: true);
    this.ClearOverlay(this.itemDetailOverlay, clearText: true);
    this.DisposeMiniTalkBubbleOverlays();
    DialogueTranslationSessionStore.Clear();
    QuestUiTranslationCache.Clear();
    QuestHoverTranslationCache.Clear();
    DbFirstGameWindowAddonHandler.ClearSessionCaches();
    QuestProgressResolver.Clear();
    QuestTodoProgressResolver.Clear();
    this.ClearAcceptedQuestPrefetchState();
    this.ClearActionDetailPrefetchState();
    this.ClearItemDetailPrefetchState();
    this.ClearTraitDetailPrefetchState();
    this.ClearReferenceTextPrefetchState();
    this.ClearNamePlatePrefetchState();
  }
}
