# Claude Engine Integration Checklist

## Goal

Add Anthropic Claude as a first-class translation engine without creating a parallel translation path and without breaking the existing engine-selection architecture.

## Implementation Checklist

### Shared engine wiring

- Add `Claude` to `TransEngines` at the end of the enum.
- Add `Claude` to the engine name list in `PluginUI`.
- Add `Claude` to `TranslatorFactory`.
- Add `Claude` to `TranslatorEngineMap`.
- Add `Claude` to the broad-coverage LLM list in `LanguageEngineSupport`.

### Config surface

Add persisted config fields for:

- `ClaudeApiKey`
- `ClaudeBaseUrl`
- `ClaudeModel`
- `ClaudePrompt`
- `ClaudeTemperature`
- `UseLiveClaudeModelList`

### Prompt surface

- Add `PromptType.Claude`.
- Add Claude prompt load/save support to `PromptTemplateManager`.
- Keep Claude on the shared prompt editor UI rather than inventing a one-off editor.

### Runtime engine surface

Add:

- `Translators/ClaudeTranslator.cs`
- `Translators/Claude/ClaudeTextModelDefaults.cs`
- `Translators/Claude/ClaudeModelManager.cs`

Claude runtime expectations:

- use Anthropic Messages API
- send `x-api-key`
- send `anthropic-version`
- parse `content[]` text blocks
- cache per exact text and language pair like the other LLM engines

### UI surface

Add:

- `PluginUI/EngineConfigUI/ClaudeEngineUI.cs`
- `TranslationEnginesTab` switch case
- resource strings for Claude warning and settings copy

### Verification surface

Add or update tests for:

- translator factory construction
- engine map
- language support coverage for Claude

## In-Game Verification

After build and test pass, verify:

1. Claude appears in the engine dropdown for common target languages.
2. Claude settings persist after plugin reload.
3. Live model toggle fetches models when a valid Anthropic key is configured.
4. Static defaults still populate the dropdown with live fetch disabled.
5. Translation works in at least one shared text surface and one UI surface that already uses `TranslationService`.

## Non-Goals

This integration does not require:

- a new DB schema
- a new translation queue
- a new prompt editor component
- a new overlay path
- a new persistence format

## Follow-Up Work

Claude should also be added to the manual model-default refresh tooling under `scripts/llm-model-refresh`, but that is separate from the runtime engine integration.
