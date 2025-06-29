using Echoglossian.Translators.OpenAI;

namespace Echoglossian.Translators.DeepSeek
{
  public static class DeepSeekTextModelDefaults
  {
    public static readonly List<OpenAITextModel> PredefinedModels = new()
    {
        new("deepseek-chat", "DeepSeek Chat", true, false, true, false),
        new("deepseek-reasoner", "DeepSeek Reasoner", true, false, false, false),
    };
  }
}