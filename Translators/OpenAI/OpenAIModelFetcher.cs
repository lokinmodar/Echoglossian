// <copyright file="OpenAIModelFetcher.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators.OpenAI;

public static class OpenAIModelFetcher
{
    private static readonly string[] TextCompatiblePrefixes =
    [
        "gpt-",
        "chatgpt-",
        "o1-"
    ];

    public static async Task<List<LlmTextModel>>
        FetchAvailableTextModelsAsync(string apiKey)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);

        var response =
            await client.GetAsync("https://api.openai.com/v1/models");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var modelList =
            JsonConvert.DeserializeObject<OpenAIModelResponse>(json);

        return modelList?.Data
                   .Where(m =>
                       TextCompatiblePrefixes.Any(prefix =>
                           m.Id.StartsWith(prefix)))
                   .OrderBy(m => m.Id).Select(BuildTextModel).ToList() ??
               new List<LlmTextModel>();
    }

    private static LlmTextModel BuildTextModel(OpenAIModelEntry entry)
    {
        var id = entry.Id;

        var isMini = id.Contains("mini", StringComparison.OrdinalIgnoreCase);
        var isTurbo = id.Contains("turbo", StringComparison.OrdinalIgnoreCase);
        var supportsVision = id.Contains(
            "gpt-4o",
            StringComparison.OrdinalIgnoreCase);
        var supportsText =
            TextCompatiblePrefixes.Any(prefix => id.StartsWith(prefix));

        var display =
            CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
                id.Replace("-", " ").Replace(".", " "));

        return new LlmTextModel(
            id,
            display,
            supportsText,
            supportsVision,
            isTurbo,
            isMini);
    }
}