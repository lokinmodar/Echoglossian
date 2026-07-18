namespace DalaMock.Core.Windows;

/// <summary>
/// Class representing a Mock Window's state.
/// </summary>
public class MockWindowHostState
{
    /// <summary>
    /// Gets or sets a value indicating whether the window is pinned.
    /// </summary>
    [JsonProperty("p")]
    public bool IsPinned { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the window is clickthrough.
    /// </summary>
    [JsonProperty("ct")]
    public bool IsClickThrough { get; set; }

    /// <summary>
    /// Gets or sets the window's opacity override.
    /// </summary>
    [JsonProperty("a")]
    public float? Alpha { get; set; }

    /// <summary>
    /// Gets or sets a value overriding the global blur factor.
    /// </summary>
    [JsonProperty("b")]
    public float? BlurFactorOverride { get; set; }

    /// <summary>
    /// Gets a value indicating whether this preset is in the default state.
    /// </summary>
    [JsonIgnore]
    public bool IsDefault =>
        !this.IsPinned &&
        !this.IsClickThrough &&
        !this.Alpha.HasValue;
}
