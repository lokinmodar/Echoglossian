
using System.Net.Http.Headers;


namespace Echoglossian.Translators.OpenAI;

public static class OpenAIModelFetcher
{
  private static readonly string[] TextCompatiblePrefixes =
  [
      "gpt-",
        "chatgpt-",
        "o1-"
  ];

  public static async Task<List<OpenAITextModel>> FetchAvailableTextModelsAsync(string apiKey)
  {
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

    var response = await client.GetAsync("https://api.openai.com/v1/models");
    response.EnsureSuccessStatusCode();

    var json = await response.Content.ReadAsStringAsync();
    var modelList = JsonConvert.DeserializeObject<OpenAIModelResponse>(json);

    return modelList?.Data
        .Where(m => TextCompatiblePrefixes.Any(prefix => m.Id.StartsWith(prefix)))
        .OrderBy(m => m.Id)
        .Select(BuildTextModel)
        .ToList()
        ?? new List<OpenAITextModel>();
  }

  private static OpenAITextModel BuildTextModel(OpenAIModelEntry entry)
  {
    string id = entry.Id;

    bool isMini = id.Contains("mini", StringComparison.OrdinalIgnoreCase);
    bool isTurbo = id.Contains("turbo", StringComparison.OrdinalIgnoreCase);
    bool supportsVision = id.Contains("gpt-4o", StringComparison.OrdinalIgnoreCase);
    bool supportsText = TextCompatiblePrefixes.Any(prefix => id.StartsWith(prefix));

    string display = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(id.Replace("-", " ").Replace(".", " "));

    return new OpenAITextModel(id, display, supportsText, supportsVision, isTurbo, isMini);
  }
}
