using System;
using System.ComponentModel;
using System.Numerics;

using Dalamud.Configuration;

namespace Echoglossian
{
  public class Config : IPluginConfiguration
  {
    /// <summary>The default culture to use for plugin translations (e.g., "en", "ja").</summary>
    [DefaultValue("en")]
    public string DefaultPluginCulture = "en";

    /// <summary>Selected plugin culture index, for internal mapping.</summary>
    public int PluginCultureInt;

    /// <summary>Enables translation processing globally.</summary>
    [DefaultValue(false)]
    public bool Translate = false;

    /// <summary>Indicates whether the selected language is unsupported.</summary>
    [DefaultValue(false)]
    public bool UnsupportedLanguage = false;

    /// <summary>Disables translation but still displays overlays (e.g., when language is visual only).</summary>
    [DefaultValue(false)]
    public bool OverlayOnlyLanguage = false;

    /// <summary>Whether plugin assets have been successfully downloaded.</summary>
    [DefaultValue(false)]
    public bool PluginAssetsDownloaded = false;

    /// <summary>Game language as internal integer (e.g., 28 for English).</summary>
    [DefaultValue(28)]
    public int Lang = 28;

    /// <summary>Font size used in overlays.</summary>
    [DefaultValue(24)]
    public int FontSize = 24;

    /// <summary>Show overlays and translations during cutscenes.</summary>
    [DefaultValue(true)]
    public bool ShowInCutscenes = true;

    /// <summary>Translate BattleTalk messages.</summary>
    [DefaultValue(false)]
    public bool TranslateBattleTalk = false;

    /// <summary>Translate Talk window messages.</summary>
    [DefaultValue(false)]
    public bool TranslateTalk = false;

    /// <summary>Translate Talk Subtitle messages.</summary>
    [DefaultValue(false)]
    public bool TranslateTalkSubtitle = false;

    /// <summary>Translate toast popup messages.</summary>
    [DefaultValue(false)]
    public bool TranslateToast = false;

    /// <summary>Translate NPC names in Talk window.</summary>
    [DefaultValue(false)]
    public bool TranslateNpcNames = false;

    /// <summary>Translate error toasts.</summary>
    [DefaultValue(false)]
    public bool TranslateErrorToast = false;

    /// <summary>Translate quest-related toasts.</summary>
    [DefaultValue(false)]
    public bool TranslateQuestToast = false;

    /// <summary>Translate area name toasts.</summary>
    [DefaultValue(false)]
    public bool TranslateAreaToast = false;

    /// <summary>Translate class/job change toasts.</summary>
    [DefaultValue(false)]
    public bool TranslateClassChangeToast = false;

    /// <summary>Translate wide-format toast messages.</summary>
    [DefaultValue(false)]
    public bool TranslateWideTextToast = false;

    /// <summary>Translate Yes/No selection dialogs.</summary>
    [DefaultValue(false)]
    public bool TranslateYesNoScreen = false;

    /// <summary>Translate cutscene-based SelectString dialog options.</summary>
    [DefaultValue(false)]
    public bool TranslateCutSceneSelectString = false;

    /// <summary>Translate regular SelectString dialogs.</summary>
    [DefaultValue(false)]
    public bool TranslateSelectString = false;

    /// <summary>Translate confirmation dialog messages.</summary>
    [DefaultValue(false)]
    public bool TranslateSelectOk = false;

    /// <summary>Translate To-Do List entries.</summary>
    [DefaultValue(false)]
    public bool TranslateToDoList = false;

    /// <summary>Translate entries in the scenario tree (progress graph).</summary>
    [DefaultValue(false)]
    public bool TranslateScenarioTree = false;

    /// <summary>Translate UI tooltips.</summary>
    [DefaultValue(false)]
    public bool TranslateTooltips = false;

    /// <summary>Translate entries in the quest journal.</summary>
    [DefaultValue(false)]
    public bool TranslateJournal = false;

    /// <summary>Use ImGui to render Talk overlay instead of modifying game UI.</summary>
    [DefaultValue(false)]
    public bool UseImGuiForTalk = false;

    /// <summary>Use ImGui for BattleTalk overlay.</summary>
    [DefaultValue(false)]
    public bool UseImGuiForBattleTalk = false;

    /// <summary>Use ImGui for toast messages.</summary>
    [DefaultValue(false)]
    public bool UseImGuiForToasts = false;

    /// <summary>Use ImGui for Talk Subtitle overlays.</summary>
    [DefaultValue(false)]
    public bool UseImGuiForTalkSubtitle = false;

    /// <summary>Render translations via ImGui rather than replacing game text directly.</summary>
    [DefaultValue(false)]
    public bool SwapTextsUsingImGui = false;

    /// <summary>Selected translation engine ID (index-based).</summary>
    [DefaultValue(0)]
    public int ChosenTransEngine = 0;

    /// <summary>Allow re-translating content even if it was previously translated and cached.</summary>
    [DefaultValue(false)]
    public bool TranslateAlreadyTranslatedTexts = false;

    /// <summary>Position correction for ImGui overlay windows.</summary>
    [DefaultValue(typeof(Vector2), "0, 0")]
    public Vector2 ImGuiWindowPosCorrection = new(0, 0);

    [DefaultValue(typeof(Vector2), "0, 0")]
    public Vector2 ImGuiToastWindowPosCorrection = new(0, 0);

    [DefaultValue(typeof(Vector2), "0, 0")]
    public Vector2 ImGuiBattleTalkWindowPosCorrection = new(0, 0);

    [DefaultValue(typeof(Vector2), "0, 0")]
    public Vector2 ImGuiTalkSubtitleWindowPosCorrection = new(0, 0);

    /// <summary>Width multiplier for Talk ImGui overlay window.</summary>
    [DefaultValue(1.5f)]
    public float ImGuiTalkWindowWidthMult = 1.5f;

    [DefaultValue(1f)]
    public float ImGuiTalkWindowHeightMult = 1f;

    [DefaultValue(1.5f)]
    public float ImGuiBattleTalkWindowWidthMult = 1.5f;

    [DefaultValue(1f)]
    public float ImGuiBattleTalkWindowHeightMult = 1f;

    [DefaultValue(1.5f)]
    public float ImGuiTalkSubtitleWindowWidthMult = 1.5f;

    [DefaultValue(1f)]
    public float ImGuiTalkSubtitleWindowHeightMult = 1f;

    [DefaultValue(1.5f)]
    public float ImGuiToastWindowWidthMult = 1.5f;

    [DefaultValue(typeof(Vector3), "1, 1, 1")]
    public Vector3 OverlayTalkTextColor = new(1f, 1f, 1f);

    [DefaultValue(typeof(Vector3), "1, 1, 1")]
    public Vector3 OverlayBattleTalkTextColor = new(1f, 1f, 1f);

    [DefaultValue(typeof(Vector3), "1, 1, 1")]
    public Vector3 OverlayTalkSubtitleTextColor = new(1f, 1f, 1f);

    [DefaultValue(typeof(Vector3), "1, 1, 1")]
    public Vector3 OverlayToastTextColor = new(1f, 1f, 1f);

    /// <summary>Copy translated text to clipboard automatically.</summary>
    [DefaultValue(false)]
    public bool CopyTranslationToClipboard = false;

    /// <summary>Remove diacritics when translating quest-related text.</summary>
    [DefaultValue(false)]
    public bool RemoveDiacriticsWhenUsingReplacementQuest = false;

    /// <summary>Remove diacritics when translating Talk/BattleTalk messages.</summary>
    [DefaultValue(false)]
    public bool RemoveDiacriticsWhenUsingReplacementTalkBTalk = false;

    /// <summary>API key for DeepL translator.</summary>
    [DefaultValue("")]
    public string DeeplTranslatorApiKey = string.Empty;

    /// <summary>Enable use of DeepL API key authentication.</summary>
    [DefaultValue(false)]
    public bool DeeplTranslatorUsingApiKey = false;

    /// <summary>API key for OpenAI's ChatGPT service.</summary>
    [DefaultValue("")]
    public string ChatGptApiKey = string.Empty;

    /// <summary>Base URL for ChatGPT API.</summary>
    [DefaultValue("https://api.openai.com/v1")]
    public string ChatGPTBaseUrl = "https://api.openai.com/v1";

    /// <summary>OpenAI LLM model for ChatGPT use.</summary>
    [DefaultValue("gpt-4o-mini")]
    public string OpenAILlmModel = "gpt-4o-mini";

    /// <summary>Temperature setting for ChatGPT responses.</summary>
    [DefaultValue(0.1f)]
    public float ChatGptTemperature = 0.1f;

    /// <summary>Engine identifier for OpenAI API.</summary>
    [DefaultValue("davinci")]
    public string ChatGptEngine = "davinci";

    /// <summary>ChatGPT model used for API calls.</summary>
    [DefaultValue("gpt-3.5-turbo-16k-0613")]
    public string ChatGptModel = "gpt-3.5-turbo-16k-0613";

    /// <summary>Prompt template used for ChatGPT translations.</summary>
    [DefaultValue("")]
    public string? ChatGptPrompt = string.Empty;

    /// <summary>Generic prompt used by all AI translators when applicable.</summary>
    [DefaultValue("")]
    public string AiTranslatorPrompt = string.Empty;

    /// <summary>Scaling factor for Talk font.</summary>
    [DefaultValue(1f)]
    public float TalkFontScale = 1f;

    /// <summary>Scaling factor for BattleTalk font.</summary>
    [DefaultValue(1f)]
    public float BattleTalkFontScale = 1f;

    /// <summary>Font scale used for Toast overlay.</summary>
    [DefaultValue(1f)]
    public float ToastFontScale = 1f;

    /// <summary>Font scale used for Talk Subtitle overlay.</summary>
    [DefaultValue(1f)]
    public float TalkSubtitleFontScale = 1f;

    /// <summary>Selected version of Google Translate API (1 or 2).</summary>
    [DefaultValue(2)]
    public int GoogleTranslateVersion = 2;

    /// <summary>Current plugin version.</summary>
    [DefaultValue("2.0.0")]
    public string PluginVersion = "2.0.0";

    /// <summary>Free Yandex API key for translation.</summary>
    [DefaultValue("")]
    public string YandexFreeApiKey = string.Empty;

    /// <summary>Use paid Yandex Cloud API instead of free version.</summary>
    [DefaultValue(false)]
    public bool UsePaidYandexApi = false;

    /// <summary>Use Yandex Cloud V2 API format for free API usage.</summary>
    [DefaultValue(false)]
    public bool UseYandexV2ForFreeApi = false;

    /// <summary>Folder ID for Yandex Cloud translation.</summary>
    [DefaultValue("")]
    public string YandexFolderId = string.Empty;

    /// <summary>Paid API key for Yandex Cloud translation service.</summary>
    [DefaultValue("")]
    public string YandexPaidApiKey = string.Empty;

    /// <summary>Total characters translated using Yandex (for stats/tracking).</summary>
    [DefaultValue(0)]
    public int YandexCharactersTranslated = 0;

    /// <summary>Prompt for Yandex Cloud translator (used in experimental flows).</summary>
    [DefaultValue("")]
    public string? YandexCloudPrompt = string.Empty;

    /// <summary>Base URL for DeepSeek API usage.</summary>
    [DefaultValue("https://api.deepseek.com/v1")]
    public string DeepSeekBaseUrl = "https://api.deepseek.com/v1";

    /// <summary>API key for DeepSeek translation engine.</summary>
    [DefaultValue("")]
    public string? DeepSeekTranslatorApiKey = string.Empty;

    /// <summary>Model used with DeepSeek translator API.</summary>
    [DefaultValue("deepseek-chat")]
    public string? DeepSeekModel = "deepseek-chat";

    /// <summary>Temperature used for DeepSeek responses.</summary>
    [DefaultValue(0.1f)]
    public float DeepSeekTemperature = 0.1f;

    /// <summary>Prompt passed to DeepSeek for contextual translation.</summary>
    [DefaultValue("")]
    public string? DeepSeekPrompt = string.Empty;

    /// <summary>AWS region for Translate API.</summary>
    [DefaultValue("us-east-1")]
    public string? AwsRegion = "us-east-1";

    /// <summary>AWS access key for Translate usage.</summary>
    [DefaultValue("")]
    public string? AwsAccessKey = string.Empty;

    /// <summary>AWS secret key for Translate usage.</summary>
    [DefaultValue("")]
    public string? AwsSecretKey = string.Empty;

    /// <summary>Model name for Amazon Translate.</summary>
    [DefaultValue("general")]
    public string? AwsTranslateModel = "general";

    /// <summary>Prompt passed to Amazon Translate (if used).</summary>
    [DefaultValue("")]
    public string? AmazonPrompt = string.Empty;

    /// <summary>API key for Gemini translator.</summary>
    [DefaultValue("")]
    public string? GeminiTranslatorApiKey = string.Empty;

    /// <summary>Gemini model ID used for translations.</summary>
    [DefaultValue("gemini-pro")]
    public string? GeminiModel = "gemini-pro";

    /// <summary>Temperature value used in Gemini translations.</summary>
    [DefaultValue(0.1f)]
    public float GeminiTemperature = 0.1f;

    /// <summary>Prompt passed to Gemini for translation context.</summary>
    [DefaultValue("")]
    public string? GeminiPrompt = string.Empty;

    /// <summary>API key for Microsoft Translator service.</summary>
    [DefaultValue("")]
    public string? MicrosoftTranslatorApiKey = string.Empty;

    /// <summary>Region code for Microsoft Translator API.</summary>
    [DefaultValue("")]
    public string? MicrosoftTranslatorRegion = string.Empty;

    /// <summary>Endpoint URL for Microsoft Translator API.</summary>
    [DefaultValue("https://api.cognitive.microsofttranslator.com")]
    public string? MicrosoftTranslatorEndpoint = "https://api.cognitive.microsofttranslator.com";

    /// <summary>Translation model used for Microsoft Translator API.</summary>
    [DefaultValue("general")]
    public string? MicrosoftTranslatorModel = "general";

    /// <summary>Prompt passed to Microsoft Translator (if supported).</summary>
    [DefaultValue("")]
    public string? MicrosoftTranslatorPrompt = string.Empty;

    /// <summary>Model identifier for OpenRouter translator.</summary>
    [DefaultValue("")]
    public string? OpenRouterModel = string.Empty;

    /// <summary>Temperature value for OpenRouter translation generation.</summary>
    [DefaultValue(0.1f)]
    public float OpenRouterTemperature = 0.1f;

    /// <summary>API key for OpenRouter.ai service.</summary>
    [DefaultValue("")]
    public string? OpenRouterApiKey = string.Empty;

    /// <summary>Base URL for OpenRouter API calls.</summary>
    [DefaultValue("https://openrouter.ai/api/v1/")]
    public string? OpenRouterBaseUrl = "https://openrouter.ai/api/v1/";

    /// <summary>Prompt passed to OpenRouter translator.</summary>
    [DefaultValue("")]
    public string? OpenRouterPrompt = string.Empty;

    /// <summary>LibreTranslate URL.</summary>
    [DefaultValue("https://libretranslate.de/")]
    public string LibreTranslateUrl = "https://libretranslate.de/";

    /// <summary>OpenLlama URL.</summary>
    [DefaultValue("https://api.openllama.org/v1/")]
    public string OpenLlamaUrl = "https://api.openllama.org/v1/";

    /// <summary>OpenLlama model to use for translations.</summary>
    [DefaultValue("open-llama-3-8b")]
    public string OpenLlamaModel = "open-llama-3-8b";

    /// <summary>OpenLlama temperature setting for translations.</summary>
    [DefaultValue(0.1f)]
    public float OpenLlamaTemperature = 0.1f;

    /// <summary>Prompt used for OpenLlama translations.</summary>
    [DefaultValue("")]
    public string OpenLlamaPrompt = string.Empty;

    /// <summary>Always show overlay title bar, even if name translation is off.</summary>
    [DefaultValue(true)]
    public bool ForceShowTitle = true;

    /// <summary>Timestamp of the last font change (for internal overlay reset timing).</summary>
    /// [NonSerialized]
    public long FontChangeTime = DateTime.Now.Ticks;

    /// <summary>Plugin configuration version number (used during migration).</summary>
    [DefaultValue(5)]
    public int Version { get; set; } = 5;
  }
}
