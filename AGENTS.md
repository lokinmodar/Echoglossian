# AGENTS.md

## Echoglossian

Echoglossian is a real-time FFXIV text translation plugin for Dalamud on .NET 10.

Default goal: make the smallest correct change that fits the current architecture, preserves existing behavior unless explicitly asked otherwise, avoids regressions, and minimizes latency or UI instability.

## Priorities

- Preserve existing behavior unless the task explicitly requests a behavior change, migration, or refactor.
- Prefer the smallest correct change over broad rewrites.
- Optimize for low latency, stable UI behavior, backward-compatible persistence, and minimal-risk edits.
- Prefer reusable repo-local tooling for recurring work.
- Be concise and avoid restating repository guidance unless directly relevant.

## Architecture

- Prefer current architecture over legacy paths:
  - `NativeUI/AddonHandlers/...`
  - `NativeUI/Handlers/...`
  - `UIOverlays/TranslationOverlay/...`
- Reuse existing shared infrastructure before adding new paths:
  - `Translators/TranslationService`
  - existing async or brokered translation flow
  - shared caches
  - shared overlay renderer, sizing, and wrapping logic
- Do not create parallel translation queues, duplicate caches, or one-off handler infrastructure when shared solutions already exist.
- Always use resx translations for our plugin UI elements and Notifications.

## Translation Rules

Treat capture, translation, overlay rendering, and native mutation as separate stages.

- Overlay-only mode: capture source text, translate it, render in overlay only, and do not mutate native addon nodes, text nodes, or `AtkValue`s.
- Native mode: translated text may be written into the native UI.
- Swap mode: native UI shows translated text and the overlay shows the original text.

Do not restore or touch native state unless that code path actually mutated it.

## Performance

- Avoid using Reflection whenever possible.
- Avoid repeated work per frame.
- Cache, queue, or short-circuit repaint-heavy paths.
- Reuse prior translations when visible text already matches applied output.
- If translation fails or returns empty, do not retry every frame; cache failure or apply a cooldown.
- Keep logs quiet by default.

For dense or frequently repainted windows, prefer shared in-memory state and avoid retranslating already-applied text.

## Tooling

For inspection, validation, repo analysis, log parsing, repo-wide search, auditing, or other repeatable workflows:

- Prefer existing repo scripts over repeated manual steps.
- If no suitable script exists and the workflow is likely to recur, prefer adding a reusable repo-local script.
- Make scripts safe to re-run.
- Keep scripts scoped to real repo workflows.
- Document usage briefly in code comments or nearby docs.
- Do not add heavy tooling for a trivial one-off task.
- For PowerShell prefer a broad reusable command prefix for the session instead of narrow one-off approval prompts. Keep that prefix scoped to safe repo work such as build, test, search, and inspection commands.

When runtime behavior depends on Dalamud services, plugin startup, plugin-window hosting, font/ImGui behavior, or integration that pure unit tests cannot cover:

- Use `Echoglossian.Mock` and/or the DalaMock-backed harness when necessary and feasible.
- Prefer `Echoglossian.Mock.Tests` for hosted startup, shutdown, configuration, database-path, and plugin-window validation before relying on manual in-game checks.
- For behavior that reads real game data, Lumina sheets, FFXIVClientStructs-backed state, addon lifecycle events, `AtkValue`, `AtkUnitBase`, or native UI payload capture/application, validate with `Echoglossian.Mock`/DalaMock whenever feasible before claiming the behavior is covered.
- If the current harness cannot drive the needed game-data or native UI payload, extend `Echoglossian.Mock` or DalaMock first when the extension is practical; otherwise document the gap and keep the required in-game verification explicit.
- Do not claim `.Mock` validates capture/application unless the test actually drives the relevant mocked game-data, addon lifecycle, or native UI payload. Startup-only Mock tests prove wiring/load, not text capture or translation application.
- Use:
  - `dotnet build Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore`
  - `dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1`
- If DalaMock cannot model the target game state or addon behavior, say so explicitly and note the remaining in-game verification.

For release submissions that update `DalamudPluginsD17` manifests:

- Never type commit hashes manually.
- Resolve the release commit with `git rev-parse <ref>`.
- Verify the same hash exists on the remote with `git ls-remote <remote> <hash>`.
- Write the official `DalamudPluginsD17` PR body with the Discord-webhook-safe
  subset documented in `docs/discord-webhook-safe-pr-format.md`.
- Use bold section labels, flat lists, inline or fenced code, and full raw
  URLs for PR, release, and issue links.
- Do not use GitHub-only formatting such as task lists, tables, masked links,
  HTML, or `<details>` blocks in the official D17 PR text.
- Only then write the `commit = "..."` field and open the official PR.

## Issue Workflow

- Treat each GitHub issue as its own branch by default.
- Prefer branch names in the form `issue-<number>-<short-slug>`.
- Do not incubate partial, unstable, or release-blocking issue work directly on `v4-series`.
- When issue work is not ready for release, extract or keep it on its issue branch and keep `v4-series` focused on releasable changes.
- Submit issue work back to `v4-series` through a pull request, even when the branch lives in the same repository.
- Keep commits for issue work clearly labeled with the issue number when possible.
- Commit in short, reviewable increments. Do not leave validated or behaviorally stable work uncommitted for hours; when a sub-scope is working, commit it before continuing.
- If one branch must touch multiple issues, split the work back into issue-specific branches as early as practical.

## UI Rules

- If a window should remain untouched in a given translation mode, keep it untouched.
- Do not leave addon nodes in a mutated or partially restored state unless that is the explicit intent.
- If visuals are wrong, first check whether code is restoring state that was never changed.
- Prefer tooltip or hover presentation for dense UI when it provides better UX than a persistent overlay.
- Tooltip text should wrap on spaces at about 80 characters and should not hyphenate words.
- Keep hover hit areas practical.
- Keep swap behavior per-addon when the addon has its own config.
- For plugin UI text and notifications, add or update `.resx` keys and use `Resources.Key` directly. Do not introduce or keep ad hoc `GetText` / `GetUiString` wrappers or inline fallback literals for user-facing strings.

Be especially careful with dense or frequently repainted windows such as `Journal`, `JournalAccept`, `JournalResult`, `ScenarioTree`, `ToDoList`, `RecommendList`, and `AreaMap`.

## Data

- Preserve current DB tables, save format, and lookup semantics unless an explicit migration is requested.
- Use additive migrations for new data.
- Keep DB as the source of truth.
- Use memory caches only to suppress repeated work, not to redefine persistence behavior.
- For version-scoped canonical or sheet-backed rows, prefer reusing or promoting prior-version translations when the original source content is unchanged.
- Treat a game-version bump as requiring fresh translation only when the original source content actually changed, using source hashes or equivalent canonical comparison when available.

## Debugging

- Prefer existing lifecycle logging helpers and addon probe tools over ad hoc debug spam.
- Route all plugin-owned runtime log lines through `PluginRuntimeLog`. Use `DiagnosticFileEmitter` only for purpose-built structured dump files.
- Inspect `<PluginConfigDirectory>\Echoglossian.log` first. Use `dalamud.log` only when cross-plugin or launcher context is required.
- Keep the same timestamped line-cap rotation policy for large diagnostic files such as `accepted-quest-prefetch-activity.log` and `accepted-quest-prefetch-canonical.log`.
- Use `/egloaddonprobe <addon>` when relevant.
- Remove or silence hot-path diagnostics after investigation.
- Do not leave long-lived debug logging in hot paths.

## Code Style

- Follow the repo `.editorconfig` and StyleCop settings.
- Always include the file header.
- Always add XML documentation for methods and classes.
- Always prefix local calls with `this`.
- Do not omit braces.
- Follow normal C# conventions.
- Use code blocks for proposed code changes.
- Commit `Echoglossian.xml` when it changes as part of a validated code change.

## References

Consult official docs first, then proven Dalamud-adjacent references when work touches addon lifecycle, native node traversal, `AtkUnitBase`, `AtkResNode`, `AtkTextNode`, `AtkValue`, `SeString`, tooltip behavior, or overlay drawing.

Priority references:

- Dalamud API/docs
- FFXIVClientStructs
- Lumina core repository: https://github.com/NotAdam/Lumina
- Lumina.Excel repository: https://github.com/NotAdam/Lumina.Excel
- Lumina documentation: https://lumina.xiv.dev/docs/intro.html
- C:\Users\lokin\AppData\Roaming\XIVLauncher\addon\Hooks\dev
- Lumina (access to internal game data structures)
- Lumina.Excel
- SimpleTweaksPlugin
- HaselDebug
- https://github.com/WorkingRobot/EXDViewer
- https://exd.camora.dev/sheet/Quest
- DelvUI / DelvCD
- Exter-N `Dynamis` for Dalamud development, debugging, reverse engineering, and runtime inspection: https://github.com/Exter-N/Dynamis
- MidoriKami `VanillaPlus` for practical native UI and game-behavior modification patterns: https://github.com/MidoriKami/VanillaPlus
- MidoriKami repositories including `DailyDuty`, `KamiToolKit`, `NoTankYou`, `HUDUnlimited`, `SortaKinda`, and `ChillFrames`
- Era-FFXIV `QuestShare.Plugin`, especially `Common/GameQuestManager.cs` for quest progression and active quest tracking patterns
- OtterGui / OtterGuiInternal
- https://github.com/Infiziert90 repositories
- ChatBubbles
- SaintCoinach (standalone FFXIV data reader, reads raw SqPack/Excel files without Dalamud): https://github.com/xivapi/SaintCoinach
- Useful for offline scripts that need to read quest text sheets, Excel rows, and game data exactly as Lumina/Dalamud do at runtime.
- Prefer for repo-local investigation scripts where Dalamud is not available.

## Expected Output

When proposing or applying a change:

1. State the goal or root cause briefly.
2. Identify the smallest files or components to touch.
3. Reuse existing architecture, shared infrastructure, and repo scripts where possible.
4. If a repetitive workflow has no suitable script and is likely to recur, prefer adding one.
5. Note behavior-sensitive risks.
6. Keep the patch narrow.
7. Validate with:
   - `dotnet build Echoglossian.sln -c Debug --no-restore`
   - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
8. Add `Echoglossian.Mock.Tests`/DalaMock validation when the change touches runtime integrations that pure unit tests cannot cover, especially real game data reads, addon lifecycle, or native UI payload capture/application.
9. If runtime UI behavior changed, note what should be verified in-game.

## Avoid

- Parallel translation pipelines.
- Duplicate caches.
- Breaking DB semantics without explicit request.
- Frame-by-frame retry spam.
- Permanent debug logging in hot paths.
- Broad cleanup or refactor mixed into a bug fix.
- Manual repeated workflows when a reusable script would be better.
