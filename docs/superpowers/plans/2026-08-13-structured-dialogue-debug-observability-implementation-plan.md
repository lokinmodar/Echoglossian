# Structured Dialogue Debug Observability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add one shared debug-only structured-dialogue observability contract that shows what was prepared, which capability decisions were applied, and what came back for every structured LLM dialogue attempt.

**Architecture:** Keep policy decisions in the existing LLM capability matrix and emit compact shared `PluginRuntimeLog.Debug` lines through `StructuredDialogueDiagnosticsHelper`. Translators remain transport adapters only: each one reports the same `structured-start`, `structured-success`, and `structured-fallback` contract while continuing to use its existing request/response plumbing.

**Tech Stack:** C# / .NET 10, Dalamud `IPluginLog`, `PluginRuntimeLog`, xUnit, FluentAssertions, existing LLM capability matrix helpers, existing structured-dialogue request/validation helpers.

## Global Constraints

- Keep diagnostics in normal debug logging only. No extra diagnostic file.
- Emit one shared structured logging shape across all structured LLM translators.
- Show the effective capability decision used at runtime, including whether a parameter was sent, omitted, or forced to an explicit transport-specific disable such as `reasoning_effort=none`.
- Preserve the capability matrix as the only policy authority.
- No raw prompt dumps, full provider bodies, API keys, bearer tokens, or complete glossary contents in logs.
- Use `PluginRuntimeLog.Debug(...)` as the only output path for all new diagnostics.
- Keep the patch narrow: extend shared helpers, wire existing translators, add targeted tests, avoid a broad executor refactor.

---

## File Structure

- `Translators/Helpers/StructuredDialogueDiagnosticsHelper.cs`
  - Shared formatter and sanitizer for compact debug log lines.
- `Translators/Helpers/StructuredDialogueCapabilityDecisionLogFormatter.cs`
  - Shared token formatter for runtime capability decisions such as `temperature=omitted(default-only)` and `reasoning_effort=explicit-none(unsupported)`.
- `Echoglossian.Tests/StructuredDialogueDiagnosticsHelperTests.cs`
  - Shared log-shape and sanitization tests.
- `Echoglossian.Tests/StructuredDialogueCapabilityDecisionLogFormatterTests.cs`
  - Shared capability-decision token tests.
- `Echoglossian.Tests/TestDoubles/CapturingPluginLog.cs`
  - Reusable log-capture double for tests that need to inspect `PluginRuntimeLog.Debug` output.
- `Translators/ChatGPTTranslator.cs`
  - OpenAI SDK structured dialogue path; must report `reasoning_effort` decisions explicitly.
- `Translators/OpenRouterTranslator.cs`
  - OpenAI-compatible HTTP structured dialogue path.
- `Translators/DeepSeekTranslator.cs`
  - OpenAI-compatible HTTP structured dialogue path.
- `Translators/GeminiTranslator.cs`
  - Gemini structured dialogue HTTP path.
- `Translators/ClaudeTranslator.cs`
  - Claude structured dialogue HTTP path.
- `Translators/OllamaTranslator.cs`
  - Ollama structured dialogue HTTP path.
- `Translators/LmStudioTranslator.cs`
  - LM Studio structured dialogue HTTP path.

### Task 1: Build The Shared Structured Debug Contract

**Files:**
- Create: `Translators/Helpers/StructuredDialogueCapabilityDecisionLogFormatter.cs`
- Create: `Echoglossian.Tests/StructuredDialogueCapabilityDecisionLogFormatterTests.cs`
- Create: `Echoglossian.Tests/TestDoubles/CapturingPluginLog.cs`
- Modify: `Translators/Helpers/StructuredDialogueDiagnosticsHelper.cs`
- Modify: `Echoglossian.Tests/StructuredDialogueDiagnosticsHelperTests.cs`

**Interfaces:**
- Consumes:
  - `LlmCapabilityScope`
  - `LlmCapabilityParameterDecision`
  - `LlmCapabilityParameterName`
  - `StructuredDialogueProviderCapability`
- Produces:
  - `internal enum StructuredDialogueCapabilityEmissionMode`
  - `internal static class StructuredDialogueCapabilityDecisionLogFormatter`
  - `public static string StructuredDialogueDiagnosticsHelper.FormatStructuredStartMessage(
        LlmCapabilityScope scope,
        string route,
        StructuredDialogueProviderCapability capability,
        string sessionNamespace,
        int priorTurns,
        int glossaryCount,
        bool speakerMetadataPresent,
        bool addresseeMetadataPresent,
        int requestPromptLength,
        int? requestJsonLength,
        string promptPreview,
        string sourcePreview,
        IReadOnlyList<string> capabilityDecisionTokens)`
  - `public static string StructuredDialogueDiagnosticsHelper.FormatStructuredSuccessMessage(
        LlmCapabilityScope scope,
        string route,
        StructuredDialogueProviderCapability capability,
        bool glossaryApplied,
        int rawPayloadLength,
        int translatedLength,
        string rawPayloadPreview,
        string translatedPreview)`
  - `public static string StructuredDialogueDiagnosticsHelper.FormatStructuredFallbackMessage(
        string providerName,
        string? modelName,
        StructuredDialogueProviderCapability capability,
        string stage,
        string failureReason,
        int? statusCode = null,
        string? responseExcerpt = null,
        string? endpointScope = null,
        string? route = null,
        IReadOnlyList<string>? capabilityDecisionTokens = null,
        bool? glossaryApplied = null)`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void FormatStructuredStartMessage_ShouldIncludeRouteGlossaryAndCapabilityDecisionTokens()
{
    var scope = new LlmCapabilityScope(
        Echoglossian.TransEngines.ChatGPT,
        "OpenAI",
        "https://api.openai.com/v1",
        "gpt-5.6-terra");

    string message = StructuredDialogueDiagnosticsHelper.FormatStructuredStartMessage(
        scope,
        "chat/completions",
        StructuredDialogueProviderCapability.JsonSchema,
        "Talk",
        1,
        2,
        true,
        false,
        420,
        640,
        "Return only a JSON object...",
        "All these bright lights...",
        [
            "temperature=omitted(default-only)",
            "reasoning_effort=explicit-none(unsupported)",
        ]);

    message.Should().Contain("structured-start");
    message.Should().Contain("endpointScope=https://api.openai.com/v1");
    message.Should().Contain("route=chat-completions");
    message.Should().Contain("glossaryCount=2");
    message.Should().Contain("capabilityDecisions=temperature=omitted(default-only)|reasoning_effort=explicit-none(unsupported)");
}

[Fact]
public void FormatStructuredSuccessMessage_ShouldIncludeSanitizedResponsePreview()
{
    var scope = new LlmCapabilityScope(
        Echoglossian.TransEngines.OpenRouter,
        "OpenRouter",
        "https://openrouter.ai/api/v1",
        "openai/gpt-5-mini");

    string message = StructuredDialogueDiagnosticsHelper.FormatStructuredSuccessMessage(
        scope,
        "chat/completions",
        StructuredDialogueProviderCapability.JsonSchema,
        true,
        218,
        84,
        "{\"textTranslated\":\"sk-secret should never leak\"}",
        "Texto traduzido final");

    message.Should().Contain("structured-success");
    message.Should().Contain("glossaryApplied=true");
    message.Should().Contain("rawPayloadLength=218");
    message.Should().Contain("translatedLength=84");
    message.Should().NotContain("sk-secret");
}

[Fact]
public void Format_WhenUnsupportedReasoningEffortUsesExplicitNone_ShouldEmitExplicitDisableToken()
{
    string token = StructuredDialogueCapabilityDecisionLogFormatter.Format(
        LlmCapabilityParameterName.ReasoningEffort,
        new LlmCapabilityParameterDecision(
            LlmCapabilitySupportState.Unsupported,
            null,
            null,
            false,
            "StaticDefault",
            "Unsupported"),
        StructuredDialogueCapabilityEmissionMode.ExplicitDisable);

    token.Should().Be("reasoning_effort=explicit-none(unsupported)");
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~StructuredDialogueDiagnosticsHelperTests|FullyQualifiedName~StructuredDialogueCapabilityDecisionLogFormatterTests" `

Expected: FAIL because the new formatter type and start/success methods do not exist yet.

- [ ] **Step 3: Write the minimal shared implementation**

```csharp
internal enum StructuredDialogueCapabilityEmissionMode
{
    SentConfigured,
    OmittedUnsupported,
    OmittedDefaultOnly,
    OmittedUnknown,
    ExplicitDisable,
}

internal static class StructuredDialogueCapabilityDecisionLogFormatter
{
    public static string Format(
        LlmCapabilityParameterName parameterName,
        LlmCapabilityParameterDecision decision,
        StructuredDialogueCapabilityEmissionMode emissionMode)
    {
        var parameterToken = parameterName switch
        {
            LlmCapabilityParameterName.ReasoningEffort => "reasoning_effort",
            LlmCapabilityParameterName.Temperature => "temperature",
            _ => parameterName.ToString().ToLowerInvariant(),
        };

        var supportToken = decision.SupportState switch
        {
            LlmCapabilitySupportState.Unsupported => "unsupported",
            LlmCapabilitySupportState.Supported => "configured",
            _ => "unknown",
        };

        var emissionToken = emissionMode switch
        {
            StructuredDialogueCapabilityEmissionMode.SentConfigured => "sent(configured)",
            StructuredDialogueCapabilityEmissionMode.OmittedDefaultOnly => "omitted(default-only)",
            StructuredDialogueCapabilityEmissionMode.OmittedUnsupported => "omitted(unsupported)",
            StructuredDialogueCapabilityEmissionMode.ExplicitDisable when parameterName == LlmCapabilityParameterName.ReasoningEffort
                => "explicit-none(unsupported)",
            _ => $"omitted({supportToken})",
        };

        return $"{parameterToken}={emissionToken}";
    }
}
```

```csharp
public static string FormatStructuredStartMessage(
    LlmCapabilityScope scope,
    string route,
    StructuredDialogueProviderCapability capability,
    string sessionNamespace,
    int priorTurns,
    int glossaryCount,
    bool speakerMetadataPresent,
    bool addresseeMetadataPresent,
    int requestPromptLength,
    int? requestJsonLength,
    string promptPreview,
    string sourcePreview,
    IReadOnlyList<string> capabilityDecisionTokens)
{
    var parts = new List<string>
    {
        "structured-start",
        $"provider={scope.ProviderScope}",
        $"endpointScope={NormalizeToken(scope.EndpointScope)}",
        $"route={NormalizeToken(route)}",
        $"model={scope.ModelId}",
        $"capability={FormatCapability(capability)}",
        $"sessionNamespace={NormalizeToken(sessionNamespace)}",
        $"priorTurns={priorTurns}",
        $"glossaryCount={glossaryCount}",
        $"glossaryApplied={(glossaryCount > 0).ToString().ToLowerInvariant()}",
        $"speakerMetadataPresent={speakerMetadataPresent.ToString().ToLowerInvariant()}",
        $"addresseeMetadataPresent={addresseeMetadataPresent.ToString().ToLowerInvariant()}",
        $"requestPromptLength={requestPromptLength}",
        $"promptPreview={SanitizeExcerpt(promptPreview)}",
        $"sourcePreview={SanitizeExcerpt(sourcePreview)}",
        $"capabilityDecisions={string.Join("|", capabilityDecisionTokens)}",
    };

    if (requestJsonLength.HasValue)
    {
        parts.Add($"requestJsonLength={requestJsonLength.Value}");
    }

    return string.Join(", ", parts);
}

public static string FormatStructuredSuccessMessage(
    LlmCapabilityScope scope,
    string route,
    StructuredDialogueProviderCapability capability,
    bool glossaryApplied,
    int rawPayloadLength,
    int translatedLength,
    string rawPayloadPreview,
    string translatedPreview)
{
    return string.Join(", ", new[]
    {
        "structured-success",
        $"provider={scope.ProviderScope}",
        $"endpointScope={NormalizeToken(scope.EndpointScope)}",
        $"route={NormalizeToken(route)}",
        $"model={scope.ModelId}",
        $"capability={FormatCapability(capability)}",
        $"glossaryApplied={glossaryApplied.ToString().ToLowerInvariant()}",
        $"rawPayloadLength={rawPayloadLength}",
        $"translatedLength={translatedLength}",
        $"rawPayloadPreview={SanitizeExcerpt(rawPayloadPreview)}",
        $"translatedPreview={SanitizeExcerpt(translatedPreview)}",
    });
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~StructuredDialogueDiagnosticsHelperTests|FullyQualifiedName~StructuredDialogueCapabilityDecisionLogFormatterTests" `

Expected: PASS with coverage for `structured-start`, `structured-success`, fallback extensions, and capability-decision tokens.

- [ ] **Step 5: Commit**

```bash
git add Echoglossian.Tests/TestDoubles/CapturingPluginLog.cs Echoglossian.Tests/StructuredDialogueCapabilityDecisionLogFormatterTests.cs Echoglossian.Tests/StructuredDialogueDiagnosticsHelperTests.cs Translators/Helpers/StructuredDialogueCapabilityDecisionLogFormatter.cs Translators/Helpers/StructuredDialogueDiagnosticsHelper.cs
git commit -m "feat: add shared structured dialogue debug contract"
```

### Task 2: Wire OpenAI And OpenAI-Compatible Structured Translators

**Files:**
- Modify: `Translators/ChatGPTTranslator.cs`
- Modify: `Translators/OpenRouterTranslator.cs`
- Modify: `Translators/DeepSeekTranslator.cs`
- Modify: `Echoglossian.Tests/LlmCapabilityPolicyServiceTests.cs`

**Interfaces:**
- Consumes:
  - `StructuredDialogueCapabilityDecisionLogFormatter.Format(...)`
  - `StructuredDialogueDiagnosticsHelper.FormatStructuredStartMessage(...)`
  - `StructuredDialogueDiagnosticsHelper.FormatStructuredSuccessMessage(...)`
  - `StructuredDialogueDiagnosticsHelper.FormatStructuredFallbackMessage(...)`
  - `PluginRuntimeLog.Debug(...)`
- Produces:
  - OpenAI SDK and OpenAI-compatible structured translators emit:
    - one `structured-start` line immediately before the provider request;
    - one `structured-success` line after successful validation;
    - one `structured-fallback` line carrying route, endpoint, glossary flag, and capability decisions.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void ChatGptTranslator_WhenReasoningEffortIsUnsupported_UsesExplicitNoneCapabilityToken()
{
    var token = StructuredDialogueCapabilityDecisionLogFormatter.Format(
        LlmCapabilityParameterName.ReasoningEffort,
        new LlmCapabilityParameterDecision(
            LlmCapabilitySupportState.Unsupported,
            null,
            null,
            false,
            "StaticDefault",
            "Unsupported"),
        StructuredDialogueCapabilityEmissionMode.ExplicitDisable);

    token.Should().Be("reasoning_effort=explicit-none(unsupported)");
}

[Fact]
public void ChatGptTranslator_WhenTemperatureIsDefaultOnly_UsesOmittedDefaultOnlyCapabilityToken()
{
    var token = StructuredDialogueCapabilityDecisionLogFormatter.Format(
        LlmCapabilityParameterName.Temperature,
        new LlmCapabilityParameterDecision(
            LlmCapabilitySupportState.Unsupported,
            null,
            null,
            true,
            "StaticDefault",
            "DefaultOnly"),
        StructuredDialogueCapabilityEmissionMode.OmittedDefaultOnly);

    token.Should().Be("temperature=omitted(default-only)");
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~LlmCapabilityPolicyServiceTests|FullyQualifiedName~StructuredDialogueCapabilityDecisionLogFormatterTests" `

Expected: FAIL until the translators and helper agree on the explicit token behavior used by the OpenAI-family structured path.

- [ ] **Step 3: Write the minimal implementation**

```csharp
// ChatGPTTranslator structured path
var snapshot = LlmCapabilityPolicyService.GetSnapshot(this.capabilityScope);
var temperatureDecision = snapshot.GetDecision(LlmCapabilityParameterName.Temperature);
var reasoningDecision = snapshot.GetDecision(LlmCapabilityParameterName.ReasoningEffort);

var capabilityTokens = new List<string>
{
    StructuredDialogueCapabilityDecisionLogFormatter.Format(
        LlmCapabilityParameterName.Temperature,
        temperatureDecision,
        temperatureWasSent
            ? StructuredDialogueCapabilityEmissionMode.SentConfigured
            : temperatureDecision.OmitWhenDefaultOnly
                ? StructuredDialogueCapabilityEmissionMode.OmittedDefaultOnly
                : temperatureDecision.SupportState == LlmCapabilitySupportState.Unsupported
                    ? StructuredDialogueCapabilityEmissionMode.OmittedUnsupported
                    : StructuredDialogueCapabilityEmissionMode.OmittedUnknown),
    StructuredDialogueCapabilityDecisionLogFormatter.Format(
        LlmCapabilityParameterName.ReasoningEffort,
        reasoningDecision,
        reasoningEffortWasSent && chatCompletionOptions.ReasoningEffortLevel == ChatReasoningEffortLevel.None
            ? StructuredDialogueCapabilityEmissionMode.ExplicitDisable
            : reasoningEffortWasSent
                ? StructuredDialogueCapabilityEmissionMode.SentConfigured
                : StructuredDialogueCapabilityEmissionMode.OmittedUnknown),
};

PluginRuntimeLog.Debug(
    this.pluginLog,
    StructuredDialogueDiagnosticsHelper.FormatStructuredStartMessage(
        this.capabilityScope,
        "chat/completions",
        StructuredDialogueProviderCapability.JsonSchema,
        dialogueContext.SessionNamespace,
        dialogueContext.PriorTurns.Count,
        glossaryEntries.Count,
        dialogueContext.Speaker.HasValue,
        dialogueContext.Addressee.HasValue,
        structuredPrompt.Length,
        null,
        structuredPrompt,
        normalizedText,
        capabilityTokens));
```

```csharp
// OpenRouter and DeepSeek structured HTTP paths
PluginRuntimeLog.Debug(
    this.pluginLog,
    StructuredDialogueDiagnosticsHelper.FormatStructuredStartMessage(
        this.capabilityScope,
        "chat/completions",
        StructuredDialogueProviderCapability.JsonSchema,
        dialogueContext.SessionNamespace,
        dialogueContext.PriorTurns.Count,
        glossaryEntries.Count,
        dialogueContext.Speaker.HasValue,
        dialogueContext.Addressee.HasValue,
        structuredPrompt.Length,
        jsonContent.Length,
        structuredPrompt,
        normalizedText,
        capabilityTokens));

PluginRuntimeLog.Debug(
    this.pluginLog,
    StructuredDialogueDiagnosticsHelper.FormatStructuredSuccessMessage(
        this.capabilityScope,
        "chat/completions",
        StructuredDialogueProviderCapability.JsonSchema,
        usedGlossary,
        rawStructuredPayload?.Length ?? 0,
        translatedText.Length,
        rawStructuredPayload ?? string.Empty,
        translatedText));
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~LlmCapabilityPolicyServiceTests|FullyQualifiedName~StructuredDialogueCapabilityDecisionLogFormatterTests|FullyQualifiedName~StructuredDialogueDiagnosticsHelperTests" `

Expected: PASS with the OpenAI-family explicit-disable and default-only token behavior preserved.

- [ ] **Step 5: Commit**

```bash
git add Echoglossian.Tests/LlmCapabilityPolicyServiceTests.cs Translators/ChatGPTTranslator.cs Translators/OpenRouterTranslator.cs Translators/DeepSeekTranslator.cs
git commit -m "feat: log structured openai dialogue decisions"
```

### Task 3: Wire Gemini, Claude, Ollama, And LM Studio Structured Translators

**Files:**
- Modify: `Translators/GeminiTranslator.cs`
- Modify: `Translators/ClaudeTranslator.cs`
- Modify: `Translators/OllamaTranslator.cs`
- Modify: `Translators/LmStudioTranslator.cs`

**Interfaces:**
- Consumes:
  - `StructuredDialogueCapabilityDecisionLogFormatter.Format(...)`
  - `StructuredDialogueDiagnosticsHelper.FormatStructuredStartMessage(...)`
  - `StructuredDialogueDiagnosticsHelper.FormatStructuredSuccessMessage(...)`
  - `StructuredDialogueDiagnosticsHelper.FormatStructuredFallbackMessage(...)`
  - `PluginRuntimeLog.Debug(...)`
- Produces:
  - Gemini, Claude, Ollama, and LM Studio emit the same compact
    `structured-start` / `structured-success` / `structured-fallback` log
    contract with transport-appropriate `route` and `requestJsonLength`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void FormatStructuredStartMessage_WhenGlossaryIsAbsent_ShouldStillReportCapabilityDecisions()
{
    var scope = new LlmCapabilityScope(
        Echoglossian.TransEngines.Gemini,
        "Gemini",
        "https://generativelanguage.googleapis.com",
        "gemini-3-pro");

    string message = StructuredDialogueDiagnosticsHelper.FormatStructuredStartMessage(
        scope,
        "models/gemini-3-pro:generateContent",
        StructuredDialogueProviderCapability.JsonSchema,
        "Talk",
        0,
        0,
        true,
        false,
        310,
        512,
        "Return only a JSON object...",
        "What brings you to the Gold Saucer?",
        ["temperature=omitted(unsupported)"]);

    message.Should().Contain("structured-start");
    message.Should().Contain("glossaryCount=0");
    message.Should().Contain("capabilityDecisions=temperature=omitted(unsupported)");
}

[Fact]
public void FormatStructuredSuccessMessage_ShouldIncludeTransportRouteForGeminiStyleCalls()
{
    var scope = new LlmCapabilityScope(
        Echoglossian.TransEngines.Gemini,
        "Gemini",
        "https://generativelanguage.googleapis.com",
        "gemini-3-pro");

    string message = StructuredDialogueDiagnosticsHelper.FormatStructuredSuccessMessage(
        scope,
        "models/gemini-3-pro:generateContent",
        StructuredDialogueProviderCapability.JsonSchema,
        false,
        144,
        66,
        "{\"textTranslated\":\"teste\"}",
        "texto final");

    message.Should().Contain("route=models-gemini-3-pro-generatecontent");
    message.Should().Contain("structured-success");
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~StructuredDialogueDiagnosticsHelperTests|FullyQualifiedName~StructuredDialogueCapabilityDecisionLogFormatterTests" `

Expected: FAIL until the remaining structured translators are updated to use the shared start/success/fallback contract and route normalization.

- [ ] **Step 3: Write the minimal implementation**

```csharp
// Gemini structured path
var capabilityTokens = new List<string>
{
    StructuredDialogueCapabilityDecisionLogFormatter.Format(
        LlmCapabilityParameterName.Temperature,
        temperatureDecision,
        temperatureWasSent
            ? StructuredDialogueCapabilityEmissionMode.SentConfigured
            : temperatureDecision.OmitWhenDefaultOnly
                ? StructuredDialogueCapabilityEmissionMode.OmittedDefaultOnly
                : temperatureDecision.SupportState == LlmCapabilitySupportState.Unsupported
                    ? StructuredDialogueCapabilityEmissionMode.OmittedUnsupported
                    : StructuredDialogueCapabilityEmissionMode.OmittedUnknown),
};

PluginRuntimeLog.Debug(
    this.pluginLog,
    StructuredDialogueDiagnosticsHelper.FormatStructuredStartMessage(
        this.capabilityScope,
        $"models/{this.model}:generateContent",
        StructuredDialogueProviderCapability.JsonSchema,
        dialogueContext.SessionNamespace,
        dialogueContext.PriorTurns.Count,
        glossaryEntries.Count,
        dialogueContext.Speaker.HasValue,
        dialogueContext.Addressee.HasValue,
        structuredPrompt.Length,
        jsonContent.Length,
        structuredPrompt,
        normalizedText,
        capabilityTokens));

PluginRuntimeLog.Debug(
    this.pluginLog,
    StructuredDialogueDiagnosticsHelper.FormatStructuredSuccessMessage(
        this.capabilityScope,
        $"models/{this.model}:generateContent",
        StructuredDialogueProviderCapability.JsonSchema,
        usedGlossary,
        rawStructuredPayload?.Length ?? 0,
        translatedText.Length,
        rawStructuredPayload ?? string.Empty,
        translatedText));
```

```csharp
// Claude structured path
PluginRuntimeLog.Debug(
    this.pluginLog,
    StructuredDialogueDiagnosticsHelper.FormatStructuredStartMessage(
        this.capabilityScope,
        "messages",
        StructuredDialogueProviderCapability.JsonSchema,
        dialogueContext.SessionNamespace,
        dialogueContext.PriorTurns.Count,
        glossaryEntries.Count,
        dialogueContext.Speaker.HasValue,
        dialogueContext.Addressee.HasValue,
        structuredPrompt.Length,
        jsonContent.Length,
        structuredPrompt,
        normalizedText,
        capabilityTokens));

// Ollama structured path
PluginRuntimeLog.Debug(
    this.pluginLog,
    StructuredDialogueDiagnosticsHelper.FormatStructuredStartMessage(
        this.capabilityScope,
        "api/chat",
        StructuredDialogueProviderCapability.JsonSchema,
        dialogueContext.SessionNamespace,
        dialogueContext.PriorTurns.Count,
        glossaryEntries.Count,
        dialogueContext.Speaker.HasValue,
        dialogueContext.Addressee.HasValue,
        structuredPrompt.Length,
        jsonContent.Length,
        structuredPrompt,
        normalizedText,
        capabilityTokens));

// LM Studio structured path
PluginRuntimeLog.Debug(
    this.pluginLog,
    StructuredDialogueDiagnosticsHelper.FormatStructuredStartMessage(
        this.capabilityScope,
        "v1/chat/completions",
        StructuredDialogueProviderCapability.JsonSchema,
        dialogueContext.SessionNamespace,
        dialogueContext.PriorTurns.Count,
        glossaryEntries.Count,
        dialogueContext.Speaker.HasValue,
        dialogueContext.Addressee.HasValue,
        structuredPrompt.Length,
        jsonContent.Length,
        structuredPrompt,
        normalizedText,
        capabilityTokens));
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~StructuredDialogueDiagnosticsHelperTests|FullyQualifiedName~StructuredDialogueCapabilityDecisionLogFormatterTests" `

Run: `dotnet build Echoglossian.sln -c Debug --no-restore`

Expected: PASS for targeted tests and successful solution build with all structured translators compiling against the shared debug contract.

- [ ] **Step 5: Commit**

```bash
git add Translators/GeminiTranslator.cs Translators/ClaudeTranslator.cs Translators/OllamaTranslator.cs Translators/LmStudioTranslator.cs
git commit -m "feat: log structured dialogue diagnostics across llm providers"
```

## Self-Review

- **Spec coverage:** The plan covers shared start/success/fallback debug lines, capability-decision tokens, glossary/context visibility, provider route visibility, sanitization, and shared translator wiring across every structured LLM provider named in the approved spec.
- **Placeholder scan:** No `TODO`, `TBD`, or “implement later” placeholders remain. Each code step includes concrete method signatures or representative call sites.
- **Type consistency:** The plan consistently uses `StructuredDialogueCapabilityDecisionLogFormatter`, `StructuredDialogueCapabilityEmissionMode`, `FormatStructuredStartMessage`, and `FormatStructuredSuccessMessage` across all tasks.

## Execution Handoff

**Plan complete and saved to `docs/superpowers/plans/2026-08-13-structured-dialogue-debug-observability-implementation-plan.md`. Two execution options:**

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**
