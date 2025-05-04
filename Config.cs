// <copyright file="Config.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Configuration;

namespace Echoglossian
{
  public class Config : IPluginConfiguration
  {
    public string DefaultPluginCulture = "en";

    public int PluginCultureInt;

    public bool Translate = false;

    public bool UnsupportedLanguage = false;

    public bool OverlayOnlyLanguage = false;

    public bool PluginAssetsDownloaded = false;

    public int Lang = 28;

    public int FontSize = 24;

    public bool ShowInCutscenes = true;

    public bool TranslateBattleTalk = false;

    public bool TranslateTalk = false;

    public bool TranslateTalkSubtitle = false;

    public bool TranslateToast = false;

    public bool TranslateNpcNames = false;

    public bool TranslateErrorToast = false;

    public bool TranslateQuestToast = false;

    public bool TranslateAreaToast = false;

    public bool TranslateClassChangeToast = false;

    public bool TranslateWideTextToast = false;

    public bool TranslateYesNoScreen = false;

    public bool TranslateCutSceneSelectString = false;

    public bool TranslateSelectString = false;

    public bool TranslateSelectOk = false;

    public bool TranslateToDoList = false;

    public bool TranslateScenarioTree = false;

    public bool TranslateTooltips = false;

    public bool TranslateJournal = false;

    public bool UseImGuiForTalk = false;

    public bool UseImGuiForBattleTalk = false;

    public bool UseImGuiForToasts = false;

    public bool UseImGuiForTalkSubtitle = false;

    public bool SwapTextsUsingImGui = false;

    public int ChosenTransEngine = 0;

    public bool TranslateAlreadyTranslatedTexts = false;

    public Vector2 ImGuiWindowPosCorrection = new(0.0f, 0.0f);

    public Vector2 ImGuiToastWindowPosCorrection = new(0.0f, 0.0f);

    public Vector2 ImGuiBattleTalkWindowPosCorrection = new(0.0f, 0.0f);

    public Vector2 ImGuiTalkSubtitleWindowPosCorrection = new(0.0f, 0.0f);

    public float ImGuiTalkWindowWidthMult = 1.5f;

    public float ImGuiTalkWindowHeightMult = 1f;

    public float ImGuiBattleTalkWindowWidthMult = 1.5f;

    public float ImGuiBattleTalkWindowHeightMult = 1f;

    public float ImGuiTalkSubtitleWindowWidthMult = 1.5f;

    public float ImGuiTalkSubtitleWindowHeightMult = 1f;

    public float ImGuiToastWindowWidthMult = 1.5f;

    public Vector3 OverlayTalkTextColor = new(1.0f, 1.0f, 1.0f);

    public Vector3 OverlayBattleTalkTextColor = new(1.0f, 1.0f, 1.0f);

    public Vector3 OverlayTalkSubtitleTextColor = new(1.0f, 1.0f, 1.0f);

    public Vector3 OverlayToastTextColor = new(1.0f, 1.0f, 1.0f);

    public bool CopyTranslationToClipboard = false;

    public bool RemoveDiacriticsWhenUsingReplacementQuest = false;

    public bool RemoveDiacriticsWhenUsingReplacementTalkBTalk = false;

    public string DeeplTranslatorApiKey = string.Empty;

    public bool DeeplTranslatorUsingApiKey = false;

    public string ChatGptApiKey = string.Empty;

    public string ChatGPTBaseUrl = "https://api.openai.com/v1";

    public string OpenAILlmModel = "gpt-4o-mini";

    public float ChatGptTemperature = 0.1f;

    public string ChatGptEngine = "davinci";

    public string ChatGptModel = "gpt-3.5-turbo-16k-0613";

    public string? ChatGptPrompt = string.Empty;

    public string AiTranslatorPrompt = string.Empty;

    public float FontScale = 1;

    public float BattleTalkFontScale = 1;

    public int GoogleTranslateVersion = 2;

    public string PluginVersion = "2.0.0";

    public string YandexFreeApiKey = string.Empty;

    public bool UsePaidYandexApi = false;

    public bool UseYandexV2ForFreeApi = false;

    public string YandexFolderId = string.Empty;

    public string YandexPaidApiKey = string.Empty;

    public int YandexCharactersTranslated = 0;

    public string? YandexCloudPrompt = string.Empty;

    public string DeepSeekBaseUrl = "https://api.deepseek.com/v1";

    public string? DeepSeekTranslatorApiKey = string.Empty;

    public string? DeepSeekModel = "deepseek-chat";

    public float DeepSeekTemperature = 0.1f;

    public string? DeepSeekPrompt = string.Empty;

    public string? AwsRegion = "us-east-1";

    public string? AwsAccessKey = string.Empty;

    public string? AwsSecretKey = string.Empty;
    public string? AwsTranslateModel = "general";
    public string? AmazonPrompt = string.Empty;

    public string? GeminiTranslatorApiKey = string.Empty;

    public string? GeminiModel = "gemini-pro";

    public float GeminiTemperature = 0.1f;

    public string? GeminiPrompt = string.Empty;

    public string? MicrosoftTranslatorApiKey = string.Empty;
    public string? MicrosoftTranslatorRegion = string.Empty;
    public string? MicrosoftTranslatorEndpoint = "https://api.cognitive.microsofttranslator.com";
    public string? MicrosoftTranslatorModel = "general";
    public string? MicrosoftTranslatorPrompt = string.Empty;

    public string? OpenRouterModel = string.Empty;
    public float OpenRouterTemperature = 0.1f;
    public string? OpenRouterApiKey = string.Empty;
    public string? OpenRouterBaseUrl = "https://openrouter.ai/api/v1/";
    public string? OpenRouterPrompt = string.Empty;

    [NonSerialized]
    public long FontChangeTime = DateTime.Now.Ticks;

    public int Version { get; set; } = 5;
  }
}