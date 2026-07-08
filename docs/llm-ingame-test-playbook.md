# LLM In-Game Test Playbook

This document is the operator playbook for testing Echoglossian's LLM
translation rework in-game on the `llm-translation-rework` branch.

## Goal

Run in-game validation in a controlled order:

1. keep a restorable backup of the active plugin config
2. enable one LLM engine at a time
3. prove baseline translation works before enabling optional features
4. use the Translator Debugger to verify readiness, live requests, failures,
   latency, and dialogue-session behavior
5. expand from smoke tests into a deeper per-surface and per-engine matrix

## Current Local State On This Machine

As of 2026-07-08, the active plugin config on this machine is:

- config path:
  `C:\Users\lokin\AppData\Roaming\XIVLauncher\pluginConfigs\Echoglossian.json`
- backup created:
  `C:\Users\lokin\AppData\Roaming\XIVLauncher\pluginConfigs\Echoglossian.json.bak-20260708-125000`
- selected engine changed from `Google` to `ChatGPT`
- selected engine fields now point to:
  - `"ChosenTransEngine": 2`
  - `"ChosenTransEngineKey": "ChatGPT"`
  - `"ChatGPTBaseUrl": "https://api.openai.com/v1"`
  - `"OpenAILlmModel": "gpt-4o-mini"`
  - `"UseLiveOpenAIModelList": false`
- no OpenAI key has been inserted yet

Important:

- because the config was edited outside the running plugin, reload Echoglossian
  from Dalamud or restart the game before trusting the active runtime state
- do not bulk-edit this file again while the plugin is actively saving config
  unless you plan to reload immediately afterward

## Do I Need An API Key For Every Engine?

No.

You only need credentials for the engine you are testing right now.

Recommended order:

1. `ChatGPT`
2. `OpenRouter`
3. `DeepSeek`
4. `Claude`
5. `Gemini`
6. `LM Studio` or `Ollama` for local no-cloud coverage

Engine credential requirements:

| Engine | API key required | Notes |
| --- | --- | --- |
| ChatGPT / OpenAI | Yes | Best first baseline for the OpenAI-family path |
| Claude / Anthropic | Yes | Separate provider and endpoint family |
| DeepSeek | Yes | OpenAI-compatible HTTP path |
| Gemini | Yes | Google AI Studio key required |
| OpenRouter | Yes | Single key, optional per-key credit limits |
| LM Studio | Usually no | Local server by default; optional auth only if you configured it |
| Ollama | No | Local server only |

## What You Need Before Testing In-Game

1. The game must be loading the branch build you actually want to test, not an
   older stable build.
2. Echoglossian must be reloaded after any external config-file edit.
3. Pick one provider only for the first smoke test.
4. Have billing, credits, or provider access ready before testing.
5. Keep live model list toggles off for the first successful request.
6. Open the Translator Debugger with `/eglotranslatordebugger` during testing.
7. Prepare easy test surfaces:
   - `Talk`
   - `BattleTalk`
   - `MiniTalk`
   - `Journal` / `JournalDetail`
   - wide/error/area/class/quest toast surfaces

## Recommended First Smoke Test

Start with the official `ChatGPT` engine.

Why:

- it is already selected in the local config on this machine
- it exercises the OpenAI-family provider path directly
- it is the cleanest baseline before testing proxy or aggregator providers

Use these values first:

- engine: `ChatGPT`
- base URL: `https://api.openai.com/v1`
- model: `gpt-4o-mini`
- live model list: `off`
- prompt: keep the current default prompt
- temperature: keep `0.1`

Do not add extra variables yet:

- do not switch to custom OpenAI-compatible mode first
- do not turn on live model refresh first
- do not change prompt text first

## How To Create API Keys

### OpenAI / ChatGPT

Official sources:

- API key help: <https://help.openai.com/en/articles/4936850-where-do-i-find-my-openai-api-key>
- project and API-key management: <https://help.openai.com/en/articles/9186755-managing-your-work-in-the-api-platform-with-projects>
- API keys page: <https://platform.openai.com/api-keys>

Process:

1. Sign in to the OpenAI API Platform.
2. Pick or create the project you want Echoglossian to bill against.
3. Open the project's `API Keys` page.
4. Click `Create new secret key`.
5. Save the key immediately. OpenAI only shows the full secret when it is
   created.
6. If calls later fail for budget or model-access reasons, check project
   billing, limits, and model usage on the project settings pages.

### Claude / Anthropic

Official sources:

- get started: <https://platform.claude.com/docs/en/get-started>
- keys page: <https://console.anthropic.com/settings/keys>

Process:

1. Sign in to Anthropic Console.
2. Open `Settings` -> `Keys`.
3. Create a new API key.
4. Save it immediately.
5. Confirm the account has usable billing or credits before testing.

### DeepSeek

Official sources:

- API docs: <https://api-docs.deepseek.com/api/deepseek-api>
- key page: <https://platform.deepseek.com/api_keys>

Process:

1. Sign in to the DeepSeek Platform.
2. Open the API keys page.
3. Create a new key.
4. Save it immediately.
5. Add credits if your account requires them before inference will succeed.

### Gemini

Official sources:

- API keys doc: <https://ai.google.dev/gemini-api/docs/api-key>
- getting started: <https://ai.google.dev/gemini-api/docs/get-started>
- AI Studio: <https://aistudio.google.com/>

Process:

1. Sign in to Google AI Studio.
2. Open the API keys page in AI Studio.
3. If you are a new user, AI Studio can create a default project and key for
   you automatically.
4. If you need a new key, click `Create API key`.
5. Save the key.
6. If you need higher limits, enable billing in the linked Google Cloud
   project.

Important date-specific note:

- Google's Gemini docs say that new AI Studio keys now default to auth keys
  and that unrestricted standard keys began being rejected on 2026-06-19
- the same docs say standard keys are planned to stop working entirely in
  September 2026

That means:

- if Gemini fails while the key looks valid, confirm the key type and
  restrictions in AI Studio before debugging Echoglossian

### OpenRouter

Official sources:

- authentication doc: <https://openrouter.ai/docs/api/reference/authentication>
- quickstart: <https://openrouter.ai/docs/quickstart>

Process:

1. Sign in to OpenRouter.
2. Open the API keys page from your account.
3. Create a new API key.
4. Save it immediately. OpenRouter keys start with `sk-or-...`.
5. Optionally set a per-key credit limit before using the key in Echoglossian.
6. Make sure the account has enough credits or an active billing path for the
   models you plan to use.

### LM Studio

Official sources:

- local server overview: <https://lmstudio.ai/docs/developer/core/server>
- OpenAI-compatible endpoints: <https://lmstudio.ai/docs/developer/openai-compat>

Process:

1. Install LM Studio.
2. Download a model locally.
3. Start the local server from the Developer tab, or run `lms server start`.
4. Keep the Echoglossian base URL at the LM Studio server, usually
   `http://localhost:1234/v1`.
5. No API key is required unless you intentionally enabled authentication on
   your own server path.

### Ollama

Official sources:

- Windows download: <https://ollama.com/download/windows>
- product home: <https://ollama.com/>

Process:

1. Install Ollama on Windows.
2. Pull at least one model.
3. Make sure the local Ollama API is running.
4. Use the default local endpoint in Echoglossian.
5. No API key is required for the default local setup.

## How To Finish The First ChatGPT Setup

Option A, recommended:

1. Open Echoglossian's config UI in-game.
2. Go to the translation-engine section.
3. Confirm the selected engine is `ChatGPT`.
4. Paste the OpenAI API key into the ChatGPT API key field.
5. Keep `https://api.openai.com/v1` as the base URL.
6. Keep `gpt-4o-mini` as the model.
7. Keep live model list disabled for the first pass.
8. Save config.

Option B, direct file edit:

1. Close the game or unload the plugin first.
2. Edit:
   `C:\Users\lokin\AppData\Roaming\XIVLauncher\pluginConfigs\Echoglossian.json`
3. Set the `ChatGptApiKey` value.
4. Save the file.
5. Reload Echoglossian or restart the game.

## In-Game Smoke Test Script

1. Reload the plugin or restart the game.
2. Open `/eglotranslatordebugger`.
3. Confirm the debugger shows the OpenAI-family provider as ready.
4. Trigger one `Talk` line from an NPC.
5. Verify:
   - live request count increments
   - no missing-key or endpoint-readiness failure is shown
   - translated text appears in the configured surface mode
6. Trigger one `BattleTalk` line.
7. Trigger one `MiniTalk` line.
8. Open one quest surface such as `Journal` or `JournalDetail`.
9. Trigger one toast surface if practical.
10. Use `Clear Dialogue Sessions` before repeating dialogue-family tests.
11. Use `Retranslate Visible Dialogue And Persist` on a visible `Talk` or
    `BattleTalk` line to validate the DB-backed refresh path.

## Deep Test Matrix

After the first smoke test passes, test in this order.

### A. Readiness And Activation

- missing key blocks translation cleanly
- valid key makes the engine ready
- wrong base URL shows a useful failure
- wrong model shows a useful failure

### B. Dialogue Family

- `Talk` single-turn translation
- `Talk` multi-turn contextual translation
- `BattleTalk` single-turn translation
- `BattleTalk` multi-turn contextual translation
- `Clear Dialogue Sessions` really resets context behavior

### C. Structured Response Paths

- one normal translation with the selected default model
- one translation after reloading the plugin
- one translation after changing the model
- one translation after changing only the API key

### D. Surface Coverage

- `MiniTalk`
- `Journal`
- `JournalDetail`
- `JournalAccept`
- `JournalResult`
- `ScenarioTree`
- `ToDoList`
- `RecommendList`
- `AreaMap`
- toasts

### E. Presentation Modes

- native mode
- tooltip / overlay mode
- swap mode where supported

### F. Live Model Refresh

Only after the baseline is good:

1. enable the live model list for the engine
2. reload or reopen the config UI
3. verify the live refresh completes
4. confirm model selection stays stable
5. confirm changing only the API key still causes refresh detection without
   exposing raw key material in helper state

## How To Restore The Previous Config

To revert to the exact pre-change config snapshot created on this machine:

```powershell
Copy-Item `
  -LiteralPath 'C:\Users\lokin\AppData\Roaming\XIVLauncher\pluginConfigs\Echoglossian.json.bak-20260708-125000' `
  -Destination 'C:\Users\lokin\AppData\Roaming\XIVLauncher\pluginConfigs\Echoglossian.json' `
  -Force
```

Then reload Echoglossian or restart the game.

## Practical Recommendation

Do not try to validate every engine at once.

Use this sequence:

1. prove `ChatGPT` works
2. prove one OpenAI-compatible provider works, preferably `OpenRouter` or
   `DeepSeek`
3. prove `Claude`
4. prove `Gemini`
5. prove one local provider, `LM Studio` or `Ollama`

That gives coverage across:

- first-party OpenAI
- OpenAI-compatible routing
- Anthropic-family behavior
- Google-family auth behavior
- local no-key inference paths
