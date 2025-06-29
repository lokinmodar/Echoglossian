namespace Echoglossian.Translators.OpenAI;

public class OpenAIModelResponse
{
  [JsonProperty("object")]
  public string Object { get; set; } = string.Empty;

  [JsonProperty("data")]
  public List<OpenAIModelEntry> Data { get; set; } = new();
}

public class OpenAIModelEntry
{
  [JsonProperty("id")]
  public string Id { get; set; } = string.Empty;

  [JsonProperty("object")]
  public string Object { get; set; } = string.Empty;

  [JsonProperty("created")]
  public long Created { get; set; }

  [JsonProperty("owned_by")]
  public string OwnedBy { get; set; } = string.Empty;
}
