namespace DalaMock.Core.Configuration;

public class MockDalamudConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether should a Veldrid window be created.
    /// </summary>
    public bool CreateWindow { get; set; } = true;

    /// <summary>
    /// Gets or sets what language should be loaded.
    /// </summary>
    public ClientLanguage ClientLanguage { get; set; } = ClientLanguage.English;

    /// <summary>
    /// Gets or sets the global UI/font scale (1.0 == 100%), analogous to Dalamud's global UI scale.
    /// </summary>
    public float GlobalUiScale { get; set; } = 1f;

    /// <summary>
    /// Gets or sets the name of the currently chosen ImGui style, analogous to Dalamud's chosen style.
    /// </summary>
    public string ChosenStyle { get; set; } = "Dalamud Standard";

    /// <summary>
    /// Gets or sets the serialized user-created ImGui styles. Built-in styles are not stored here;
    /// each entry is a string produced by <see cref="Dalamud.Interface.Style.StyleModel.Serialize"/>.
    /// </summary>
    public List<string> SavedStyles { get; set; } = new();

    [JsonIgnore]
    public DirectoryInfo? GamePath { get; set; }

    public string? GamePathString
    {
        get => this.GamePath?.FullName;
        set => this.GamePath = value == null ? null : new DirectoryInfo(value);
    }

    /// <summary>
    /// Gets a value indicating whether gets a value indicating that the game path provided is valid.
    /// </summary>
    public bool GamePathValid
    {
        get
        {
            return this.GamePath != null && Directory.Exists(this.GamePath.FullName) && this.GamePath.EnumerateDirectories().Any(c => c.Name == "ffxiv");
        }
    }

    [JsonIgnore]
    public DirectoryInfo? PluginSavePath { get; set; }

    public string? PluginSavePathString
    {
        get => this.PluginSavePath?.FullName;
        set => this.PluginSavePath = value == null ? null : new DirectoryInfo(value);
    }

    /// <summary>
    /// Gets a value indicating whether gets a value indicating that the plugin path provided is valid.
    /// </summary>
    public bool PluginSavePathValid
    {
        get
        {
            return this.PluginSavePath != null && Directory.Exists(this.PluginSavePath.FullName);
        }
    }

    /// <summary>
    /// Gets or sets the language code to load Dalamud localization with.
    /// </summary>
    public string? LanguageOverride { get; set; } = null;

    /// <summary>
    /// Gets or sets the chosen default font, applied to the global ImGui atlas on startup. Null means
    /// the built-in default (AXIS game font).
    /// </summary>
    public MockDefaultFontConfig? DefaultFont { get; set; }

    /// <summary>
    /// Gets the ISO 639-1 two-letter code for the language of the effective Dalamud display language.
    /// </summary>
    public string EffectiveLanguage
    {
        get
        {
            var languages = Localization.ApplicableLangCodes.Prepend("en").ToArray();
            try
            {
                if (string.IsNullOrEmpty(this.LanguageOverride))
                {
                    var currentUiLang = CultureInfo.CurrentUICulture;

                    if (Localization.ApplicableLangCodes.Any(x => currentUiLang.TwoLetterISOLanguageName == x))
                    {
                        return currentUiLang.TwoLetterISOLanguageName;
                    }

                    return languages[0];
                }

                return this.LanguageOverride;
            }
            catch (Exception)
            {
                return languages[0];
            }
        }
    }
}
