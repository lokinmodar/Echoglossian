# LLM Capability Matrix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add one conservative shared LLM capability matrix that disables incompatible UI controls, sanitizes provider payloads before send, learns exact-model incompatibilities from classified `400` responses, and keeps SQLite overlays synchronized with the existing live-model refresh flow.

**Architecture:** Introduce a small shared capability layer under `Translators/Capabilities` that resolves one effective policy snapshot from static defaults plus SQLite overlays. Keep runtime lookups DB-free by loading persisted rules into a cache manager, reuse existing model managers to promote discovered models after live refresh, and route UI plus translator payload decisions through the same policy service so they cannot drift apart.

**Tech Stack:** C#/.NET 10, EF Core SQLite migrations, existing model managers and engine UIs, `HttpClient`/OpenAI SDK request builders, xUnit + FluentAssertions, resx/ImGui, and PowerShell.

## Global Constraints

- Add one shared capability-policy layer for LLM engines only:
  - OpenAI / ChatGPT and custom OpenAI-compatible
  - Claude
  - Gemini
  - DeepSeek
  - OpenRouter
  - Ollama
  - LM Studio
- Drive both UI gating and runtime payload sanitization from the same effective policy resolution path.
- Keep the behavior conservative:
  - if support is unknown, the runtime omits the parameter;
  - the UI shows the control disabled with a tooltip rather than pretending it is supported.
- Reuse the existing live-model refresh flow as the single operator entrypoint for refreshing both model identity and capability overlays.
- Store live and learned capability overlays in SQLite, not in user config.
- Support exact-model rules and family-prefix inheritance.
- Allow bounded auto-learning from clearly classifiable provider `400` errors.
- No change to non-LLM translation engines.
- No second model-discovery pipeline or second refresh button.
- No replacement of current engine-specific translator classes.
- No automatic promotion from one observed model error to an entire family.
- No optimistic assumption that a successful request proves a parameter is universally supported.
- No provider-body dumps, raw prompts, or credential material in logs.
- No broad refactor of all model catalog code in this first slice.
- Every LLM translator request path must resolve the effective capability snapshot before building its outbound payload.
- Live refresh is best-effort and must not assume every provider returns complete capability metadata.
- Refresh work remains asynchronous and owned by the existing refresh coordinator, with no callback blocking.

---

## File map

### New files

- `Translators/Capabilities/LlmCapabilityParameterName.cs` — canonical supported parameter names for the first slice.
- `Translators/Capabilities/LlmCapabilitySupportState.cs` — `Unknown`, `Supported`, `Unsupported`.
- `Translators/Capabilities/LlmCapabilityRuleMatchType.cs` — `ExactModel` and `FamilyPrefix`.
- `Translators/Capabilities/LlmCapabilityScope.cs` — stable lookup identity containing engine, provider scope, endpoint scope, and model id.
- `Translators/Capabilities/LlmCapabilityRuleDefinition.cs` — static or cached rule shape consumed by the resolver.
- `Translators/Capabilities/LlmCapabilityParameterDecision.cs` — one resolved parameter decision including range, source, and reason.
- `Translators/Capabilities/LlmCapabilitySnapshot.cs` — immutable resolved policy snapshot with `GetDecision(LlmCapabilityParameterName parameterName)`.
- `Translators/Capabilities/LlmCapabilityStaticCatalog.cs` — committed conservative defaults by engine family and prefix.
- `Translators/Capabilities/LlmCapabilityResolver.cs` — merges static defaults and persisted overlays conservatively.
- `Translators/Capabilities/LlmCapabilityPolicyService.cs` — shared runtime/UI entrypoint for scope creation, snapshot lookup, temperature sanitization, and failure learning.
- `Translators/Capabilities/LlmCapabilityRefreshPromoter.cs` — promotes discovered model ids into exact-model overlay rows using family rules.
- `Translators/Capabilities/LlmCapabilityErrorClassifier.cs` — maps sanitized provider `400` responses into promotable exact-model rules or observation-only outcomes.
- `Cache/LlmCapabilityCacheManager.cs` — in-memory rule and observation cache hydrated from SQLite at startup.
- `DBHelpers/LlmCapabilityPersistenceHelper.cs` — additive SQLite upsert helpers for rules and observations.
- `EFCoreSqlite/Models/LlmModelCapabilityRule.cs` — persisted conservative rule entity.
- `EFCoreSqlite/Models/LlmModelCapabilityObservation.cs` — persisted audit trail for classified provider feedback.
- `PluginUI/EngineConfigUI/LlmCapabilityUiHelper.cs` — returns disabled/enabled state, tooltip text, and numeric ranges for temperature controls.
- `Echoglossian.Tests/LlmCapabilityResolverTests.cs`.
- `Echoglossian.Tests/LlmCapabilityPersistenceTests.cs`.
- `Echoglossian.Tests/LlmCapabilityPolicyServiceTests.cs`.
- `Echoglossian.Tests/LlmCapabilityErrorClassifierTests.cs`.
- `Echoglossian.Tests/LlmCapabilityUiHelperTests.cs`.

### Modified files

- `Echoglossian.cs` — initialize the new capability cache on plugin startup and clear it on shutdown.
- `GeneralHelpers/RuntimeConfigurationRefresh.cs` — clear capability cache/runtime state on translation-signature changes when required.
- `EFCoreSqlite/EchoglossianDbContext.cs` — add the new tables and indexes.
- `EFCoreSqlite/Migrations/EchoglossianDbContextModelSnapshot.cs` and one new dated migration pair — additive schema update only.
- `Translators/OpenAI/OpenAIModelManager.cs` — promote exact-model capability overlays after successful refresh.
- `Translators/Claude/ClaudeModelManager.cs`, `Translators/Gemini/GeminiModelManager.cs`, `Translators/DeepSeek/DeepSeekModelManager.cs`, `Translators/Ollama/OllamaModelManager.cs`, `Translators/LmStudio/LmStudioModelManager.cs` — same promotion hook for discovered models.
- `Translators/ChatGPTTranslator.cs`, `Translators/ClaudeTranslator.cs`, `Translators/GeminiTranslator.cs`, `Translators/DeepSeekTranslator.cs`, `Translators/OpenRouterTranslator.cs`, `Translators/OllamaTranslator.cs`, `Translators/LmStudioTranslator.cs` — omit unsupported parameters on plain and structured paths and record classified observations.
- `PluginUI/EngineConfigUI/ChatGptEngineUI.cs`, `ClaudeEngineUI.cs`, `GeminiEngineUI.cs`, `DeepSeekEngineUI.cs`, `OpenRouterEngineUI.cs`, `OllamaEngineUI.cs`, `LmStudioEngineUI.cs` — disable incompatible temperature sliders with tooltips and policy ranges.
- `Echoglossian.Tests/OpenAIModelManagerTests.cs`, `DeepSeekModelManagerTests.cs`, `LiveModelRefreshCoordinatorTests.cs`, `TranslationServiceTests.cs`, `RuntimeConfigurationRefreshContractTests.cs`, `ConfigDefaultsTests.cs`, and `OpenAiProviderConfigurationTests.cs` — extend existing seams rather than duplicating coverage.
- `Echoglossian.Mock.Tests/PluginStartupSmokeTests.cs` — prove startup still hydrates the plugin runtime with the new capability cache.
- `Properties/Resources.resx`, `Properties/Resources.en-US.resx`, `Properties/Resources.pt-BR.resx`, and `Properties/Resources.Designer.cs` — operator-facing tooltip and status copy.
- `Echoglossian.xml` — include if XML docs change while touching documented public or internal members.

## Interfaces produced and consumed

```csharp
public enum LlmCapabilityParameterName
{
    Temperature = 0,
    TopP = 1,
    TopK = 2,
    PresencePenalty = 3,
    FrequencyPenalty = 4,
    ReasoningEffort = 5,
    StructuredToolCalling = 6,
}

public enum LlmCapabilitySupportState
{
    Unknown = 0,
    Supported = 1,
    Unsupported = 2,
}

public enum LlmCapabilityRuleMatchType
{
    ExactModel = 0,
    FamilyPrefix = 1,
}

public readonly record struct LlmCapabilityScope(
    Echoglossian.TransEngines Engine,
    string ProviderScope,
    string EndpointScope,
    string ModelId);

public readonly record struct LlmCapabilityRuleDefinition(
    string Engine,
    string ProviderScope,
    string EndpointScope,
    LlmCapabilityRuleMatchType MatchType,
    string MatchValue,
    LlmCapabilityParameterName ParameterName,
    LlmCapabilitySupportState SupportState,
    float? MinValue,
    float? MaxValue,
    bool OmitWhenDefaultOnly,
    string Source,
    string Reason);

public readonly record struct LlmCapabilityParameterDecision(
    LlmCapabilitySupportState SupportState,
    float? MinValue,
    float? MaxValue,
    bool OmitWhenDefaultOnly,
    string Source,
    string Reason);

public sealed class LlmCapabilitySnapshot
{
    public LlmCapabilityParameterDecision GetDecision(
        LlmCapabilityParameterName parameterName);
}

public readonly record struct LlmCapabilitySliderState(
    bool IsEnabled,
    float MinValue,
    float MaxValue,
    string TooltipText);

public static class LlmCapabilityPolicyService
{
    public static LlmCapabilityScope CreateScope(
        Echoglossian.TransEngines engine,
        string providerScope,
        string? endpointScope,
        string? modelId);

    public static LlmCapabilitySnapshot GetSnapshot(
        LlmCapabilityScope scope);

    public static bool TryResolveTemperature(
        LlmCapabilityScope scope,
        float configuredValue,
        out float sanitizedValue,
        out LlmCapabilityParameterDecision decision);

    public static LlmCapabilityLearningResult LearnFromProviderFailure(
        LlmCapabilityScope scope,
        LlmCapabilityParameterName parameterName,
        int? statusCode,
        string? responseText);
}

public readonly record struct LlmCapabilityLearningResult(
    bool ObservationRecorded,
    bool RulePromoted,
    string FailureKind);

public static class LlmCapabilityRefreshPromoter
{
    public static void PromoteDiscoveredModels(
        Echoglossian.TransEngines engine,
        string providerScope,
        string endpointScope,
        IReadOnlyList<string> modelIds,
        DateTime observedAtUtc);
}

public static class LlmCapabilityUiHelper
{
    public static LlmCapabilitySliderState GetTemperatureSliderState(
        LlmCapabilityScope scope,
        float fallbackMin,
        float fallbackMax);
}
```

## Task 1: Define the shared capability contracts and conservative resolver

**Files:**
- Create: `Translators/Capabilities/LlmCapabilityParameterName.cs`
- Create: `Translators/Capabilities/LlmCapabilitySupportState.cs`
- Create: `Translators/Capabilities/LlmCapabilityRuleMatchType.cs`
- Create: `Translators/Capabilities/LlmCapabilityScope.cs`
- Create: `Translators/Capabilities/LlmCapabilityRuleDefinition.cs`
- Create: `Translators/Capabilities/LlmCapabilityParameterDecision.cs`
- Create: `Translators/Capabilities/LlmCapabilitySnapshot.cs`
- Create: `Translators/Capabilities/LlmCapabilityStaticCatalog.cs`
- Create: `Translators/Capabilities/LlmCapabilityResolver.cs`
- Test: `Echoglossian.Tests/LlmCapabilityResolverTests.cs`

**Interfaces:**
- Consumes: `Echoglossian.TransEngines`, `OpenAiProviderVariantHelper.ResolveActiveSettings(Config config)`, existing `LlmTextModel` ids from model managers.
- Produces:
  - `public readonly record struct LlmCapabilityScope(Echoglossian.TransEngines Engine, string ProviderScope, string EndpointScope, string ModelId);`
  - `public readonly record struct LlmCapabilityRuleDefinition(string Engine, string ProviderScope, string EndpointScope, LlmCapabilityRuleMatchType MatchType, string MatchValue, LlmCapabilityParameterName ParameterName, LlmCapabilitySupportState SupportState, float? MinValue, float? MaxValue, bool OmitWhenDefaultOnly, string Source, string Reason);`
  - `public readonly record struct LlmCapabilityParameterDecision(LlmCapabilitySupportState SupportState, float? MinValue, float? MaxValue, bool OmitWhenDefaultOnly, string Source, string Reason);`
  - `public sealed class LlmCapabilitySnapshot`
  - `public static LlmCapabilitySnapshot Resolve(LlmCapabilityScope scope, IReadOnlyList<LlmCapabilityRuleDefinition> overlayRules);`

- [ ] **Step 1: Write the failing resolver tests**

```csharp
[Fact]
public void Resolve_WithExactAndFamilyRules_PrefersExactRule()
{
    var scope = new LlmCapabilityScope(
        Echoglossian.TransEngines.ChatGPT,
        "OpenAI",
        "https://api.openai.com/v1",
        "gpt-5.6-terra");

    var snapshot = LlmCapabilityResolver.Resolve(
        scope,
        Array.Empty<LlmCapabilityRuleDefinition>());

    snapshot.GetDecision(LlmCapabilityParameterName.Temperature)
        .SupportState
        .Should()
        .Be(LlmCapabilitySupportState.Unsupported);
}

[Fact]
public void Resolve_WithUnknownSupport_RemainsConservative()
{
    var scope = new LlmCapabilityScope(
        Echoglossian.TransEngines.Gemini,
        "Gemini",
        "https://generativelanguage.googleapis.com",
        "gemini-3-pro");

    var snapshot = LlmCapabilityResolver.Resolve(
        scope,
        Array.Empty<LlmCapabilityRuleDefinition>());

    snapshot.GetDecision(LlmCapabilityParameterName.Temperature)
        .SupportState
        .Should()
        .Be(LlmCapabilitySupportState.Unknown);
}
```

- [ ] **Step 2: Run the focused resolver tests to verify failure**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~LlmCapabilityResolverTests"
```

Expected: FAIL because the capability contract types and resolver do not exist yet.

- [ ] **Step 3: Implement the minimal contract and catalog code**

```csharp
public static class LlmCapabilityResolver
{
    public static LlmCapabilitySnapshot Resolve(
        LlmCapabilityScope scope,
        IReadOnlyList<LlmCapabilityRuleDefinition> overlayRules)
    {
        var definitions = LlmCapabilityStaticCatalog.GetDefinitions(scope.Engine);
        return LlmCapabilitySnapshot.Create(scope, definitions, overlayRules);
    }
}
```

```csharp
internal static IEnumerable<LlmCapabilityRuleDefinition> GetDefinitions(
    Echoglossian.TransEngines engine)
{
    if (engine == Echoglossian.TransEngines.ChatGPT)
    {
        yield return LlmCapabilityRuleDefinition.FamilyPrefix(
            "OpenAI",
            "https://api.openai.com/v1",
            "gpt-5.6-",
            LlmCapabilityParameterName.Temperature,
            LlmCapabilitySupportState.Unsupported,
            omitWhenDefaultOnly: true,
            reason: "OpenAI chat-completions reasoning models accept only the implicit default temperature.");
    }
}
```

- [ ] **Step 4: Run the focused resolver tests to verify pass**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~LlmCapabilityResolverTests"
```

Expected: PASS with exact-model, family-prefix, and conservative-unknown coverage green.

- [ ] **Step 5: Commit**

```powershell
git add -- Translators/Capabilities Echoglossian.Tests/LlmCapabilityResolverTests.cs
git commit -m "feat: add LLM capability resolver primitives"
```

### Task 2: Persist capability overlays and hydrate an in-memory cache

**Files:**
- Create: `EFCoreSqlite/Models/LlmModelCapabilityRule.cs`
- Create: `EFCoreSqlite/Models/LlmModelCapabilityObservation.cs`
- Create: `Cache/LlmCapabilityCacheManager.cs`
- Create: `DBHelpers/LlmCapabilityPersistenceHelper.cs`
- Modify: `EFCoreSqlite/EchoglossianDbContext.cs`
- Modify: `EFCoreSqlite/Migrations/EchoglossianDbContextModelSnapshot.cs`
- Create: `EFCoreSqlite/Migrations/20260812130000_AddLlmCapabilityMatrix.cs`
- Create: `EFCoreSqlite/Migrations/20260812130000_AddLlmCapabilityMatrix.Designer.cs`
- Modify: `Echoglossian.cs`
- Test: `Echoglossian.Tests/LlmCapabilityPersistenceTests.cs`
- Test: `Echoglossian.Mock.Tests/PluginStartupSmokeTests.cs`

**Interfaces:**
- Consumes: `LlmCapabilityScope`, `LlmCapabilityParameterName`, `LlmCapabilitySupportState`, existing `EchoglossianDbContext(string configDir)` startup flow.
- Produces:
  - `public DbSet<LlmModelCapabilityRule> LlmModelCapabilityRules { get; set; }`
  - `public DbSet<LlmModelCapabilityObservation> LlmModelCapabilityObservations { get; set; }`
  - `public static void Initialize(string configDir);`
  - `public static IReadOnlyList<LlmCapabilityRuleDefinition> GetRuleDefinitions();`
  - `public static void UpsertRules(string configDir, IReadOnlyList<LlmModelCapabilityRule> rules);`
  - `public static void RecordObservation(string configDir, LlmModelCapabilityObservation observation);`

- [ ] **Step 1: Write failing persistence and startup tests**

```csharp
[Fact]
public void UpsertRule_ThenReload_PreservesExactModelLookup()
{
    var configDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(configDir);
    LlmCapabilityPersistenceHelper.UpsertRules(
        configDir,
        new[]
        {
            LlmModelCapabilityRule.CreateExactModel(
                "ChatGPT",
                "OpenAI",
                "https://api.openai.com/v1",
                "gpt-5.6-terra",
                LlmCapabilityParameterName.Temperature,
                LlmCapabilitySupportState.Unsupported,
                omitWhenDefaultOnly: true,
                source: "Observed400",
                reason: "provider rejected non-default temperature"),
        });

    LlmCapabilityCacheManager.Initialize(configDir);

    LlmCapabilityCacheManager.GetRuleDefinitions()
        .Should()
        .ContainSingle(rule => rule.MatchValue == "gpt-5.6-terra");
}
```

- [ ] **Step 2: Run the focused persistence tests to verify failure**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~LlmCapabilityPersistenceTests|FullyQualifiedName~PluginStartupSmokeTests"
```

Expected: FAIL because the new EF entities, tables, and cache manager are missing.

- [ ] **Step 3: Implement additive EF entities, indexes, and cache hydration**

```csharp
public static class LlmCapabilityCacheManager
{
    public static void Initialize(string configDir)
    {
        using var context = new EchoglossianDbContext(configDir);
        cachedRules = context.LlmModelCapabilityRules.AsNoTracking().ToList();
        cachedObservations = context.LlmModelCapabilityObservations
            .AsNoTracking()
            .OrderByDescending(row => row.ObservedAtUtc)
            .Take(128)
            .ToList();
    }
}
```

```csharp
modelBuilder.Entity<LlmModelCapabilityRule>()
    .HasIndex(row => new
    {
        row.Engine,
        row.ProviderScope,
        row.EndpointScope,
        row.MatchType,
        row.MatchValue,
        row.ParameterName,
    })
    .IsUnique()
    .HasDatabaseName("IX_llmmodelcapabilityrules_lookup");
```

- [ ] **Step 4: Run the persistence and startup tests to verify pass**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~LlmCapabilityPersistenceTests"
dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore -p:VSTestMaxCpuCount=1 --filter "FullyQualifiedName~PluginStartupSmokeTests"
```

Expected: PASS with additive migration and startup hydration covered.

- [ ] **Step 5: Commit**

```powershell
git add -- EFCoreSqlite Cache DBHelpers Echoglossian.cs Echoglossian.Tests/LlmCapabilityPersistenceTests.cs Echoglossian.Mock.Tests/PluginStartupSmokeTests.cs
git commit -m "feat: persist LLM capability overlays"
```

### Task 3: Add the shared policy service and bounded failure learning

**Files:**
- Create: `Translators/Capabilities/LlmCapabilityPolicyService.cs`
- Create: `Translators/Capabilities/LlmCapabilityRefreshPromoter.cs`
- Create: `Translators/Capabilities/LlmCapabilityErrorClassifier.cs`
- Modify: `GeneralHelpers/RuntimeConfigurationRefresh.cs`
- Test: `Echoglossian.Tests/LlmCapabilityPolicyServiceTests.cs`
- Test: `Echoglossian.Tests/LlmCapabilityErrorClassifierTests.cs`
- Test: `Echoglossian.Tests/RuntimeConfigurationRefreshContractTests.cs`

**Interfaces:**
- Consumes: `LlmCapabilityCacheManager`, `LlmCapabilityPersistenceHelper`, `OpenAiProviderVariantHelper.OpenAiProviderSettings`, existing runtime refresh contract.
- Produces:
  - `public static class LlmCapabilityPolicyService`
  - `public static class LlmCapabilityRefreshPromoter`
  - `public readonly record struct LlmCapabilityLearningResult(bool ObservationRecorded, bool RulePromoted, string FailureKind);`

- [ ] **Step 1: Write failing tests for scope creation, sanitization, and exact-model learning**

```csharp
[Fact]
public void TryResolveTemperature_WithUnsupportedDecision_OmitsPayloadValue()
{
    var scope = LlmCapabilityPolicyService.CreateScope(
        Echoglossian.TransEngines.ChatGPT,
        "OpenAI",
        "https://api.openai.com/v1",
        "gpt-5.6-terra");

    var resolved = LlmCapabilityPolicyService.TryResolveTemperature(
        scope,
        0.7f,
        out var sanitizedValue,
        out var decision);

    resolved.Should().BeFalse();
    sanitizedValue.Should().Be(default);
    decision.SupportState.Should().Be(LlmCapabilitySupportState.Unsupported);
}

[Fact]
public void LearnFromProviderFailure_WithClassifiedTemperature400_PromotesExactModelOnly()
{
    var scope = new LlmCapabilityScope(
        Echoglossian.TransEngines.ChatGPT,
        "OpenAI",
        "https://api.openai.com/v1",
        "gpt-5.6-terra");

    var result = LlmCapabilityPolicyService.LearnFromProviderFailure(
        scope,
        LlmCapabilityParameterName.Temperature,
        400,
        "{ \"error\": { \"message\": \"Unsupported value: 'temperature' does not support 0.7 with this model.\" } }");

    result.RulePromoted.Should().BeTrue();
    result.FailureKind.Should().Be("unsupported-parameter");
}
```

- [ ] **Step 2: Run the focused policy tests to verify failure**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~LlmCapabilityPolicyServiceTests|FullyQualifiedName~LlmCapabilityErrorClassifierTests|FullyQualifiedName~RuntimeConfigurationRefreshContractTests"
```

Expected: FAIL because the policy service, classifier, and refresh clear path do not exist yet.

- [ ] **Step 3: Implement the shared policy and learning helpers**

```csharp
public static bool TryResolveTemperature(
    LlmCapabilityScope scope,
    float configuredValue,
    out float sanitizedValue,
    out LlmCapabilityParameterDecision decision)
{
    decision = GetSnapshot(scope).GetDecision(LlmCapabilityParameterName.Temperature);
    sanitizedValue = configuredValue;

    if (decision.SupportState != LlmCapabilitySupportState.Supported ||
        decision.OmitWhenDefaultOnly)
    {
        sanitizedValue = default;
        return false;
    }

    if (decision.MinValue.HasValue && configuredValue < decision.MinValue.Value)
    {
        sanitizedValue = decision.MinValue.Value;
    }

    if (decision.MaxValue.HasValue && configuredValue > decision.MaxValue.Value)
    {
        sanitizedValue = decision.MaxValue.Value;
    }

    return true;
}
```

```csharp
public static LlmCapabilityLearningResult LearnFromProviderFailure(
    LlmCapabilityScope scope,
    LlmCapabilityParameterName parameterName,
    int? statusCode,
    string? responseText)
{
    var classification = LlmCapabilityErrorClassifier.TryClassify(
        scope,
        parameterName,
        statusCode,
        responseText);
    return classification.Apply(scope);
}
```

- [ ] **Step 4: Run the focused policy tests to verify pass**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~LlmCapabilityPolicyServiceTests|FullyQualifiedName~LlmCapabilityErrorClassifierTests|FullyQualifiedName~RuntimeConfigurationRefreshContractTests"
```

Expected: PASS with exact-model promotion only and runtime reset behavior covered.

- [ ] **Step 5: Commit**

```powershell
git add -- Translators/Capabilities GeneralHelpers/RuntimeConfigurationRefresh.cs Echoglossian.Tests/LlmCapabilityPolicyServiceTests.cs Echoglossian.Tests/LlmCapabilityErrorClassifierTests.cs Echoglossian.Tests/RuntimeConfigurationRefreshContractTests.cs
git commit -m "feat: add conservative LLM capability policy service"
```

### Task 4: Sanitize OpenAI-family and Anthropic request payloads

**Files:**
- Modify: `Translators/ChatGPTTranslator.cs`
- Modify: `Translators/OpenRouterTranslator.cs`
- Modify: `Translators/DeepSeekTranslator.cs`
- Modify: `Translators/LmStudioTranslator.cs`
- Modify: `Translators/ClaudeTranslator.cs`
- Modify: `Echoglossian.Tests/TranslationServiceTests.cs`
- Test: `Echoglossian.Tests/LlmCapabilityPolicyServiceTests.cs`

**Interfaces:**
- Consumes:
  - `public static LlmCapabilityScope CreateScope(Echoglossian.TransEngines engine, string providerScope, string? endpointScope, string? modelId);`
  - `public static bool TryResolveTemperature(LlmCapabilityScope scope, float configuredValue, out float sanitizedValue, out LlmCapabilityParameterDecision decision);`
  - `public static LlmCapabilityLearningResult LearnFromProviderFailure(LlmCapabilityScope scope, LlmCapabilityParameterName parameterName, int? statusCode, string? responseText);`
- Produces:
  - provider request builders that omit unsupported `temperature`
  - provider failure handlers that record classified exact-model observations

- [ ] **Step 1: Extend tests to prove temperature is omitted and exact-model learning is recorded**

```csharp
[Fact]
public void CreateScope_ForOfficialOpenAi_UsesProviderVariantAndNormalizedEndpoint()
{
    var scope = LlmCapabilityPolicyService.CreateScope(
        Echoglossian.TransEngines.ChatGPT,
        "OpenAI",
        "https://api.openai.com/v1/",
        "gpt-5.6-terra");

    scope.ProviderScope.Should().Be("OpenAI");
    scope.EndpointScope.Should().Be("https://api.openai.com/v1");
}
```

```csharp
[Fact]
public async Task TranslateAsync_WhenStructuredFailureIsClassified_PromotesExactModelRule()
{
    var result = LlmCapabilityPolicyService.LearnFromProviderFailure(
        new LlmCapabilityScope(
            Echoglossian.TransEngines.ChatGPT,
            "OpenAI",
            "https://api.openai.com/v1",
            "gpt-5.6-terra"),
        LlmCapabilityParameterName.ReasoningEffort,
        400,
        "function tools with reasoning_effort are not supported");

    result.RulePromoted.Should().BeTrue();
}
```

- [ ] **Step 2: Run the focused tests to verify failure**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~LlmCapabilityPolicyServiceTests|FullyQualifiedName~TranslationServiceTests"
```

Expected: FAIL because the translators still set `Temperature` unconditionally and do not report learning outcomes centrally.

- [ ] **Step 3: Implement minimal OpenAI-family and Claude sanitization**

```csharp
var scope = LlmCapabilityPolicyService.CreateScope(
    Echoglossian.TransEngines.ChatGPT,
    providerSettings.ProviderName,
    baseUrl,
    this.model);

var options = new ChatCompletionOptions();
if (LlmCapabilityPolicyService.TryResolveTemperature(
        scope,
        this.temperature,
        out var sanitizedTemperature,
        out _))
{
    options.Temperature = sanitizedTemperature;
}
```

```csharp
var learning = LlmCapabilityPolicyService.LearnFromProviderFailure(
    scope,
    LlmCapabilityParameterName.Temperature,
    statusCode,
    responseExcerpt);
PluginRuntimeLog.Debug(
    this.pluginLog,
    $"Capability learning: promoted={learning.RulePromoted}, kind={learning.FailureKind}");
```

- [ ] **Step 4: Run the focused tests to verify pass**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~LlmCapabilityPolicyServiceTests|FullyQualifiedName~TranslationServiceTests"
```

Expected: PASS with OpenAI-family and Claude request sanitization verified through shared policy behavior.

- [ ] **Step 5: Commit**

```powershell
git add -- Translators/ChatGPTTranslator.cs Translators/OpenRouterTranslator.cs Translators/DeepSeekTranslator.cs Translators/LmStudioTranslator.cs Translators/ClaudeTranslator.cs Echoglossian.Tests/TranslationServiceTests.cs Echoglossian.Tests/LlmCapabilityPolicyServiceTests.cs
git commit -m "fix: sanitize OpenAI-family and Claude capability parameters"
```

### Task 5: Sanitize Gemini, Ollama, and remaining provider-specific payloads

**Files:**
- Modify: `Translators/GeminiTranslator.cs`
- Modify: `Translators/OllamaTranslator.cs`
- Modify: `Translators/OpenAI/OpenAIModelManager.cs`
- Modify: `Translators/Claude/ClaudeModelManager.cs`
- Modify: `Translators/Gemini/GeminiModelManager.cs`
- Modify: `Translators/OpenRouter/OpenRouterModelManager.cs`
- Modify: `Translators/Ollama/OllamaModelManager.cs`
- Modify: `Translators/DeepSeek/DeepSeekModelManager.cs`
- Modify: `Translators/LmStudio/LmStudioModelManager.cs`
- Test: `Echoglossian.Tests/LlmCapabilityPolicyServiceTests.cs`
- Test: `Echoglossian.Tests/DeepSeekModelManagerTests.cs`

**Interfaces:**
- Consumes: `LlmCapabilityPolicyService`, `LlmCapabilityRefreshPromoter`.
- Produces:
  - provider-specific omission of unsupported sampling parameters on the remaining LLM engines
  - refresh-time exact-model promotions for non-OpenAI managers

- [ ] **Step 1: Add failing tests for family defaults and refresh promotion**

```csharp
[Fact]
public void PromoteDiscoveredModels_WithGemini3Model_CreatesExactRuleFromFamilyDefault()
{
    LlmCapabilityRefreshPromoter.PromoteDiscoveredModels(
        Echoglossian.TransEngines.Gemini,
        "Gemini",
        "https://generativelanguage.googleapis.com",
        new[] { "gemini-3-pro" },
        new DateTime(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc));

    LlmCapabilityCacheManager.GetRuleDefinitions()
        .Should()
        .Contain(rule => rule.MatchValue == "gemini-3-pro");
}
```

- [ ] **Step 2: Run the focused tests to verify failure**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~LlmCapabilityPolicyServiceTests|FullyQualifiedName~DeepSeekModelManagerTests|FullyQualifiedName~OpenAIModelManagerTests"
```

Expected: FAIL because the remaining model managers do not promote discovered models and some provider payloads still serialize unsupported parameters.

- [ ] **Step 3: Implement minimal provider-specific sanitization and promotion hooks**

```csharp
if (models.Count > 0)
{
    CurrentModelList = models;
    LlmCapabilityRefreshPromoter.PromoteDiscoveredModels(
        Echoglossian.TransEngines.Gemini,
        "Gemini",
        "https://generativelanguage.googleapis.com",
        models.Select(model => model.Id).ToArray(),
        DateTime.UtcNow);
}
```

```csharp
if (LlmCapabilityPolicyService.TryResolveTemperature(
        scope,
        this.temperature,
        out var sanitizedTemperature,
        out _))
{
    payload["temperature"] = sanitizedTemperature;
}
```

- [ ] **Step 4: Run the focused tests to verify pass**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~LlmCapabilityPolicyServiceTests|FullyQualifiedName~DeepSeekModelManagerTests|FullyQualifiedName~OpenAIModelManagerTests"
```

Expected: PASS with non-OpenAI refresh promotion and payload omission behavior covered.

- [ ] **Step 5: Commit**

```powershell
git add -- Translators/GeminiTranslator.cs Translators/OllamaTranslator.cs Translators/OpenAI/OpenAIModelManager.cs Translators/Claude/ClaudeModelManager.cs Translators/Gemini/GeminiModelManager.cs Translators/OpenRouter/OpenRouterModelManager.cs Translators/Ollama/OllamaModelManager.cs Translators/DeepSeek/DeepSeekModelManager.cs Translators/LmStudio/LmStudioModelManager.cs Echoglossian.Tests/DeepSeekModelManagerTests.cs Echoglossian.Tests/OpenAIModelManagerTests.cs Echoglossian.Tests/LlmCapabilityPolicyServiceTests.cs
git commit -m "feat: promote discovered LLM capability overlays"
```

### Task 6: Gate the LLM engine UI from the shared policy snapshot

**Files:**
- Create: `PluginUI/EngineConfigUI/LlmCapabilityUiHelper.cs`
- Modify: `PluginUI/EngineConfigUI/ChatGptEngineUI.cs`
- Modify: `PluginUI/EngineConfigUI/ClaudeEngineUI.cs`
- Modify: `PluginUI/EngineConfigUI/GeminiEngineUI.cs`
- Modify: `PluginUI/EngineConfigUI/DeepSeekEngineUI.cs`
- Modify: `PluginUI/EngineConfigUI/OpenRouterEngineUI.cs`
- Modify: `PluginUI/EngineConfigUI/OllamaEngineUI.cs`
- Modify: `PluginUI/EngineConfigUI/LmStudioEngineUI.cs`
- Modify: `Properties/Resources.resx`
- Modify: `Properties/Resources.en-US.resx`
- Modify: `Properties/Resources.pt-BR.resx`
- Modify: `Properties/Resources.Designer.cs`
- Test: `Echoglossian.Tests/LlmCapabilityUiHelperTests.cs`
- Test: `Echoglossian.Tests/ConfigDefaultsTests.cs`

**Interfaces:**
- Consumes:
  - `public static LlmCapabilityScope CreateScope(Echoglossian.TransEngines engine, string providerScope, string? endpointScope, string? modelId);`
  - existing engine-specific config/model selectors and selected model ids.
- Produces:
  - `public readonly record struct LlmCapabilitySliderState(bool IsEnabled, float MinValue, float MaxValue, string TooltipText);`
  - `public static LlmCapabilitySliderState GetTemperatureSliderState(LlmCapabilityScope scope, float fallbackMin, float fallbackMax);`

- [ ] **Step 1: Write failing UI helper tests**

```csharp
[Fact]
public void GetTemperatureSliderState_WithDefaultOnlyDecision_DisablesControlAndExplainsWhy()
{
    var scope = new LlmCapabilityScope(
        Echoglossian.TransEngines.ChatGPT,
        "OpenAI",
        "https://api.openai.com/v1",
        "gpt-5.6-terra");

    var state = LlmCapabilityUiHelper.GetTemperatureSliderState(
        scope,
        0.1f,
        1.0f);

    state.IsEnabled.Should().BeFalse();
    state.TooltipText.Should().Contain("default-only");
}
```

- [ ] **Step 2: Run the focused UI tests to verify failure**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~LlmCapabilityUiHelperTests|FullyQualifiedName~ConfigDefaultsTests"
```

Expected: FAIL because the UI helper and tooltip resources do not exist yet.

- [ ] **Step 3: Implement the shared UI helper and wire each engine slider through it**

```csharp
var sliderState = LlmCapabilityUiHelper.GetTemperatureSliderState(
    scope,
    0.1f,
    1.0f);

ImGui.BeginDisabled(!sliderState.IsEnabled);
if (ImGui.SliderFloat(Resources.Temperature, ref temp, sliderState.MinValue, sliderState.MaxValue, "%.1f"))
{
    config.ChatGptTemperature = temp;
    changed = true;
}
ImGui.EndDisabled();

if (!sliderState.IsEnabled && ImGui.IsItemHovered())
{
    ImGui.SetTooltip(sliderState.TooltipText);
}
```

- [ ] **Step 4: Run the focused UI tests to verify pass**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~LlmCapabilityUiHelperTests|FullyQualifiedName~ConfigDefaultsTests"
```

Expected: PASS with disabled-slider tooltip text and dynamic ranges covered.

- [ ] **Step 5: Commit**

```powershell
git add -- PluginUI/EngineConfigUI Properties Echoglossian.Tests/LlmCapabilityUiHelperTests.cs Echoglossian.Tests/ConfigDefaultsTests.cs
git commit -m "feat: gate LLM config controls by capability"
```

### Task 7: Validate the matrix end to end and prepare PR evidence

**Files:**
- Modify: `Echoglossian.xml` if XML docs changed
- Verify: `docs/superpowers/specs/2026-08-12-llm-capability-matrix-design.md`
- Verify: `docs/superpowers/plans/2026-08-12-llm-capability-matrix-implementation-plan.md`

**Interfaces:**
- Consumes: all prior tasks’ committed code and tests.
- Produces: validated branch evidence ready for a PR to `v4-series`.

- [ ] **Step 1: Run focused matrix tests**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~LlmCapabilityResolverTests|FullyQualifiedName~LlmCapabilityPersistenceTests|FullyQualifiedName~LlmCapabilityPolicyServiceTests|FullyQualifiedName~LlmCapabilityErrorClassifierTests|FullyQualifiedName~LlmCapabilityUiHelperTests|FullyQualifiedName~OpenAIModelManagerTests|FullyQualifiedName~DeepSeekModelManagerTests|FullyQualifiedName~TranslationServiceTests|FullyQualifiedName~RuntimeConfigurationRefreshContractTests|FullyQualifiedName~ConfigDefaultsTests"
```

Expected: PASS.

- [ ] **Step 2: Run full build, full tests, Mock startup tests, and whitespace validation**

Run:

```powershell
dotnet build Echoglossian.sln -c Debug --no-restore
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1
git diff --check
```

Expected: PASS, and include `Echoglossian.xml` in the commit if the build updates it.

- [ ] **Step 3: Run in-game verification focused on current failures**

Check:

```text
1. Official OpenAI + gpt-5.6-terra: temperature slider disabled with tooltip; request no longer returns 400 for non-default temperature because the payload omits it.
2. Official OpenAI structured dialogue on the same model: reasoning/tool incompatibility records one exact-model observation and the next request follows the learned conservative rule.
3. Claude 4.7+/5 family model: temperature control disabled when policy marks default-only.
4. Gemini 3 family: unknown or unsupported sampling remains omitted conservatively while the UI explains why.
5. Live-model refresh on OpenAI, Gemini, DeepSeek, Ollama, and LM Studio updates model lists without blocking Draw and promotes exact-model capability rows derived from family rules.
6. Changing endpoint, provider variant, or model produces a different capability scope and does not reuse the previous scope’s learned rule incorrectly.
```

- [ ] **Step 4: Prepare PR evidence and commit any remaining generated files**

```powershell
git add -- Echoglossian.xml
git status --short
git commit -m "chore: finalize LLM capability matrix validation"
```

Expected: Only real generated or validation-followup files are staged; skip the commit if no new files changed in this final validation pass.
