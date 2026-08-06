# Issue 171 DeepSeek runtime authentication Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make DeepSeek configuration and runtime failures accurately diagnosable, fix short-key constructor failure, and guarantee credentials are never sliced or logged.

**Architecture:** Validate DeepSeek inputs before translator construction, inject a reusable HTTP transport seam for deterministic tests, and map HTTP/network/model/schema outcomes into the shared failure classification. Runtime UI receives only localized, sanitized status; raw keys and request content never enter logs or metrics.

**Tech Stack:** C#/.NET 10, `HttpClient`, existing DeepSeek translator/model manager, translation failure classifier, ImGui/resx, xUnit fake handlers, and PowerShell.

## Global Constraints

- Branch from the merged #176 result as `issue-171-deepseek-runtime-auth`.
- Scope the remaining DeepSeek unavailable/auth/runtime problem only. Do not mix quest-window UI comments already separated/resolved elsewhere.
- Never log complete or partial API keys, authorization headers, prompts, glossary terms, or response bodies that may echo sensitive input.
- Do not make a network request from Draw; model refresh and health checks stay owned async operations.
- Distinguish configuration missing, invalid endpoint, 401, 403, 404/model, 429/quota, timeout, DNS/connectivity, malformed response, and schema incompatibility.
- Authentication/rate-limit/network failures must not poison the persistent translation-failure cache as content defects.
- Preserve DeepSeek's shared OpenAI-compatible structured adapter from #148.

---

## File map

### New files

- `Translators/DeepSeek/DeepSeekConfigurationValidator.cs`.
- `Echoglossian.Tests/DeepSeekTranslatorTests.cs`.
- `Echoglossian.Tests/DeepSeekConfigurationValidatorTests.cs`.

### Modified files

- `Translators/DeepSeekTranslator.cs` — remove key slicing, inject/test transport, propagate classified failures.
- `Translators/DeepSeek/DeepSeekModelManager.cs` — sanitized async model-list outcomes.
- `PluginUI/EngineConfigUI/DeepSeekEngineUI.cs` — validation and non-blocking status.
- `PluginUI/Helpers/TranslationEngineConfigurationHelper.cs` — shared configured predicate.
- `GeneralHelpers/TranslationFailureTextClassifier.cs` — DeepSeek status mapping.
- `Translators/TranslationService.cs` — apply the transient/persistent classification contract.
- Existing DeepSeek model manager, translation failure, service, and runtime refresh tests.
- `Properties/Resources.resx`, `Properties/Resources.en-US.resx`, `Properties/Resources.pt-BR.resx`, and `Properties/Resources.Designer.cs`.

## Task 1: Reproduce the constructor/key-slicing defect

- [ ] Add tests constructing DeepSeek with empty, 1-character, 19-character, 20-character, and normal keys. Capture logs and assert no key or substring appears.
- [ ] Assert a short nonempty key does not turn the translator into a generic unavailable instance due to constructor exception.
- [ ] Run the focused tests and confirm current code fails for keys shorter than the logged slice lengths.
- [ ] Delete all key-prefix/suffix logging. Log only booleans such as `ApiKeyConfigured=true`, normalized provider name, and sanitized endpoint host.
- [ ] Make validation return a classified configuration result instead of nulling `HttpClient` after a broad constructor catch.
- [ ] Commit:

```powershell
git add -- Translators/DeepSeekTranslator.cs Translators/DeepSeek/DeepSeekConfigurationValidator.cs Echoglossian.Tests/DeepSeekTranslatorTests.cs Echoglossian.Tests/DeepSeekConfigurationValidatorTests.cs
git commit -m "fix(#171): remove DeepSeek key slicing failure"
```

## Task 2: Classify DeepSeek HTTP and transport outcomes

- [ ] Add fake-handler tests for 200 valid/malformed, 400, 401, 403, 404, 429, 500, timeout cancellation, DNS/connectivity exception, and structured incompatibility.
- [ ] Assert the Authorization header is set for a configured key but compare only against the test fixture in-memory; never print it in assertion messages.
- [ ] Add an internal constructor accepting `HttpClient` or `HttpMessageHandler` while production retains one reusable client.
- [ ] Map statuses to stable failure reasons and localized operator messages. Mark auth/quota/network/timeout as transient/non-content; mark only deterministic invalid response/content outcomes according to the shared persistence guard.
- [ ] Include sanitized request correlation/status metadata in debug diagnostics without body or credentials.
- [ ] Run DeepSeek, classifier, service, and metrics tests; commit:

```powershell
git add -- Translators/DeepSeekTranslator.cs GeneralHelpers/TranslationFailureTextClassifier.cs Translators/TranslationService.cs Echoglossian.Tests
git commit -m "fix(#171): classify DeepSeek runtime failures"
```

## Task 3: Expose safe non-blocking configuration status

- [ ] Add validator tests for missing key, malformed/non-HTTPS official endpoint, valid custom endpoint when supported, missing model, and whitespace normalization.
- [ ] Change `TranslationEngineConfigurationHelper` and DeepSeek UI to show localized field validation before runtime construction.
- [ ] Ensure live model refresh captures immutable key/base-URL strings, runs asynchronously, owns exceptions, and publishes only a sanitized success/failure snapshot.
- [ ] Add a one-shot test/refresh button only through the existing async model/health coordinator; disable it while in flight and never poll every frame.
- [ ] Add resx messages for each operator-actionable class without echoing provider body text.
- [ ] Commit:

```powershell
git add -- PluginUI/EngineConfigUI/DeepSeekEngineUI.cs PluginUI/Helpers/TranslationEngineConfigurationHelper.cs Translators/DeepSeek/DeepSeekModelManager.cs Properties Echoglossian.Tests
git commit -m "feat(#171): report safe DeepSeek configuration status"
```

## Task 4: Validate and close #171

- [ ] Run focused DeepSeek/security/classifier tests, full build/tests, Mock startup/config tests, `git diff --check`, and include `Echoglossian.xml` if changed.
- [ ] Search for secret slicing/logging:

```powershell
rg -n "apiKey.*\[|ApiKey.*\[|Authorization|Bearer|DeepSeek.*key" Translators PluginUI GeneralHelpers
```

Expected result: Authorization construction remains, but no key substring or value logging exists.

- [ ] In game, test missing, deliberately invalid, and valid DeepSeek credentials; bad model; 429 simulation when possible; and provider delay. Confirm precise sanitized status, responsive UI, no persistent content-failure poisoning, and recovery after config correction.
- [ ] Attach sanitized current-version evidence to #171 and state explicitly that unrelated quest UI comments were not bundled; open the PR to `v4-series`.
