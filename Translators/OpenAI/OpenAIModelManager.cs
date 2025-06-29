namespace Echoglossian.Translators.OpenAI;



public static class OpenAIModelManager
{
  private static List<OpenAITextModel> _currentModels = OpenAITextModelDefaults.PredefinedModels;

  public static IReadOnlyList<OpenAITextModel> CurrentModelList => _currentModels;

  public static async Task RefreshAsync(string apiKey)
  {
    try
    {
      var models = await OpenAIModelFetcher.FetchAvailableTextModelsAsync(apiKey);
      _currentModels = models;
      PluginLog.Information("Successfully fetched live OpenAI model list.");
    }
    catch (Exception ex)
    {
      PluginLog.Warning(ex, "Failed to fetch live OpenAI models. Keeping previous list.");
    }
  }

  public static void ResetToDefault()
  {
    _currentModels = OpenAITextModelDefaults.PredefinedModels;
  }
}
