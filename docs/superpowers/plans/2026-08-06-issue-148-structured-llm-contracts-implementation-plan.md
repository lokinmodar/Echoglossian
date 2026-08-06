# Issue 148 structured LLM contracts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish provider-aware structured dialogue translation with typed speaker/text output, glossary and metadata support, operator-selectable Auto/Structured/PlainText modes, safe fallback, target-change isolation, and an optional bounded official-OpenAI Responses session.

**Architecture:** Preserve `ITranslator` and add one optional typed dialogue capability consumed only by `TranslationService`. A shared internal request/result contract is adapted to OpenAI-compatible tools, Gemini schema, Claude tools, Ollama JSON schema, or plain-text glossary prompts. Auto mode uses a runtime capability circuit breaker; Structured mode fails visibly instead of silently changing semantics; PlainText never sends structured fields. Provider/session state is bounded, runtime-only, and keyed by effective engine/model/source/target/session.

**Tech Stack:** C#/.NET 10, existing structured dialogue records/helpers, provider HTTP APIs, JSON Schema/tool calling, xUnit fake `HttpMessageHandler`s, resx/ImGui, and PowerShell.

## Global Constraints

- Branch from the merged #209 result as `issue-148-structured-llm-contracts`.
- Keep `TranslationService` as the single orchestration path and retain `ITranslator` compatibility.
- Do not require every provider/model to support the same wire format.
- Modes are exact: `Auto` may try structured then one plain fallback; `Structured` returns a classified failure on incompatibility; `PlainText` never attempts structured output.
- A structured provider request returns translated speaker and body in one response. Do not send a second speaker request after typed success.
- PlainText mode may retain the current legacy speaker behavior, but it must remain async and cancellation-safe.
- Validate every typed response before cache or DB persistence. Reject annotations/code fences/extraneous fields where the strict provider contract promises strictness.
- Use the #252 glossary protector regardless of wire format; structured glossary payload improves model guidance but does not replace deterministic marker validation.
- Circuit-breaker/session state contains no dialogue text or credentials and is cleared on config/model/target changes and plugin shutdown.
- Verify current official provider documentation during implementation; cite the primary API docs in the PR when wire contracts changed.

---

## File map

### New files

- `Translators/StructuredDialogueMode.cs` — `Auto`, `Structured`, `PlainText` enum.
- `Translators/DialogueTranslationResult.cs` — typed accepted speaker/body result plus structured/fallback provenance.
- `Translators/IStructuredDialogueTranslator.cs` — optional typed capability.
- `Translators/StructuredDialogueCapabilityStateStore.cs` — bounded runtime Auto-mode breaker keyed by provider/model/endpoint signature.
- `Translators/Helpers/PlainTextDialogueGlossaryPromptHelper.cs` — Sakura-compatible plain glossary and metadata block.
- `Translators/OpenAI/OpenAiResponsesDialogueSessionStore.cs` — optional bounded runtime response-chain identities for official OpenAI only.
- `Echoglossian.Tests/StructuredDialogueCapabilityStateStoreTests.cs`.
- `Echoglossian.Tests/PlainTextDialogueGlossaryPromptHelperTests.cs`.
- `Echoglossian.Tests/OpenAiResponsesDialogueSessionStoreTests.cs`.

### Modified files

- `Config.cs` — structured mode and official-OpenAI Responses-session opt-in.
- `PluginUI/Tabs/TranslationEnginesTab.cs` — mode/session controls and capability status.
- `GeneralHelpers/RuntimeConfigurationRefresh.cs` — signature and breaker/session reset.
- `Translators/TranslationService.cs` — typed capability selection and accepted result.
- `Translators/StructuredDialogueTranslationRequest.cs`, `StructuredDialogueTranslationResponse.cs`, and `StructuredDialogueTranslationMetadata.cs`.
- `Translators/Helpers/StructuredDialogueTranslationRequestBuilder.cs` and `StructuredDialogueTranslationResponseValidator.cs`.
- `Translators/ChatGPTTranslator.cs`, `DeepSeekTranslator.cs`, `OpenRouterTranslator.cs`, `LmStudioTranslator.cs` — OpenAI-compatible adapters.
- `Translators/GeminiTranslator.cs` — Gemini schema adapter.
- `Translators/ClaudeTranslator.cs` — Anthropic tool adapter.
- `Translators/OllamaTranslator.cs` — Ollama `format` schema adapter.
- `NativeUI/AddonHandlers/Talk/TalkHandler.cs`, `BattleTalkHandler.cs` — consume typed speaker/body result.
- Existing structured helper, service, metrics, handler, and runtime refresh tests.
- `Properties/Resources.resx`, `Properties/Resources.en-US.resx`, `Properties/Resources.pt-BR.resx`, and `Properties/Resources.Designer.cs`.

## Interfaces produced and consumed

```csharp
public enum StructuredDialogueMode
{
    Auto = 0,
    Structured = 1,
    PlainText = 2,
}

public readonly record struct DialogueTranslationResult(
    string SpeakerTranslated,
    string TextTranslated,
    bool UsedStructuredContract,
    bool UsedPlainTextFallback);

public interface IStructuredDialogueTranslator
{
    Task<DialogueTranslationResult?> TranslateDialogueAsync(
        StructuredDialogueTranslationRequest request,
        CancellationToken cancellationToken);
}
```

`TranslationService` produces one accepted typed result:

```csharp
public Task<DialogueTranslationResult> TranslateDialogueAsync(
    string text,
    SourceClientLanguage sourceLanguage,
    string targetLanguage,
    DialogueTranslationContext context,
    TranslatorResolution translatorResolution,
    bool translateSpeaker,
    string? originContext,
    CancellationToken cancellationToken);
```

## Task 1: Lock the mode and typed-result semantics with tests

- [ ] Add serialization/normalization tests: absent config defaults to `Auto`; invalid enum normalizes to `Auto`; Responses session defaults false.
- [ ] Add `TranslationServiceTests` with fakes for typed success, Auto structured incompatibility followed by one plain fallback, Structured incompatibility with no fallback, and PlainText with zero typed calls.
- [ ] Assert typed success returns both speaker/body and causes exactly one provider call.
- [ ] Assert protected-marker validation runs before the typed result is accepted.
- [ ] Implement the enum, result, interface, config fields, and service decision skeleton until tests pass.
- [ ] Commit:

```powershell
git add -- Config.cs Translators/StructuredDialogueMode.cs Translators/DialogueTranslationResult.cs Translators/IStructuredDialogueTranslator.cs Translators/TranslationService.cs Echoglossian.Tests
git commit -m "feat(#148): define typed dialogue translation modes"
```

## Task 2: Add the Auto-mode capability circuit breaker

- [ ] Test a key shape containing effective engine, normalized endpoint host/path, model, and structured mode but no credential. Test two consecutive compatibility failures open a ten-minute breaker, success resets it, expiry permits a probe, and maximum retained keys is bounded.
- [ ] Classify only schema/tool unsupported, invalid structured payload, or provider model incompatibility as breaker failures. Do not open it for cancellation, timeout, 401/403, 429, or transient network failure.
- [ ] Implement `StructuredDialogueCapabilityStateStore` with injected clock for tests, a small fixed maximum key count, and aggregate snapshots for diagnostics.
- [ ] Make Auto consult the breaker before a structured attempt and record the outcome afterward. Structured ignores an open Auto breaker; PlainText never consults it.
- [ ] Clear state on runtime translation-signature changes and shutdown.
- [ ] Commit:

```powershell
git add -- Translators/StructuredDialogueCapabilityStateStore.cs Translators/TranslationService.cs GeneralHelpers/RuntimeConfigurationRefresh.cs Echoglossian.Tests
git commit -m "feat(#148): bound structured capability fallback"
```

## Task 3: Finish shared request, response, metadata, and plain glossary contracts

- [ ] Extend request-builder tests for first-turn speaker, prior turns, target changes, source/target codes, surface family, optional quest/role/pronoun/subject metadata, and immutable glossary snapshots.
- [ ] Extend validator tests for missing fields, empty body, optional empty speaker when speaker translation is off, extraneous prose/code fences, annotations, duplicated protected markers, and target-language mismatch signals when detectable.
- [ ] Implement a provider-neutral plain-text block that includes current speaker, bounded prior turns, compact `source => target` glossary rows, and the instruction to output only current translated body (plus a stable separator for translated speaker when requested).
- [ ] Keep metadata optional and omit empty fields from provider payloads to control token cost.
- [ ] Ensure a target-language change creates a different request/cache/session key and cannot reuse prior target output.
- [ ] Commit:

```powershell
git add -- Translators/StructuredDialogueTranslationRequest.cs Translators/StructuredDialogueTranslationResponse.cs Translators/StructuredDialogueTranslationMetadata.cs Translators/Helpers/StructuredDialogueTranslationRequestBuilder.cs Translators/Helpers/StructuredDialogueTranslationResponseValidator.cs Translators/Helpers/PlainTextDialogueGlossaryPromptHelper.cs Echoglossian.Tests
git commit -m "feat(#148): complete shared dialogue contracts"
```

## Task 4: Implement OpenAI-compatible typed adapters

- [ ] Add fake-handler tests for ChatGPT official/custom-compatible, DeepSeek, OpenRouter, and LM Studio. Assert request URL, auth header presence without exposing its value, tool/schema payload, target language, one-call speaker/body response, cancellation, and malformed-output classification.
- [ ] Extract only the duplicated request/response mechanics into the existing OpenAI-compatible helper family; keep endpoint/model/auth configuration provider-owned.
- [ ] Implement `IStructuredDialogueTranslator` in the four translators using the current tool helpers and strict response validator.
- [ ] In Auto, let compatibility failures reach the shared breaker/fallback; do not catch and silently launch a second request inside each translator.
- [ ] Keep per-translator `HttpClient` reuse; add no per-request client construction.
- [ ] Commit:

```powershell
git add -- Translators/ChatGPTTranslator.cs Translators/DeepSeekTranslator.cs Translators/OpenRouterTranslator.cs Translators/LmStudioTranslator.cs Translators/Helpers/StructuredDialogueOpenAiCompatiblePayloadHelper.cs Translators/Helpers/StructuredDialogueOpenAiToolHelper.cs Echoglossian.Tests
git commit -m "feat(#148): unify OpenAI-compatible dialogue contracts"
```

## Task 5: Implement Gemini, Claude, and Ollama adapters

- [ ] Add isolated fake-handler tests for Gemini response schema, Claude tool use, and Ollama `format` schema. Include models that reject the structured field and verify the failure is returned to shared policy rather than internally retried.
- [ ] Implement the typed interface with existing provider-specific helpers. Keep wire property names and auth rules native to each API.
- [ ] Verify PlainText mode sends no schema/tool fields for any of the three providers but still includes the compact glossary block.
- [ ] Verify cancellation reaches `SendAsync`/content reads and no result publishes after cancellation.
- [ ] Commit:

```powershell
git add -- Translators/GeminiTranslator.cs Translators/ClaudeTranslator.cs Translators/OllamaTranslator.cs Translators/Helpers/StructuredDialogueAnthropicToolHelper.cs Echoglossian.Tests
git commit -m "feat(#148): add provider-native dialogue adapters"
```

## Task 6: Consume translated speaker/body atomically in Talk and BattleTalk

- [ ] Add lifecycle tests proving typed success uses one translation request, applies body and optional speaker from the same result, and never launches the legacy speaker request.
- [ ] Update both handlers to call `TranslationService.TranslateDialogueAsync`; use legacy separate speaker translation only when the service reports a PlainText legacy result requiring it.
- [ ] Capture the mode, effective engine/model, target, metadata, and glossary generation before awaiting. Reject completion after source/config generation changes.
- [ ] Preserve runtime-context persistence rules: context-dependent results remain runtime-only unless the explicit visible retranslation flow deliberately validates and persists them under its current contract.
- [ ] Run handler/service tests and commit:

```powershell
git add -- NativeUI/AddonHandlers/Talk/TalkHandler.cs NativeUI/AddonHandlers/Talk/BattleTalkHandler.cs Translators/TranslationService.cs Echoglossian.Tests/NativeDialogueHandlerLifecycleTests.cs Echoglossian.Tests/TranslationServiceTests.cs
git commit -m "fix(#148): publish typed dialogue results atomically"
```

## Task 7: Add optional official-OpenAI Responses session

- [ ] Consult the current official OpenAI Responses API documentation and record the exact request/response fields used in a test fixture; do not infer them from an OpenAI-compatible provider.
- [ ] Test a bounded runtime store keyed by Talk/BattleTalk namespace/session plus source/target/model. A target/model/config change starts a fresh chain; TTL and max-count eviction remove old IDs; no prompt/dialogue/API key is stored.
- [ ] When the opt-in is enabled for official OpenAI only, send the prior response identifier according to current docs and publish the returned identifier only after a valid accepted response. Cancellation/failure must not advance the chain.
- [ ] Custom OpenAI-compatible, DeepSeek, OpenRouter, and LM Studio ignore the opt-in and display a localized explanatory hint.
- [ ] Clear the store on shutdown/runtime signature change.
- [ ] Commit:

```powershell
git add -- Translators/OpenAI/OpenAiResponsesDialogueSessionStore.cs Translators/ChatGPTTranslator.cs PluginUI/Tabs/TranslationEnginesTab.cs GeneralHelpers/RuntimeConfigurationRefresh.cs Properties Echoglossian.Tests
git commit -m "feat(#148): add bounded OpenAI dialogue sessions"
```

## Task 8: Validate and close #148

- [ ] Run every structured helper/provider/service/handler test, full build/tests, Mock tests, `git diff --check`, and include `Echoglossian.xml` if changed.
- [ ] In game, run Auto/Structured/PlainText for each configured LLM provider. Verify first speaker, translated speaker/body, glossary exactness, no annotations, target change, incompatible model fallback, breaker recovery, and optional official OpenAI response chaining.
- [ ] Suspend each provider and rapidly change dialogue/target/config; verify UI responsiveness and stale-result rejection.
- [ ] Attach a sanitized capability matrix and request-count evidence to #148; open the PR to `v4-series`.
