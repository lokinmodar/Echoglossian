// <copyright file="RuntimeConfigurationRefresh.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

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

    var translationSignature = this.ComputeTranslationRuntimeSignature();
    var translationChanged = !string.Equals(
        translationSignature,
        this.translationRuntimeSignature,
        StringComparison.Ordinal);

    if (translationChanged)
    {
      this.RebuildTranslationServiceSafely();
      this.RebuildQueuedTranslationBroker();
      this.RebuildQuestToastRuntime();
      this.translationRuntimeSignature = translationSignature;
      this.addonHandlerRegistrationSignature = null;
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
          this.configuration.OpenAILlmModel,
          this.configuration.ChatGptTemperature,
          this.configuration.ChatGptPrompt,
          this.configuration.UseLiveOpenAIModelList,
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
  ///     Computes a signature for config values that determine which addon
  ///     handlers should be registered.
  /// </summary>
  /// <returns>A stable serialized signature.</returns>
  private string ComputeAddonHandlerRegistrationSignature()
  {
    return JsonConvert.SerializeObject(
        new
        {
          this.configuration.TranslateOperationGuideWindow,
          this.configuration.TranslateHudWindow,
          this.configuration.TranslateGameMainMenu,
          this.configuration.TranslateActionMenuWindow,
          this.configuration.TranslateCharacterWindow,
          this.configuration.TranslateTalk,
          this.configuration.TranslateBattleTalk,
          this.configuration.TranslateTalkSubtitle,
          this.configuration.TranslateMiniTalk,
          this.configuration.TranslateCutSceneSelectString,
          this.configuration.TranslateToDoList,
          this.configuration.TranslateScenarioTree,
          this.configuration.TranslateToast,
          this.configuration.TranslateWideTextToast,
          this.configuration.TranslateErrorToast,
          this.configuration.TranslateAreaToast,
          this.configuration.TranslateClassChangeToast,
          this.configuration.TranslateTextGimmickHint,
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
  private void RebuildQuestToastRuntime()
  {
    this.UnregisterQuestToastRuntime();
    this.questToastRuntime = this.CreateQuestToastRuntime();
    this.RegisterQuestToastRuntime();
  }

  /// <summary>
  ///     Re-registers addon handlers according to the current config.
  /// </summary>
  private void RefreshAddonHandlerRegistrations()
  {
    if (this.registeredAddonHandlers != null)
    {
      foreach (var (_, handler) in this.registeredAddonHandlers)
      {
        if (handler is IPluginUnloadAwareAddonHandler unloadAwareHandler)
        {
          unloadAwareHandler.OnPluginUnload();
        }
      }

      AddonHandlerRegistrar.UnregisterMany(
          this.registeredAddonHandlers,
          AddonLifecycle);
    }

    this.hoverTooltipManager.Clear();
    this.EgloAddonHandler();
  }
}
