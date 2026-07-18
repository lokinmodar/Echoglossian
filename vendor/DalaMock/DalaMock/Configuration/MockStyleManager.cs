namespace DalaMock.Core.Configuration;

/// <summary>
/// Manages the set of ImGui styles available in DalaMock, mirroring Dalamud's style handling.
/// The three Dalamud built-in styles are always present and cannot be edited; user-created styles
/// are loaded from and persisted to <see cref="MockDalamudConfiguration"/>.
/// </summary>
public class MockStyleManager
{
    /// <summary>
    /// The number of built-in styles that occupy the first slots of <see cref="Styles"/>.
    /// </summary>
    public const int BuiltInStyleCount = 3;

    private readonly MockDalamudConfiguration configuration;
    private readonly MockConfigurationManager configurationManager;
    private readonly ILogger<MockStyleManager> logger;

    public MockStyleManager(
        MockDalamudConfiguration configuration,
        MockConfigurationManager configurationManager,
        ILogger<MockStyleManager> logger)
    {
        this.configuration = configuration;
        this.configurationManager = configurationManager;
        this.logger = logger;
        this.Styles = new List<StyleModel>();
        this.ReloadStyles();
    }

    /// <summary>
    /// Gets the styles currently available. The first <see cref="BuiltInStyleCount"/> entries are the
    /// non-editable Dalamud built-in styles; any remaining entries are user-created.
    /// </summary>
    public List<StyleModel> Styles { get; }

    /// <summary>
    /// Gets or sets the name of the chosen style. Backed directly by the configuration.
    /// </summary>
    public string ChosenStyle
    {
        get => this.configuration.ChosenStyle;
        set => this.configuration.ChosenStyle = value;
    }

    /// <summary>
    /// Determines whether the style at the given index is a non-editable built-in style.
    /// </summary>
    /// <param name="index">The index into <see cref="Styles"/>.</param>
    /// <returns>True if the style is built-in.</returns>
    public bool IsBuiltIn(int index) => index < BuiltInStyleCount;

    /// <summary>
    /// Gets the chosen style, falling back to the first available style if the chosen one is missing.
    /// </summary>
    /// <returns>The chosen style model, or null if no styles exist.</returns>
    public StyleModel? GetChosenStyle()
    {
        return this.Styles.FirstOrDefault(x => x.Name == this.ChosenStyle) ?? this.Styles.FirstOrDefault();
    }

    /// <summary>
    /// Applies the chosen style to ImGui. Requires an active ImGui context.
    /// </summary>
    public void ApplyChosenStyle()
    {
        this.GetChosenStyle()?.Apply();
    }

    /// <summary>
    /// Persists the current user styles and chosen style to the configuration file.
    /// Built-in styles are regenerated at load time and are therefore not persisted.
    /// </summary>
    public void Save()
    {
        this.configuration.SavedStyles = this.Styles
            .Skip(BuiltInStyleCount)
            .Select(x => x.Serialize())
            .ToList();
        this.configurationManager.SaveConfiguration(this.configuration);
    }

    private void ReloadStyles()
    {
        this.Styles.Clear();
        this.Styles.Add(StyleModelV1.DalamudStandard);
        this.Styles.Add(StyleModelV1.DalamudClassic);
        this.Styles.Add(StyleModelV1.DalamudHazy);

        foreach (var serialized in this.configuration.SavedStyles)
        {
            try
            {
                var model = StyleModel.Deserialize(serialized);
                if (model != null)
                {
                    this.Styles.Add(model);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to deserialize a saved DalaMock style; skipping it.");
            }
        }
    }
}
