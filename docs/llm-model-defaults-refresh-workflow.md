# LLM Model Defaults Refresh

Use [update-llm-model-defaults.ps1](/C:/Dante/_dalamud/Echoglossian/scripts/update-llm-model-defaults.ps1) to refresh the committed fallback model lists for the LLM engines.

This workflow is intentionally manual:
- it does not run during normal local builds
- it does not run in CI/CD
- it only updates the static `*TextModelDefaults.cs` files after you choose to run it

## What It Generates

It does not generate `.resx` resources, resource strings, or runtime config blobs.

It rewrites committed C# source files that already act as the static fallback model lists:
- [OpenAITextModelDefaults.cs](/C:/Dante/_dalamud/Echoglossian/Translators/OpenAI/OpenAITextModelDefaults.cs:1)
- [OpenRouterTextModelDefaults.cs](/C:/Dante/_dalamud/Echoglossian/Translators/OpenRouter/OpenRouterTextModelDefaults.cs:1)
- [GeminiTextModelDefaults.cs](/C:/Dante/_dalamud/Echoglossian/Translators/Gemini/GeminiTextModelDefaults.cs:1)
- [DeepSeekTextModelDefaults.cs](/C:/Dante/_dalamud/Echoglossian/Translators/DeepSeek/DeepSeekTextModelDefaults.cs:1)
- optionally [OllamaTextModelDefaults.cs](/C:/Dante/_dalamud/Echoglossian/Translators/Ollama/OllamaTextModelDefaults.cs:1)
- optionally [LmStudioTextModelDefaults.cs](/C:/Dante/_dalamud/Echoglossian/Translators/LmStudio/LmStudioTextModelDefaults.cs:1)

Those files are the reviewable, committable output.

Remote engines refreshed from their own APIs:
- `OpenAI`
- `OpenRouter`
- `Gemini`
- `DeepSeek`

Optional local engines:
- `Ollama`
- `LmStudio`

Environment variables the script understands:
- `OPENAI_API_KEY` or `EGLO_OPENAI_API_KEY`
- `OPENROUTER_API_KEY` or `EGLO_OPENROUTER_API_KEY`
- `GEMINI_API_KEY`, `GOOGLE_API_KEY`, or `EGLO_GEMINI_API_KEY`
- `DEEPSEEK_API_KEY` or `EGLO_DEEPSEEK_API_KEY`
- `LLMINDEX_API_KEY` or `EGLO_LLMINDEX_API_KEY`
- `EGLO_OPENROUTER_BASE_URL`
- `EGLO_DEEPSEEK_BASE_URL`
- `EGLO_OLLAMA_BASE_URL`
- `EGLO_LMSTUDIO_BASE_URL`
- `EGLO_LMSTUDIO_API_KEY`
- `EGLO_LLMINDEX_BASE_URL`

## Why The Script Uses Provider APIs For The Actual Rewrite

The committed defaults need canonical provider ids. In practice, `llmindex` is useful as an external catalog, but it is not a safe source of truth for emitted code because some ids are normalized differently from the literal API ids used by the engines.

Examples observed during integration:
- `gpt-4-1` in `llmindex` vs `gpt-4.1` in OpenAI
- `gemini-1-5-pro` in `llmindex` vs `gemini-1.5-pro` in Gemini

Because of that:
- the actual `*TextModelDefaults.cs` refresh uses each provider's own list endpoint
- `llmindex` is optional and only used to export a review snapshot for humans

## Optional LLM Index Snapshot

If you want a cross-provider catalog snapshot before updating defaults, run:

```powershell
.\scripts\update-llm-model-defaults.ps1 -ExportLlmIndexSnapshot
```

That writes an ignored artifact by default:
- `artifacts/llmindex-model-catalog.json`

The snapshot is advisory only. It is meant to help manual review and spotting newly published families, not to rewrite the committed defaults directly.

## Typical Workflow

1. Optionally export the advisory `llmindex` snapshot.
2. Run the provider-backed refresh script for the engines you want.
3. Review the diff in the rewritten `*TextModelDefaults.cs` files.
4. Validate locally with build and tests.
5. Commit the refreshed defaults if the diff looks correct.

## Basic Usage

Refresh the default remote engines:

```powershell
.\scripts\update-llm-model-defaults.ps1
```

Refresh only specific engines:

```powershell
.\scripts\update-llm-model-defaults.ps1 -Engines OpenAI,Gemini
```

Include local OpenAI-compatible runtimes too:

```powershell
.\scripts\update-llm-model-defaults.ps1 -IncludeLocal
```

Preview what would be rewritten without touching files:

```powershell
.\scripts\update-llm-model-defaults.ps1 -DryRun
```

Export only the advisory catalog snapshot and skip the engine rewrite:

```powershell
.\scripts\update-llm-model-defaults.ps1 -ExportLlmIndexSnapshot -Engines ''
```

If you want the snapshot in a different location:

```powershell
.\scripts\update-llm-model-defaults.ps1 -ExportLlmIndexSnapshot -LlmIndexSnapshotPath .\artifacts\my-model-catalog.json -Engines ''
```

## Authentication

You can pass keys as parameters, but the intended workflow is environment variables so the command line stays clean.

Examples:

```powershell
$env:EGLO_OPENAI_API_KEY = '...'
$env:EGLO_OPENROUTER_API_KEY = '...'
$env:EGLO_GEMINI_API_KEY = '...'
$env:EGLO_DEEPSEEK_API_KEY = '...'
$env:EGLO_LLMINDEX_API_KEY = '...'
.\scripts\update-llm-model-defaults.ps1 -ExportLlmIndexSnapshot
```

## Examples

```powershell
.\scripts\update-llm-model-defaults.ps1
.\scripts\update-llm-model-defaults.ps1 -IncludeLocal
.\scripts\update-llm-model-defaults.ps1 -Engines OpenAI,Gemini -DryRun
.\scripts\update-llm-model-defaults.ps1 -ExportLlmIndexSnapshot -DryRun
```

Recommended usage:
1. Optionally export the `llmindex` snapshot.
2. Run the provider-backed refresh.
3. Review the diff in the generated `*TextModelDefaults.cs` files.
4. Build and test locally.
5. Commit the refreshed defaults if the diff looks correct.
