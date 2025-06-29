namespace Echoglossian.Translators.OpenAI;

public sealed record OpenAITextModel(
    string Id,
    string DisplayName,
    bool SupportsText,
    bool SupportsVision,
    bool IsTurbo,
    bool IsMini
);
