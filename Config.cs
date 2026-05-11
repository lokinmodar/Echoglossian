// <copyright file="Config.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.ComponentModel;

using Dalamud.Configuration;

using Echoglossian.Translators.LibreTranslate;

using Newtonsoft.Json;

namespace Echoglossian;

public enum JournalTranslationDisplayMode
{
  NativeUiTranslation = 0,
  TooltipTranslation = 1,
  NativeUiTranslationWithOriginalTooltips = 2,
}

public class Config : IPluginConfiguration
{
  /// <summary>Generic prompt used by all AI translators when applicable.</summary>
  [DefaultValue("")] public string AiTranslatorPrompt = string.Empty;

  /// <summary>Prompt passed to Amazon Translate (if used).</summary>
  [DefaultValue("")] public string? AmazonPrompt = string.Empty;

  /// <summary>AWS access key for Translate usage.</summary>
  [DefaultValue("")] public string? AwsAccessKey = string.Empty;

  /// <summary>AWS region for Translate API.</summary>
  [DefaultValue("us-east-1")] public string? AwsRegion = "us-east-1";

  /// <summary>AWS secret key for Translate usage.</summary>
  [DefaultValue("")] public string? AwsSecretKey = string.Empty;

  /// <summary>Model name for Amazon Translate.</summary>
  [DefaultValue("general")] public string? AwsTranslateModel = "general";

  /// <summary>Scaling factor for BattleTalk font.</summary>
  [DefaultValue(1f)] public float BattleTalkFontScale = 1f;

  /// <summary>API key for OpenAI's ChatGPT service.</summary>
  [DefaultValue("")] public string ChatGptApiKey = string.Empty;

  /// <summary>Base URL for ChatGPT API.</summary>
  [DefaultValue("https://api.openai.com/v1")]
  public string ChatGPTBaseUrl = "https://api.openai.com/v1";

  /// <summary>Engine identifier for OpenAI API.</summary>
  [DefaultValue("davinci")] public string ChatGptEngine = "davinci";

  /// <summary>ChatGPT model used for API calls.</summary>
  [DefaultValue("gpt-4.1-mini")] public string ChatGptModel = "gpt-4.1-mini";

  /// <summary>Prompt template used for ChatGPT translations.</summary>
  [DefaultValue("")] public string? ChatGptPrompt = string.Empty;

  /// <summary>Temperature setting for ChatGPT responses.</summary>
  [DefaultValue(0.1f)] public float ChatGptTemperature = 0.1f;

  /// <summary>Selected translation engine ID (index-based).</summary>
  [DefaultValue(0)] public int ChosenTransEngine = 0;

  /// <summary>
  ///     Selected translation engine key persisted alongside the numeric
  ///     engine id so engine selection remains stable across enum-layout
  ///     changes.
  /// </summary>
  [DefaultValue("Google")] public string ChosenTransEngineKey = "Google";

  /// <summary>API key for Anthropic Claude usage.</summary>
  [DefaultValue("")] public string ClaudeApiKey = string.Empty;

  /// <summary>Base URL for Anthropic Claude API usage.</summary>
  [DefaultValue("https://api.anthropic.com")]
  public string ClaudeBaseUrl = "https://api.anthropic.com";

  /// <summary>Model used with Anthropic Claude translator API.</summary>
  [DefaultValue("claude-sonnet-4-20250514")]
  public string ClaudeModel = "claude-sonnet-4-20250514";

  /// <summary>Prompt passed to Claude for contextual translation.</summary>
  [DefaultValue("")] public string ClaudePrompt = string.Empty;

  /// <summary>Temperature used for Claude responses.</summary>
  [DefaultValue(0.1f)] public float ClaudeTemperature = 0.1f;

  /// <summary>Copy translated text to clipboard automatically.</summary>
  [DefaultValue(false)] public bool CopyTranslationToClipboard = false;

  /// <summary>API key for DeepL translator.</summary>
  [DefaultValue("")] public string DeeplTranslatorApiKey = string.Empty;

  /// <summary>Enable use of DeepL API key authentication.</summary>
  [DefaultValue(false)] public bool DeeplTranslatorUsingApiKey = false;

  /// <summary>Base URL for DeepSeek API usage.</summary>
  [DefaultValue("https://api.deepseek.com/v1")]
  public string DeepSeekBaseUrl = "https://api.deepseek.com/v1";

  /// <summary>Model used with DeepSeek translator API.</summary>
  [DefaultValue("deepseek-chat")]
  public string? DeepSeekModel = "deepseek-chat";

  /// <summary>Prompt passed to DeepSeek for contextual translation.</summary>
  [DefaultValue("")] public string? DeepSeekPrompt = string.Empty;

  /// <summary>Temperature used for DeepSeek responses.</summary>
  [DefaultValue(0.1f)] public float DeepSeekTemperature = 0.1f;

  /// <summary>API key for DeepSeek translation engine.</summary>
  [DefaultValue("")] public string? DeepSeekTranslatorApiKey = string.Empty;

  /// <summary>The default culture to use for plugin translations (e.g., "en", "ja").</summary>
  [DefaultValue("en")] public string DefaultPluginCulture = "en";

  /// <summary>Timestamp of the last font change (for internal overlay reset timing).</summary>
  [NonSerialized] public long FontChangeTime = DateTime.Now.Ticks;

  /// <summary>Font size used in overlays.</summary>
  [DefaultValue(24)] public int FontSize = 24;

  /// <summary>
  ///     Legacy shared title-bar toggle kept for config migration from older
  ///     versions that still used a single overlay title-bar setting.
  /// </summary>
  [DefaultValue(false)] public bool ForceShowTitle = false;

  /// <summary>Swap Journal hover tooltip text independently from the global overlay swap toggle.</summary>
  [DefaultValue(false)] public bool SwapJournalTextsUsingImGui = false;

  /// <summary>
  ///     How Journal should present translated text. NativeUiTranslation
  ///     writes only to the native addon, TooltipTranslation keeps the addon
  ///     intact and uses hover tooltips, and
  ///     NativeUiTranslationWithOriginalTooltips writes translation natively
  ///     while showing the original in hover tooltips.
  /// </summary>
  [DefaultValue(JournalTranslationDisplayMode.NativeUiTranslation)]
  public JournalTranslationDisplayMode JournalTranslationDisplayMode =
      JournalTranslationDisplayMode.NativeUiTranslation;

  /// <summary>Display mode for JournalDetail quest entries.</summary>
  [DefaultValue(JournalTranslationDisplayMode.NativeUiTranslation)]
  public JournalTranslationDisplayMode JournalDetailTranslationDisplayMode =
      JournalTranslationDisplayMode.NativeUiTranslation;

  /// <summary>Display mode for JournalAccept quest entries.</summary>
  [DefaultValue(JournalTranslationDisplayMode.NativeUiTranslation)]
  public JournalTranslationDisplayMode JournalAcceptTranslationDisplayMode =
      JournalTranslationDisplayMode.NativeUiTranslation;

  /// <summary>Display mode for JournalResult quest entries.</summary>
  [DefaultValue(JournalTranslationDisplayMode.NativeUiTranslation)]
  public JournalTranslationDisplayMode JournalResultTranslationDisplayMode =
      JournalTranslationDisplayMode.NativeUiTranslation;

  /// <summary>Display mode for Talk.</summary>
  [DefaultValue(JournalTranslationDisplayMode.NativeUiTranslation)]
  public JournalTranslationDisplayMode TalkTranslationDisplayMode =
      JournalTranslationDisplayMode.NativeUiTranslation;

  /// <summary>Display mode for BattleTalk.</summary>
  [DefaultValue(JournalTranslationDisplayMode.NativeUiTranslation)]
  public JournalTranslationDisplayMode BattleTalkTranslationDisplayMode =
      JournalTranslationDisplayMode.NativeUiTranslation;

  /// <summary>Display mode for TalkSubtitle.</summary>
  [DefaultValue(JournalTranslationDisplayMode.NativeUiTranslation)]
  public JournalTranslationDisplayMode TalkSubtitleTranslationDisplayMode =
      JournalTranslationDisplayMode.NativeUiTranslation;

  /// <summary>Display mode for MiniTalk.</summary>
  [DefaultValue(JournalTranslationDisplayMode.NativeUiTranslation)]
  public JournalTranslationDisplayMode MiniTalkTranslationDisplayMode =
      JournalTranslationDisplayMode.NativeUiTranslation;

  /// <summary>Display mode for CutSceneSelectString.</summary>
  [DefaultValue(JournalTranslationDisplayMode.NativeUiTranslation)]
  public JournalTranslationDisplayMode CutSceneSelectStringTranslationDisplayMode =
      JournalTranslationDisplayMode.NativeUiTranslation;

  /// <summary>Display mode for ScreenInfo/WideText toasts.</summary>
  [DefaultValue(JournalTranslationDisplayMode.NativeUiTranslation)]
  public JournalTranslationDisplayMode WideTextToastTranslationDisplayMode =
      JournalTranslationDisplayMode.NativeUiTranslation;

  /// <summary>Display mode for Error toasts.</summary>
  [DefaultValue(JournalTranslationDisplayMode.NativeUiTranslation)]
  public JournalTranslationDisplayMode ErrorToastTranslationDisplayMode =
      JournalTranslationDisplayMode.NativeUiTranslation;

  /// <summary>Display mode for Area toasts.</summary>
  [DefaultValue(JournalTranslationDisplayMode.NativeUiTranslation)]
  public JournalTranslationDisplayMode AreaToastTranslationDisplayMode =
      JournalTranslationDisplayMode.NativeUiTranslation;

  /// <summary>Display mode for ClassChange toasts.</summary>
  [DefaultValue(JournalTranslationDisplayMode.NativeUiTranslation)]
  public JournalTranslationDisplayMode ClassChangeToastTranslationDisplayMode =
      JournalTranslationDisplayMode.NativeUiTranslation;

  /// <summary>Display mode for TextGimmickHint toasts.</summary>
  [DefaultValue(JournalTranslationDisplayMode.NativeUiTranslation)]
  public JournalTranslationDisplayMode TextGimmickHintTranslationDisplayMode =
      JournalTranslationDisplayMode.NativeUiTranslation;

  /// <summary>Display mode for Quest toasts.</summary>
  [DefaultValue(JournalTranslationDisplayMode.NativeUiTranslation)]
  public JournalTranslationDisplayMode QuestToastTranslationDisplayMode =
      JournalTranslationDisplayMode.NativeUiTranslation;

  /// <summary>Always show the BattleTalk overlay title bar.</summary>
  [DefaultValue(false)] public bool BattleTalkForceShowTitle = false;

  /// <summary>Gemini model ID used for translations.</summary>
  [DefaultValue("gemini-pro")] public string? GeminiModel = "gemini-pro";

  /// <summary>
  ///     Model identifier for Gemini translations.
  /// </summary>
  [DefaultValue("gemini-pro")]
  public string GeminiModelId = "gemini-pro"; // or "gemini-1.5-flash"

  /// <summary>Prompt passed to Gemini for translation context.</summary>
  [DefaultValue("")] public string? GeminiPrompt = string.Empty;

  /// <summary>Temperature value used in Gemini translations.</summary>
  [DefaultValue(0.1f)] public float GeminiTemperature = 0.1f;

  /// <summary>API key for Gemini translator.</summary>
  [DefaultValue("")] public string? GeminiTranslatorApiKey = string.Empty;

  /// <summary>Selected version of Google Translate API (1 or 2).</summary>
  [DefaultValue(2)] public int GoogleTranslateVersion = 2;

  [DefaultValue(1f)] public float ImGuiBattleTalkWindowHeightMult = 1f;

  [DefaultValue(typeof(Vector2), "0, 0")]
  public Vector2 ImGuiBattleTalkWindowPosCorrection = new(0, 0);

  [DefaultValue(1.0f)] public float ImGuiBattleTalkWindowWidthMult = 1.0f;

  [DefaultValue(1f)] public float ImGuiTalkSubtitleWindowHeightMult = 1f;

  [DefaultValue(typeof(Vector2), "0, 0")]
  public Vector2 ImGuiTalkSubtitleWindowPosCorrection = new(0, 0);

  [DefaultValue(1.0f)] public float ImGuiTalkSubtitleWindowWidthMult = 1.0f;

  [DefaultValue(1f)] public float ImGuiTextGimmickHintWindowHeightMult = 1f;

  [DefaultValue(typeof(Vector2), "0, 0")]
  public Vector2 ImGuiTextGimmickHintWindowPosCorrection = new(0, 0);

  [DefaultValue(1.0f)] public float ImGuiTextGimmickHintWindowWidthMult = 1.0f;

  [DefaultValue(1f)] public float ImGuiTalkWindowHeightMult = 1f;

  /// <summary>Width multiplier for Talk ImGui overlay window.</summary>
  [DefaultValue(1.0f)] public float ImGuiTalkWindowWidthMult = 1.0f;

  [DefaultValue(typeof(Vector2), "0, 0")]
  public Vector2 ImGuiToastWindowPosCorrection = new(0, 0);

  [DefaultValue(1.0f)] public float ImGuiToastWindowWidthMult = 1.0f;

  /// <summary>Position correction for Screen Info (_WideText) toast overlays.</summary>
  [DefaultValue(typeof(Vector2), "0, 0")]
  public Vector2 ImGuiWideTextToastWindowPosCorrection = new(0, 0);

  /// <summary>Width multiplier for Screen Info (_WideText) toast overlays.</summary>
  [DefaultValue(1.0f)] public float ImGuiWideTextToastWindowWidthMult = 1.0f;

  /// <summary>Position correction for Error toast overlays.</summary>
  [DefaultValue(typeof(Vector2), "0, 0")]
  public Vector2 ImGuiErrorToastWindowPosCorrection = new(0, 0);

  /// <summary>Width multiplier for Error toast overlays.</summary>
  [DefaultValue(1.0f)] public float ImGuiErrorToastWindowWidthMult = 1.0f;

  /// <summary>Position correction for Area toast overlays.</summary>
  [DefaultValue(typeof(Vector2), "0, 0")]
  public Vector2 ImGuiAreaToastWindowPosCorrection = new(0, 0);

  /// <summary>Width multiplier for Area toast overlays.</summary>
  [DefaultValue(1.0f)] public float ImGuiAreaToastWindowWidthMult = 1.0f;

  /// <summary>Position correction for Class/Job change toast overlays.</summary>
  [DefaultValue(typeof(Vector2), "0, 0")]
  public Vector2 ImGuiClassChangeToastWindowPosCorrection = new(0, 0);

  /// <summary>Width multiplier for Class/Job change toast overlays.</summary>
  [DefaultValue(1.0f)] public float ImGuiClassChangeToastWindowWidthMult = 1.0f;

  /// <summary>Position correction for Quest toast overlays.</summary>
  [DefaultValue(typeof(Vector2), "0, 0")]
  public Vector2 ImGuiQuestToastWindowPosCorrection = new(0, 0);

  /// <summary>Width multiplier for Quest toast overlays.</summary>
  [DefaultValue(1.0f)] public float ImGuiQuestToastWindowWidthMult = 1.0f;

  /// <summary>Position correction for ImGui overlay windows.</summary>
  [DefaultValue(typeof(Vector2), "0, 0")]
  public Vector2 ImGuiWindowPosCorrection = new(0, 0);

  /// <summary>Game language as internal integer (e.g., 28 for English).</summary>
  [DefaultValue(28)] public int Lang = 28;

  /// <summary>
  ///     API key for LibreTranslate service.
  /// </summary>
  [DefaultValue("")] public string LibreTranslateApiKey = string.Empty;

  /// <summary>
  ///     Type of LibreTranslate instance to use for translations.
  /// </summary>
  [DefaultValue(LibreTranslateInstanceType.De)]
  public LibreTranslateInstanceType LibreTranslateInstanceType =
      LibreTranslateInstanceType.De;

  /// <summary>LibreTranslate URL.</summary>
  [DefaultValue("https://libretranslate.de/")]
  public string LibreTranslateUrl = "https://libretranslate.de/";

  /// <summary>
  ///     API key for LM Studio API access.
  /// </summary>
  [DefaultValue("")] public string LmStudioApiKey = string.Empty;

  /// <summary>
  ///     Base URL for LM Studio API (local or remote instance).
  /// </summary>
  [DefaultValue("http://localhost:1234/v1")]
  public string LmStudioBaseUrl = "http://localhost:1234/v1";

  /// <summary>Selected LM Studio model for translations.</summary>
  [DefaultValue("llama3")] public string LmStudioModel = "llama3";

  /// <summary>Prompt template used for LM Studio translations.</summary>
  [DefaultValue("")] public string LmStudioPrompt = string.Empty;

  /// <summary>Temperature setting for LM Studio translations.</summary>
  [DefaultValue(0.1f)] public float LmStudioTemperature = 0.1f;

  /// <summary>API key for Microsoft Translator service.</summary>
  [DefaultValue("")] public string? MicrosoftTranslatorApiKey = string.Empty;

  /// <summary>Endpoint URL for Microsoft Translator API.</summary>
  [DefaultValue("https://api.cognitive.microsofttranslator.com")]
  public string? MicrosoftTranslatorEndpoint =
      "https://api.cognitive.microsofttranslator.com";

  /// <summary>Translation model used for Microsoft Translator API.</summary>
  [DefaultValue("general")]
  public string? MicrosoftTranslatorModel = "general";

  /// <summary>Prompt passed to Microsoft Translator (if supported).</summary>
  [DefaultValue("")] public string? MicrosoftTranslatorPrompt = string.Empty;

  /// <summary>Region code for Microsoft Translator API.</summary>
  [DefaultValue("")] public string? MicrosoftTranslatorRegion = string.Empty;

  /// <summary>Ollama model to use for translations.</summary>
  [DefaultValue("llama3")] public string OllamaModel = "llama3";

  /// <summary>Prompt used for Ollama translations.</summary>
  [DefaultValue("")] public string OllamaPrompt = string.Empty;

  /// <summary>Ollama temperature setting for translations.</summary>
  [DefaultValue(0.1f)] public float OllamaTemperature = 0.1f;

  /// <summary>Ollama URL.</summary>
  [DefaultValue("http://localhost:11434")]
  public string OllamaUrl = "http://localhost:11434";

  /// <summary>OpenAI LLM model for ChatGPT use.</summary>
  [DefaultValue("gpt-4o-mini")] public string OpenAILlmModel = "gpt-4o-mini";

  /// <summary>API key for OpenRouter.ai service.</summary>
  [DefaultValue("")] public string? OpenRouterApiKey = string.Empty;

  /// <summary>Base URL for OpenRouter API calls.</summary>
  [DefaultValue("https://openrouter.ai/api/v1/")]
  public string? OpenRouterBaseUrl = "https://openrouter.ai/api/v1/";

  /// <summary>Model identifier for OpenRouter translator.</summary>
  [DefaultValue("mistral")] public string? OpenRouterModel = "mistral";

  /// <summary>Prompt passed to OpenRouter translator.</summary>
  [DefaultValue("")] public string? OpenRouterPrompt = string.Empty;

  /// <summary>Temperature value for OpenRouter translation generation.</summary>
  [DefaultValue(0.1f)] public float OpenRouterTemperature = 0.1f;

  [DefaultValue(typeof(Vector3), "1, 1, 1")]
  public Vector3 OverlayBattleTalkTextColor = new(1f, 1f, 1f);

  /// <summary>
  ///    Controls whether the translations should only be displayed as overlays because of missing native game font support for the selected language.
  /// </summary>
  [DefaultValue(false)] public bool OverlayOnlyLanguage = false;

  [DefaultValue(typeof(Vector3), "1, 1, 1")]
  public Vector3 OverlayTalkSubtitleTextColor = new(1f, 1f, 1f);

  [DefaultValue(typeof(Vector3), "1, 1, 1")]
  public Vector3 OverlayTalkTextColor = new(1f, 1f, 1f);

  [DefaultValue(typeof(Vector3), "1, 1, 1")]
  public Vector3 OverlayTextGimmickHintTextColor = new(1f, 1f, 1f);

  /// <summary>Background opacity used by Text Gimmick Hint overlays.</summary>
  [DefaultValue(1f)] public float TextGimmickHintBackgroundOpacity = 1f;

  [DefaultValue(typeof(Vector3), "1, 1, 1")]
  public Vector3 OverlayToastTextColor = new(1f, 1f, 1f);

  /// <summary>Text color used by Screen Info (_WideText) toast overlays.</summary>
  [DefaultValue(typeof(Vector3), "1, 1, 1")]
  public Vector3 OverlayWideTextToastTextColor = new(1f, 1f, 1f);

  /// <summary>Text color used by Error toast overlays.</summary>
  [DefaultValue(typeof(Vector3), "1, 1, 1")]
  public Vector3 OverlayErrorToastTextColor = new(1f, 1f, 1f);

  /// <summary>Text color used by Area toast overlays.</summary>
  [DefaultValue(typeof(Vector3), "1, 1, 1")]
  public Vector3 OverlayAreaToastTextColor = new(1f, 1f, 1f);

  /// <summary>Text color used by Class/Job change toast overlays.</summary>
  [DefaultValue(typeof(Vector3), "1, 1, 1")]
  public Vector3 OverlayClassChangeToastTextColor = new(1f, 1f, 1f);

  /// <summary>Text color used by Quest toast overlays.</summary>
  [DefaultValue(typeof(Vector3), "1, 1, 1")]
  public Vector3 OverlayQuestToastTextColor = new(1f, 1f, 1f);

  /// <summary>Background opacity used by Screen Info (_WideText) toast overlays.</summary>
  [DefaultValue(1f)] public float WideTextToastBackgroundOpacity = 1f;

  /// <summary>Background opacity used by Error toast overlays.</summary>
  [DefaultValue(0f)] public float ErrorToastBackgroundOpacity = 0f;

  /// <summary>Background opacity used by Area toast overlays.</summary>
  [DefaultValue(1f)] public float AreaToastBackgroundOpacity = 1f;

  /// <summary>Background opacity used by Class/Job change toast overlays.</summary>
  [DefaultValue(1f)] public float ClassChangeToastBackgroundOpacity = 1f;

  /// <summary>Background opacity used by Quest toast overlays.</summary>
  [DefaultValue(1f)] public float QuestToastBackgroundOpacity = 1f;

  /// <summary>Whether plugin assets have been successfully downloaded.</summary>
  [DefaultValue(false)] public bool PluginAssetsDownloaded = false;

  /// <summary>Selected plugin culture index, for internal mapping.</summary>
  public int PluginCultureInt;

  /// <summary>Current plugin version.</summary>
  [DefaultValue("2.0.0")] public string PluginVersion = "2.0.0";

  /// <summary>Remove diacritics when translating quest-related text.</summary>
  [DefaultValue(false)]
  public bool RemoveDiacriticsWhenUsingReplacementQuest = false;

  /// <summary>Remove diacritics when translating Talk/BattleTalk messages.</summary>
  [DefaultValue(false)]
  public bool RemoveDiacriticsWhenUsingReplacementTalkBTalk = false;

  /// <summary>Show overlays and translations during cutscenes.</summary>
  [DefaultValue(true)] public bool ShowInCutscenes = true;

  /// <summary>
  ///     Show informational Dalamud notifications for quest prefetch and
  ///     quest-window readiness waits.
  /// </summary>
  [DefaultValue(false)] public bool ShowQuestProgressNotifications = false;

  /// <summary>
  ///     Render translations via ImGui rather than replacing game text
  ///     directly.
  /// </summary>
  [DefaultValue(false)] public bool SwapTextsUsingImGui = false;

  /// <summary>Scaling factor for Talk font.</summary>
  [DefaultValue(1f)] public float TalkFontScale = 1f;

  /// <summary>Always show the Talk overlay title bar.</summary>
  [DefaultValue(false)] public bool TalkForceShowTitle = false;

  /// <summary>Font scale used for Talk Subtitle overlay.</summary>
  [DefaultValue(1f)] public float TalkSubtitleFontScale = 1f;

  /// <summary>Font scale used for MiniTalk overlay.</summary>
  [DefaultValue(1f)] public float MiniTalkFontScale = 1f;

  /// <summary>Background opacity used for MiniTalk overlays.</summary>
  [DefaultValue(1f)] public float MiniTalkBackgroundOpacity = 1f;

  [DefaultValue(typeof(Vector2), "0, 0")]
  public Vector2 ImGuiMiniTalkWindowPosCorrection = new(0, 0);

  [DefaultValue(1.0f)] public float ImGuiMiniTalkWindowWidthMult = 1.0f;

  [DefaultValue(typeof(Vector3), "1, 1, 1")]
  public Vector3 OverlayMiniTalkTextColor = new(1f, 1f, 1f);

  /// <summary>Use ImGui for CutSceneSelectString overlays.</summary>
  [DefaultValue(false)] public bool UseImGuiForCutSceneSelectString = false;

  /// <summary>Font scale used for CutSceneSelectString overlays.</summary>
  [DefaultValue(1f)] public float CutSceneSelectStringFontScale = 1f;

  [DefaultValue(typeof(Vector2), "0, 0")]
  public Vector2 ImGuiCutSceneSelectStringWindowPosCorrection = new(0, 0);

  [DefaultValue(1.0f)]
  public float ImGuiCutSceneSelectStringWindowWidthMult = 1.0f;

  [DefaultValue(typeof(Vector3), "1, 1, 1")]
  public Vector3 OverlayCutSceneSelectStringTextColor = new(1f, 1f, 1f);

  /// <summary>Background opacity used for CutSceneSelectString overlays.</summary>
  [DefaultValue(1f)] public float CutSceneSelectStringBackgroundOpacity = 1f;

  /// <summary>Always show the Talk Subtitle overlay title bar.</summary>
  [DefaultValue(false)] public bool TalkSubtitleForceShowTitle = false;

  /// <summary>Font scale used for Text Gimmick Hint overlay.</summary>
  [DefaultValue(1f)] public float TextGimmickHintFontScale = 1f;

  /// <summary>Font scale used for Toast overlay.</summary>
  [DefaultValue(1f)] public float ToastFontScale = 1f;

  /// <summary>Always show the Toast overlay title bar.</summary>
  [DefaultValue(false)] public bool ToastForceShowTitle = false;

  /// <summary>Font scale used for Screen Info (_WideText) toast overlays.</summary>
  [DefaultValue(1f)] public float WideTextToastFontScale = 1f;

  /// <summary>Always show the Screen Info (_WideText) toast overlay title bar.</summary>
  [DefaultValue(false)] public bool WideTextToastForceShowTitle = false;

  /// <summary>Font scale used for Error toast overlays.</summary>
  [DefaultValue(1f)] public float ErrorToastFontScale = 1f;

  /// <summary>Always show the Error toast overlay title bar.</summary>
  [DefaultValue(false)] public bool ErrorToastForceShowTitle = false;

  /// <summary>Font scale used for Area toast overlays.</summary>
  [DefaultValue(1f)] public float AreaToastFontScale = 1f;

  /// <summary>Always show the Area toast overlay title bar.</summary>
  [DefaultValue(false)] public bool AreaToastForceShowTitle = false;

  /// <summary>Font scale used for Class/Job change toast overlays.</summary>
  [DefaultValue(1f)] public float ClassChangeToastFontScale = 1f;

  /// <summary>Always show the Class/Job change toast overlay title bar.</summary>
  [DefaultValue(false)] public bool ClassChangeToastForceShowTitle = false;

  /// <summary>Font scale used for Quest toast overlays.</summary>
  [DefaultValue(1f)] public float QuestToastFontScale = 1f;

  /// <summary>Always show the Quest toast overlay title bar.</summary>
  [DefaultValue(false)] public bool QuestToastForceShowTitle = false;

  /// <summary>Enables translation processing globally.</summary>
  [DefaultValue(false)] public bool Translate = false;

  /// <summary>
  ///     Allow re-translating content even if it was previously translated and
  ///     cached.
  /// </summary>
  [DefaultValue(false)] public bool TranslateAlreadyTranslatedTexts = false;

  /// <summary>Translate area name toasts.</summary>
  [DefaultValue(false)] public bool TranslateAreaToast = false;

  /// <summary>Translate BattleTalk messages.</summary>
  [DefaultValue(false)] public bool TranslateBattleTalk = false;

  /// <summary>Translate or show sender names for BattleTalk messages.</summary>
  [DefaultValue(false)] public bool TranslateBattleTalkNpcNames = false;

  /// <summary> Translate character window text.</summary>
  [DefaultValue(false)] public bool TranslateCharacterWindow = false;

  /// <summary>Display mode for Character-family DB-first windows.</summary>
  [DefaultValue(JournalTranslationDisplayMode.NativeUiTranslation)]
  public JournalTranslationDisplayMode CharacterWindowTranslationDisplayMode =
      JournalTranslationDisplayMode.NativeUiTranslation;

  /// <summary>Translate the main command menu.</summary>
  [DefaultValue(false)] public bool TranslateMainCommandWindow = false;

  /// <summary>Display mode for the Main Command window.</summary>
  [DefaultValue(JournalTranslationDisplayMode.NativeUiTranslation)]
  public JournalTranslationDisplayMode MainCommandWindowTranslationDisplayMode =
      JournalTranslationDisplayMode.NativeUiTranslation;

  /// <summary>Translate the ActionMenu window.</summary>
  [JsonProperty("TranslateActionsWindow")]
  [DefaultValue(false)] public bool TranslateActionMenuWindow = false;

  /// <summary>Display mode for the ActionMenu window.</summary>
  [JsonProperty("ActionsWindowTranslationDisplayMode")]
  [DefaultValue(JournalTranslationDisplayMode.NativeUiTranslation)]
  public JournalTranslationDisplayMode ActionMenuWindowTranslationDisplayMode =
      JournalTranslationDisplayMode.NativeUiTranslation;

  /// <summary>
  /// Gets a value indicating whether the game main menu scope is enabled.
  /// </summary>
  [JsonIgnore]
  public bool TranslateGameMainMenu =>
      this.TranslateMainCommandWindow ||
      this.TranslateAddonContextMenuTitle;

  /// <summary>
  /// Gets the unified display mode for the game main menu scope.
  /// </summary>
  [JsonIgnore]
  public JournalTranslationDisplayMode GameMainMenuWindowTranslationDisplayMode =>
      this.ResolveGameMainMenuTranslationDisplayMode();

  /// <summary>
  /// Synchronizes the legacy MainCommand and AddonContextMenuTitle toggles so
  /// they behave as one combined game-main-menu scope.
  /// </summary>
  /// <returns>
  /// <see langword="true" /> when one or more values changed; otherwise
  /// <see langword="false" />.
  /// </returns>
  public bool NormalizeGameMainMenuTranslationSettings()
  {
    var changed = false;
    var unifiedEnabled = this.TranslateGameMainMenu;
    var unifiedDisplayMode = this.ResolveGameMainMenuTranslationDisplayMode();

    if (this.TranslateMainCommandWindow != unifiedEnabled)
    {
      this.TranslateMainCommandWindow = unifiedEnabled;
      changed = true;
    }

    if (this.TranslateAddonContextMenuTitle != unifiedEnabled)
    {
      this.TranslateAddonContextMenuTitle = unifiedEnabled;
      changed = true;
    }

    if (this.MainCommandWindowTranslationDisplayMode != unifiedDisplayMode)
    {
      this.MainCommandWindowTranslationDisplayMode = unifiedDisplayMode;
      changed = true;
    }

    if (this.AddonContextMenuTitleTranslationDisplayMode != unifiedDisplayMode)
    {
      this.AddonContextMenuTitleTranslationDisplayMode = unifiedDisplayMode;
      changed = true;
    }

    return changed;
  }

  /// <summary>
  /// Sets the unified game-main-menu scope toggle and display mode.
  /// </summary>
  /// <param name="enabled">Whether translation is enabled for the scope.</param>
  /// <param name="displayMode">The unified display mode.</param>
  /// <returns>
  /// <see langword="true" /> when one or more values changed; otherwise
  /// <see langword="false" />.
  /// </returns>
  public bool SetGameMainMenuTranslationSettings(
      bool enabled,
      JournalTranslationDisplayMode displayMode)
  {
    var changed = false;

    if (this.TranslateMainCommandWindow != enabled)
    {
      this.TranslateMainCommandWindow = enabled;
      changed = true;
    }

    if (this.TranslateAddonContextMenuTitle != enabled)
    {
      this.TranslateAddonContextMenuTitle = enabled;
      changed = true;
    }

    if (this.MainCommandWindowTranslationDisplayMode != displayMode)
    {
      this.MainCommandWindowTranslationDisplayMode = displayMode;
      changed = true;
    }

    if (this.AddonContextMenuTitleTranslationDisplayMode != displayMode)
    {
      this.AddonContextMenuTitleTranslationDisplayMode = displayMode;
      changed = true;
    }

    return changed;
  }

  /// <summary>
  /// Resolves the unified display mode for the combined game main menu scope.
  /// </summary>
  /// <returns>The resolved unified display mode.</returns>
  private JournalTranslationDisplayMode ResolveGameMainMenuTranslationDisplayMode()
  {
    if (this.AddonContextMenuTitleTranslationDisplayMode ==
        this.MainCommandWindowTranslationDisplayMode)
    {
      return this.AddonContextMenuTitleTranslationDisplayMode;
    }

    if (this.TranslateAddonContextMenuTitle &&
        !this.TranslateMainCommandWindow)
    {
      return this.AddonContextMenuTitleTranslationDisplayMode;
    }

    if (this.TranslateMainCommandWindow &&
        !this.TranslateAddonContextMenuTitle)
    {
      return this.MainCommandWindowTranslationDisplayMode;
    }

    var mainCommandUsesDefault =
        this.MainCommandWindowTranslationDisplayMode ==
        JournalTranslationDisplayMode.NativeUiTranslation;
    var addonContextUsesDefault =
        this.AddonContextMenuTitleTranslationDisplayMode ==
        JournalTranslationDisplayMode.NativeUiTranslation;

    if (!this.AddonContextMenuTitleTranslationDisplayMode.Equals(
            this.MainCommandWindowTranslationDisplayMode))
    {
      if (!this.AddonContextMenuTitleTranslationDisplayMode.Equals(
              JournalTranslationDisplayMode.NativeUiTranslation) &&
          mainCommandUsesDefault)
      {
        return this.AddonContextMenuTitleTranslationDisplayMode;
      }

      if (!this.MainCommandWindowTranslationDisplayMode.Equals(
              JournalTranslationDisplayMode.NativeUiTranslation) &&
          addonContextUsesDefault)
      {
        return this.MainCommandWindowTranslationDisplayMode;
      }
    }

    return this.AddonContextMenuTitleTranslationDisplayMode;
  }

  /// <summary>Translate HUD window text surfaces backed by StringArrayData.</summary>
  [DefaultValue(false)] public bool TranslateHudWindow = false;

  /// <summary>Display mode for HUD DB-first windows.</summary>
  [DefaultValue(JournalTranslationDisplayMode.NativeUiTranslation)]
  public JournalTranslationDisplayMode HudWindowTranslationDisplayMode =
      JournalTranslationDisplayMode.NativeUiTranslation;

  /// <summary>Translate the operation guide window.</summary>
  [DefaultValue(false)] public bool TranslateOperationGuideWindow = false;

  /// <summary>Display mode for the Operation Guide window.</summary>
  [DefaultValue(JournalTranslationDisplayMode.NativeUiTranslation)]
  public JournalTranslationDisplayMode OperationGuideTranslationDisplayMode =
      JournalTranslationDisplayMode.NativeUiTranslation;

  /// <summary>Translate the addon context menu title window.</summary>
  [DefaultValue(false)] public bool TranslateAddonContextMenuTitle = false;

  /// <summary>Display mode for the Addon Context Menu Title window.</summary>
  [DefaultValue(JournalTranslationDisplayMode.NativeUiTranslation)]
  public JournalTranslationDisplayMode AddonContextMenuTitleTranslationDisplayMode =
      JournalTranslationDisplayMode.NativeUiTranslation;

  /// <summary>Translate class/job change toasts.</summary>
  [DefaultValue(false)] public bool TranslateClassChangeToast = false;

  /// <summary>Translate cutscene-based SelectString dialog options.</summary>
  [DefaultValue(false)] public bool TranslateCutSceneSelectString = false;

  /// <summary>Translate error toasts.</summary>
  [DefaultValue(false)] public bool TranslateErrorToast = false;

  /// <summary>Translate entries in the quest journal.</summary>
  [DefaultValue(false)] public bool TranslateJournal = false;

  /// <summary>Translate the Journal detail pane.</summary>
  [DefaultValue(false)] public bool TranslateJournalDetail = false;

  /// <summary>Translate JournalAccept quest entries.</summary>
  [DefaultValue(false)] public bool TranslateJournalAccept = false;

  /// <summary>Translate JournalResult quest entries.</summary>
  [DefaultValue(false)] public bool TranslateJournalResult = false;

  /// <summary>
  ///     Legacy shared NPC-name translation toggle kept for migration from
  ///     older configs that did not yet separate Talk and BattleTalk.
  /// </summary>
  [DefaultValue(false)] public bool TranslateNpcNames = false;

  /// <summary>Translate quest-related toasts.</summary>
  [DefaultValue(false)] public bool TranslateQuestToast = false;

  /// <summary>Translate the RecommendList quest window.</summary>
  [DefaultValue(false)] public bool TranslateRecommendList = false;

  /// <summary>Translate the AreaMap quest window.</summary>
  [DefaultValue(false)] public bool TranslateAreaMap = false;

  /// <summary>Translate entries in the scenario tree (progress graph).</summary>
  [DefaultValue(false)] public bool TranslateScenarioTree = false;

  /// <summary>Translate confirmation dialog messages.</summary>
  [DefaultValue(false)] public bool TranslateSelectOk = false;

  /// <summary>Translate regular SelectString dialogs.</summary>
  [DefaultValue(false)] public bool TranslateSelectString = false;

  /// <summary>Translate Talk window messages.</summary>
  [DefaultValue(false)] public bool TranslateTalk = false;

  /// <summary>Translate or show sender names for Talk messages.</summary>
  [DefaultValue(false)] public bool TranslateTalkNpcNames = false;

  /// <summary>Translate Talk Subtitle messages.</summary>
  [DefaultValue(false)] public bool TranslateTalkSubtitle = false;

  /// <summary>Translate MiniTalk messages.</summary>
  [DefaultValue(false)] public bool TranslateMiniTalk = false;

  /// <summary>Translate Text Gimmick Hint messages.</summary>
  [DefaultValue(false)] public bool TranslateTextGimmickHint = false;

  /// <summary>Translate toast popup messages.</summary>
  [DefaultValue(false)] public bool TranslateToast = false;

  /// <summary>Translate To-Do List entries.</summary>
  [DefaultValue(false)] public bool TranslateToDoList = false;

  /// <summary>Display mode for ToDoList quest entries.</summary>
  [DefaultValue(JournalTranslationDisplayMode.NativeUiTranslation)]
  public JournalTranslationDisplayMode ToDoListTranslationDisplayMode =
      JournalTranslationDisplayMode.NativeUiTranslation;

  /// <summary>Display mode for ScenarioTree quest entries.</summary>
  [DefaultValue(JournalTranslationDisplayMode.NativeUiTranslation)]
  public JournalTranslationDisplayMode ScenarioTreeTranslationDisplayMode =
      JournalTranslationDisplayMode.NativeUiTranslation;

  /// <summary>Display mode for RecommendList quest entries.</summary>
  [DefaultValue(JournalTranslationDisplayMode.NativeUiTranslation)]
  public JournalTranslationDisplayMode RecommendListTranslationDisplayMode =
      JournalTranslationDisplayMode.NativeUiTranslation;

  /// <summary>Display mode for AreaMap quest entries.</summary>
  [DefaultValue(JournalTranslationDisplayMode.NativeUiTranslation)]
  public JournalTranslationDisplayMode AreaMapTranslationDisplayMode =
      JournalTranslationDisplayMode.NativeUiTranslation;

  /// <summary>Translate UI tooltips.</summary>
  [DefaultValue(false)] public bool TranslateTooltips = false;

  /// <summary>
  ///     Display mode for action and item tooltips managed by the DB-first
  ///     tooltip runtime.
  /// </summary>
  [DefaultValue(JournalTranslationDisplayMode.NativeUiTranslation)]
  public JournalTranslationDisplayMode TooltipTranslationDisplayMode =
      JournalTranslationDisplayMode.NativeUiTranslation;

  /// <summary>Text color used by Echoglossian hover tooltips.</summary>
  [DefaultValue(typeof(Vector3), "1, 1, 1")]
  public Vector3 HoverTooltipTextColor = new(1f, 1f, 1f);

  /// <summary>Background color used by Echoglossian hover tooltips.</summary>
  [DefaultValue(typeof(Vector3), "0.08, 0.08, 0.08")]
  public Vector3 HoverTooltipBackgroundColor = new(0.08f, 0.08f, 0.08f);

  /// <summary>Background opacity used by Echoglossian hover tooltips.</summary>
  [DefaultValue(0.95f)] public float HoverTooltipBackgroundOpacity = 0.95f;

  /// <summary>Translate wide-format toast messages.</summary>
  [DefaultValue(false)] public bool TranslateWideTextToast = false;

  /// <summary>Translate Yes/No selection dialogs.</summary>
  [DefaultValue(false)] public bool TranslateYesNoScreen = false;

  /// <summary>Indicates whether the selected language is unsupported.</summary>
  [DefaultValue(false)] public bool UnsupportedLanguage = false;

  /// <summary>Use ImGui for BattleTalk overlay.</summary>
  [DefaultValue(false)] public bool UseImGuiForBattleTalk = false;

  /// <summary>Use ImGui to render Talk overlay instead of modifying game UI.</summary>
  [DefaultValue(false)] public bool UseImGuiForTalk = false;

  /// <summary>Use ImGui for Talk Subtitle overlays.</summary>
  [DefaultValue(false)] public bool UseImGuiForTalkSubtitle = false;

  /// <summary>Use ImGui for MiniTalk overlays.</summary>
  [DefaultValue(false)] public bool UseImGuiForMiniTalk = false;

  /// <summary>Use ImGui for Text Gimmick Hint overlays.</summary>
  [DefaultValue(false)] public bool UseImGuiForTextGimmickHint = false;

  /// <summary>Use ImGui for toast messages.</summary>
  [DefaultValue(false)] public bool UseImGuiForToasts = false;

  /// <summary>Use ImGui overlay specifically for Screen Info (_WideText) toasts.</summary>
  [DefaultValue(false)] public bool UseImGuiForWideTextToast = false;

  /// <summary>Use ImGui overlay specifically for Error toasts.</summary>
  [DefaultValue(false)] public bool UseImGuiForErrorToast = false;

  /// <summary>Use ImGui overlay specifically for Area toasts.</summary>
  [DefaultValue(false)] public bool UseImGuiForAreaToast = false;

  /// <summary>Use ImGui overlay specifically for Class/Job change toasts.</summary>
  [DefaultValue(false)] public bool UseImGuiForClassChangeToast = false;

  /// <summary>Use ImGui overlay specifically for Quest toasts.</summary>
  [DefaultValue(false)] public bool UseImGuiForQuestToast = false;

  /// <summary>Temperature setting for DeepSeek responses.</summary>
  [DefaultValue(false)] public bool UseLiveDeepSeekModelList = false;

  /// <summary>
  ///     Use live Gemini model list instead of static defaults.
  /// </summary>
  [DefaultValue(false)] public bool UseLiveGeminiModelList = false;

  /// <summary>
  ///     Selected LM Studio model for translations.
  /// </summary>
  [DefaultValue(false)] public bool UseLiveLmStudioModelList = false;

  /// <summary>
  ///     Use live model list from Ollama instead of static defaults.
  /// </summary>
  [DefaultValue(false)] public bool UseLiveOllamaModelList = false;

  /// <summary>
  ///     Use live model list from OpenAI instead of static list.
  /// </summary>
  [DefaultValue(false)] public bool UseLiveOpenAIModelList = false;

  /// <summary>
  ///     Use live model list from OpenRouter instead of static defaults.
  /// </summary>
  [DefaultValue(false)] public bool UseLiveOpenRouterModelList = false;

  /// <summary>
  ///     Use live model list from Claude instead of static defaults.
  /// </summary>
  [DefaultValue(false)] public bool UseLiveClaudeModelList = false;

  /// <summary>
  ///     API key for LM Studio authentication (if required).
  /// </summary>
  [DefaultValue(false)] public bool UseLmStudioAuth = false;

  /// <summary>Use paid Yandex Cloud API instead of free version.</summary>
  [DefaultValue(false)] public bool UsePaidYandexApi = false;

  /// <summary>Use Yandex Cloud V2 API format for free API usage.</summary>
  [DefaultValue(false)] public bool UseYandexV2ForFreeApi = false;

  /// <summary>Total characters translated using Yandex (for stats/tracking).</summary>
  [DefaultValue(0)] public int YandexCharactersTranslated = 0;

  /// <summary>Prompt for Yandex Cloud translator (used in experimental flows).</summary>
  [DefaultValue("")] public string? YandexCloudPrompt = string.Empty;

  /// <summary>Folder ID for Yandex Cloud translation.</summary>
  [DefaultValue("")] public string YandexFolderId = string.Empty;

  /// <summary>Free Yandex API key for translation.</summary>
  [DefaultValue("")] public string YandexFreeApiKey = string.Empty;

  /// <summary>Paid API key for Yandex Cloud translation service.</summary>
  [DefaultValue("")] public string YandexPaidApiKey = string.Empty;

  /// <summary>Plugin configuration version number (used during migration).</summary>
  [DefaultValue(15)]
  public int Version { get; set; } = 15;

  /// <summary>
  /// Gets or sets a value indicating whether translation should run asynchronously.
  /// </summary>
  [DefaultValue(true)] public bool EnableAsyncTranslation { get; set; } = true;
}
