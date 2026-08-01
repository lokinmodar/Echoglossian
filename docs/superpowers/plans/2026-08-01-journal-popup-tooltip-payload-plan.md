# Journal popup tooltip and payload fix Implementation Plan

> For agentic workers: REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Make JournalAccept and JournalResult reliably expose translated title/body tooltips, preserve readable SeString payloads, and resolve JournalResult translations from canonical QuestPlate data before fallback translation.

**Architecture:** Keep capture, translation, tooltip presentation, and native mutation as separate stages. Extend the existing readable-payload helper only for safe formatting-wrapper matching, and extract the existing JournalAccept body-hitbox logic into a shared geometry helper used by both popup handlers. Keep the handler-owned text node for payload capture and swap, while registering the tooltip with a separately selected structural body region.

**Tech Stack:** C#/.NET 10, Dalamud addon lifecycle APIs, FFXIVClientStructs AtkResNode/AtkTextNode, Lumina ReadOnlySeString/SeStringBuilder, xUnit, PowerShell, and the existing Echoglossian translation/database infrastructure.

## Global Constraints

- Preserve native UI, structured tooltip/overlay, and swap modes for both handlers.
- Normalize only known outer presentation wrappers such as balanced ** markers when matching captured text to an existing payload.
- Preserve payload macros and control data; use plain-text fallback only when the payload cannot be validated.
- Select body hover geometry from live node relationships and bounds; do not use synthetic node ids or fixed popup coordinates.
- Resolve JournalResult canonical data by quest id first, then title/name, then existing realtime and persistence fallbacks.
- Do not add a database schema migration, duplicate translation queue, duplicate cache, or changes to unrelated addon handlers.
- Write and run failing tests before each implementation change.
- Keep commits short and do not stage the existing untracked handoff/plan documents except the plan file itself.

---

## File map

### New files

- NativeUI/Helpers/PopupBodyHoverGeometryHelper.cs — pure candidate model and deterministic selector for practical body-region geometry.
- Echoglossian.Tests/PopupBodyHoverGeometryTests.cs — allocation-free tests for candidate selection and rejection.

### Modified files

- NativeUI/Helpers/ReadableSeStringPayloadHelper.cs — safe wrapper-aware payload matching.
- NativeUI/AddonHandlers/Quest/QuestAddonHandlerBase.cs — shared native traversal and bounds construction around the pure geometry selector.
- NativeUI/AddonHandlers/Quest/JournalAcceptHandler.cs — use shared body bounds and retain the live body text node for capture/swap.
- NativeUI/AddonHandlers/Quest/JournalResultHandler.cs — use shared body bounds and enforce canonical lookup precedence.
- Echoglossian.Tests/QuestReadableTextMatchTests.cs — payload regression coverage and removal/replacement of handler-local geometry tests.
- Echoglossian.Tests/QuestAddonHandlerLifecycleTests.cs — lookup and shared-registration contracts.

No changes are planned for HoverTooltipManager, Journal, JournalDetail, ScenarioTree, ToDoList, RecommendList, or the database model.

## Task 1: Regress the formatted SeString payload failure

**Files:**

- Modify: Echoglossian.Tests/QuestReadableTextMatchTests.cs near ProjectReadablePayloadBytes_PreservesRichFormattingWhenReplacingBodyText.
- Modify: NativeUI/Helpers/ReadableSeStringPayloadHelper.cs in PayloadMatches and its private normalization helpers.

**Interfaces:**

- Consumes: ReadOnlySeString.ExtractText(), existing NormalizeReadableText, and existing ProjectReadablePayloadBytes.
- Produces: PayloadMatches and RetainMatchingPayload accept a captured source such as ** Quest Sync** when the payload extracts  Quest Sync, while unrelated text still fails.

- [ ] Step 1: Write the failing test. Add a test that builds a rich payload containing Quest Sync without the outer setup wrappers, calls ProjectReadablePayloadBytes with **Quest Sync**, and asserts:

~~~csharp
Assert.NotNull(projectedPayload);
Assert.Equal(translatedText, new ReadOnlySeString(projectedPayload).ExtractText());
Assert.NotEqual(
    ReadOnlySeString.FromText(translatedText).Data.ToArray(),
    projectedPayload);
~~~

Use the same SeStringBuilder.AddUiForeground(...).AddText(...).AddUiForegroundOff() construction already used by the neighboring rich-payload test.

- [ ] Step 2: Run the focused test and verify it fails.

Run from the worktree:

~~~powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~ProjectReadablePayloadBytes_RetainsRichPayloadWhenCapturedTextHasOuterWrappers"
~~~

Expected result: the new assertion fails because RetainMatchingPayload currently compares the literal outer ** markers with the extracted payload text and returns null.

- [ ] Step 3: Implement the smallest matching change. In ReadableSeStringPayloadHelper, keep NormalizeReadableText unchanged for general node matching. Add a private NormalizePayloadComparisonText(string text) that first calls NormalizeReadableText, then removes only balanced outer ** pairs while the resulting string still has content. Use this helper only for the two values compared inside PayloadMatches.

The helper must not remove unmatched markers, markers in the middle of a string, payload bytes, or arbitrary punctuation. ProjectReadablePayloadBytes must continue to use the original retained payload and existing LuminaSeStringBuilder.ReplaceText path.

- [ ] Step 4: Run the focused payload tests and verify they pass.

~~~powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~ReadableSeStringPayloadHelper|FullyQualifiedName~ProjectReadablePayloadBytes"
~~~

Expected result: the new wrapper regression and all existing payload tests pass.

- [ ] Step 5: Commit the payload fix.

~~~powershell
git add -- NativeUI/Helpers/ReadableSeStringPayloadHelper.cs Echoglossian.Tests/QuestReadableTextMatchTests.cs
git commit -m "fix(quest): retain payloads with captured wrappers"
~~~

## Task 2: Add the pure popup-body geometry selector

**Files:**

- Create: NativeUI/Helpers/PopupBodyHoverGeometryHelper.cs.
- Create: Echoglossian.Tests/PopupBodyHoverGeometryTests.cs.

**Interfaces:**

- Consumes: candidate snapshots produced by native node traversal.
- Produces: PopupBodyHoverGeometryHelper.SelectCandidateIndex(float textWidth, float textHeight, IReadOnlyList<PopupBodyHoverCandidate> candidates), returning a zero-based candidate index or -1.

Define the internal immutable candidate shape as:

~~~csharp
internal readonly record struct PopupBodyHoverCandidate(
    float Width,
    float Height,
    bool IsVisible,
    bool ContainsText,
    bool IsCollision,
    bool IsComponent,
    int DistanceFromText);
~~~

The selector must reject non-positive dimensions, invisible candidates, candidates that do not contain/intersect the body text rectangle, candidates smaller than the text node, and candidates that are not materially larger than the text node using the existing 12-pixel width/18-pixel height thresholds. Among valid candidates, rank collision candidates ahead of component candidates, then choose the smallest area and nearest distance. This prevents an addon-sized ancestor from winning over a body collision region.

- [ ] Step 1: Write the failing selector tests. Add tests for:

  - a text-sized component, a visible body collision, and an addon-sized ancestor, asserting the collision index wins;
  - invisible, zero-sized, non-containing, and non-materially-larger candidates returning -1;
  - a component fallback winning when no valid collision candidate exists.

Use only PopupBodyHoverCandidate values; do not allocate or dereference AtkResNode in these tests.

- [ ] Step 2: Run the new tests and verify they fail to compile.

~~~powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~PopupBodyHoverGeometryTests"
~~~

Expected result: compilation fails because the new helper and candidate type do not exist.

- [ ] Step 3: Implement the pure selector. Add XML documentation, constants for the existing material width/height thresholds, candidate validation, collision/component ranking, area comparison, and stable distance tie-breaking. Return -1 for an empty or wholly invalid list.

- [ ] Step 4: Run the selector tests and verify they pass.

~~~powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~PopupBodyHoverGeometryTests"
~~~

- [ ] Step 5: Commit the pure geometry selector.

~~~powershell
git add -- NativeUI/Helpers/PopupBodyHoverGeometryHelper.cs Echoglossian.Tests/PopupBodyHoverGeometryTests.cs
git commit -m "test(quest): define popup body hover geometry"
~~~

## Task 3: Share native body bounds between JournalAccept and JournalResult

**Files:**

- Modify: NativeUI/AddonHandlers/Quest/QuestAddonHandlerBase.cs around TryFindPopupSectionBodyTextNodeByHeadingTextId and the existing text-node matching helpers.
- Modify: NativeUI/AddonHandlers/Quest/JournalAcceptHandler.cs around RegisterJournalAcceptHoverTooltip, TryFindJournalAcceptMessageNode, and the existing private bounds methods.
- Modify: NativeUI/AddonHandlers/Quest/JournalResultHandler.cs around RegisterJournalResultHoverTooltip and TryFindJournalResultMessageNode.
- Modify: Echoglossian.Tests/QuestReadableTextMatchTests.cs and Echoglossian.Tests/QuestAddonHandlerLifecycleTests.cs.

**Interfaces:**

- Consumes: AtkTextNode* body node, optional structural node returned by the heading resolver, and PopupBodyHoverGeometryHelper.SelectCandidateIndex.
- Produces: QuestAddonHandlerBase.TryBuildPopupBodyHoverBounds(AtkTextNode* textNode, AtkResNode* preferredHoverNode, out Vector2 topLeft, out Vector2 bottomRight) for both handlers.

- [ ] Step 1: Write the wiring tests before changing runtime code. Add lifecycle assertions that both RegisterJournalAcceptHoverTooltip and RegisterJournalResultHoverTooltip reference the shared TryBuildPopupBodyHoverBounds method. Add a regression assertion that JournalResult body registration uses the explicit-bounds overload when a structural body candidate is available. Replace the old reflection tests that target JournalAccept-only ancestor methods with direct tests for PopupBodyHoverGeometryHelper.

- [ ] Step 2: Run the focused lifecycle and readable-text tests and verify they fail.

~~~powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~JournalResultHandler|FullyQualifiedName~JournalAccept|FullyQualifiedName~PopupBodyHoverGeometryTests"
~~~

Expected result: the shared bounds method is absent or the JournalResult method does not reference it.

- [ ] Step 3: Implement the shared native bounds method. In QuestAddonHandlerBase, collect the preferred structural node, visible collision siblings associated with its parent, and visible component ancestors up to the addon-section boundary. For each candidate, record visibility, screen-rectangle containment/intersection with the live body node, collision/component classification, dimensions, and distance. Map the selected pure-candidate index back to its native pointer and union its screen bounds with the live text-node bounds, retaining the existing small padding used by the JournalAccept hitbox.

Do not use node ids or fixed popup coordinates. If no candidate is selected, return false so the caller can register the text-node fallback. Keep the heading-based resolver responsible for locating the body text; keep this method responsible only for geometry.

- [ ] Step 4: Route JournalAccept through the shared method. Update the message-node resolver to return the body text node, the optional preferred structural node, and the original hover text. Replace the private TryBuildJournalAcceptMessageHoverBounds, ancestor scanning, parent-origin reconstruction, and parent-size predicate with TryBuildPopupBodyHoverBounds. Keep the body text node as the node passed to the rich original capture request and native write/restore paths.

- [ ] Step 5: Route JournalResult through the same method. Update TryFindJournalResultMessageNode to return the body text node and optional preferred structural node. In RegisterJournalResultHoverTooltip, call TryBuildPopupBodyHoverBounds and use the explicit bounds overload when it succeeds; otherwise use the existing text-node overload. Update visible payload capture, native application, and restoration call sites to ignore the structural output while continuing to use the same live body text node.

- [ ] Step 6: Run the focused tests and verify they pass.

~~~powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~QuestReadableTextMatchTests|FullyQualifiedName~QuestAddonHandlerLifecycleTests|FullyQualifiedName~PopupBodyHoverGeometryTests"
~~~

- [ ] Step 7: Commit the shared geometry integration.

~~~powershell
git add -- NativeUI/Helpers/PopupBodyHoverGeometryHelper.cs NativeUI/AddonHandlers/Quest/QuestAddonHandlerBase.cs NativeUI/AddonHandlers/Quest/JournalAcceptHandler.cs NativeUI/AddonHandlers/Quest/JournalResultHandler.cs Echoglossian.Tests/QuestReadableTextMatchTests.cs Echoglossian.Tests/QuestAddonHandlerLifecycleTests.cs
git commit -m "fix(quest): share popup body hover bounds"
~~~

## Task 4: Enforce JournalResult canonical lookup precedence

**Files:**

- Modify: NativeUI/AddonHandlers/Quest/JournalResultHandler.cs in FindJournalResultQuestPlate, TryResolveJournalResultTranslation, and TryRefreshJournalResultPendingTranslation.
- Modify: Echoglossian.Tests/QuestAddonHandlerLifecycleTests.cs and its dependency factory.

**Interfaces:**

- Consumes: QuestPopupIdentity's optional quest id, existing FindQuestPlate, FindQuestPlateByName, popup persistence, UI cache, and queued translation delegates.
- Produces: ID lookup with title fallback, and translation resolution ordered as canonical QuestPlate, then existing session/cache/popup/queued fallback paths.

- [ ] Step 1: Write the failing lookup tests. Add a test that configures FindQuestPlate to record an id call and return null, configures FindQuestPlateByName to record a title call and return a known row, invokes FindJournalResultQuestPlate with a quest id, and asserts the known row plus call order id, title. Add a second test that stores a different applied text in QuestUiTranslationCache, supplies a translated QuestPlate, invokes TryResolveJournalResultTranslation, and asserts the canonical translation wins.

Use the existing QuestAddonHandlerDependencies factory, adding optional FindQuestPlate and FindQuestPlateByName delegates so existing tests retain their defaults. Construct the known row with:

~~~csharp
new QuestPlate(
    "Quest title",
    string.Empty,
    "en",
    "Canonical translation",
    string.Empty,
    "42",
    "pt-BR",
    0,
    DateTime.UtcNow,
    DateTime.UtcNow);
~~~

Clear QuestUiTranslationCache in a try/finally around the cache-precedence test.

- [ ] Step 2: Run the focused tests and verify they fail.

~~~powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~JournalResult"
~~~

Expected results: an id lookup that misses does not yet call title lookup, and a previously applied cache value can win over a canonical QuestPlate value.

- [ ] Step 3: Implement ID-then-title lookup. Change FindJournalResultQuestPlate to call FindQuestPlate whenever a non-empty id exists, return that row immediately when found, and otherwise call FindQuestPlateByName. When no id exists, call only the title lookup.

- [ ] Step 4: Implement canonical-first translation resolution. In both TryResolveJournalResultTranslation and TryRefreshJournalResultPendingTranslation, check a complete foundQuestPlate.TranslatedQuestName before QuestUiTranslationCache, current queued/session values, and dedicated popup persistence. Preserve realtime queuing when no complete canonical translation exists, and do not change the complete-body rule used by ResolveJournalResultStoredMessage.

- [ ] Step 5: Run the focused tests and verify they pass.

~~~powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~JournalResult"
~~~

- [ ] Step 6: Commit the lookup fix.

~~~powershell
git add -- NativeUI/AddonHandlers/Quest/JournalResultHandler.cs Echoglossian.Tests/QuestAddonHandlerLifecycleTests.cs
git commit -m "fix(quest): prioritize canonical JournalResult rows"
~~~

## Task 5: Run repository verification and record runtime coverage

**Files:**

- Modify: none unless a test exposes an implementation defect.
- Inspect: docs/superpowers/specs/2026-08-01-journal-popup-tooltip-payload-design.md, the changed handlers, and existing addon probe output.

**Interfaces:**

- Consumes: all committed implementation tasks and the existing in-game probe/log workflow.
- Produces: passing build/tests, a clean tracked diff apart from the known local handoff documents, and a concise list of in-game checks still required.

- [ ] Step 1: Run the standard solution build.

~~~powershell
dotnet build Echoglossian.sln -c Debug --no-restore
~~~

Expected result: success, with only the known Multilingual App Toolkit warning and documented pre-existing warnings if they remain.

- [ ] Step 2: Run the full unit test project serially.

~~~powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
~~~

Expected result: all existing tests plus the new regressions pass.

- [ ] Step 3: Run the Mock/DalaMock project because the change touches addon lifecycle and native UI integration.

~~~powershell
dotnet build Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore
dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1
~~~

If the harness does not model JournalAccept/JournalResult AtkValue or node payloads, state that explicitly; do not claim the Mock project validates native capture/application unless the tests drive those paths.

- [ ] Step 4: Run the locale checker from its project directory.

~~~powershell
Push-Location Echoglossian.Docs
node --test .\scripts\check-locales.test.mjs
Pop-Location
~~~

- [ ] Step 5: Inspect status and the final diff.

~~~powershell
git status --short
git diff origin/feature/issues-230-233-234...HEAD --stat
git diff origin/feature/issues-230-233-234...HEAD --check
~~~

Confirm that only the spec, plan, and intentional implementation/test files are tracked changes. Leave all known handoff and local plan documents untracked.

- [ ] Step 6: Perform the explicit in-game verification handoff. Use the existing /egloaddonprobe JournalAccept and /egloaddonprobe JournalResult workflow and verify title/body hover regions, neighboring controls, repeated refreshes, formatted Quest Sync, native mode, structured tooltip mode, swap mode, and result popups both with and without a reliable quest id. Do not attach a live debugger without the separate executable identity, address, question, authorization, and evidence plan required by the runtime investigation skill.

- [ ] Step 7: Commit any verification-only documentation only if the user explicitly asks for it. Runtime observations belong in the final handoff unless a tracked document update is requested; do not commit logs or generated probe output.

## Self-review checklist

- Spec coverage: payload wrapper matching is Task 1; geometry selection and shared integration are Tasks 2–3; JournalResult id/title/fallback precedence is Task 4; modes and runtime verification are Tasks 3–5.
- Placeholder scan: every task names concrete files, methods, commands, expected outcomes, and commit messages.
- Type consistency: PopupBodyHoverCandidate and SelectCandidateIndex are introduced in Task 2; Task 3 consumes those exact names; TryBuildPopupBodyHoverBounds is introduced in Task 3 and referenced by both handlers/tests in that task.
- Risk boundary: no HoverTooltipManager, database schema, unrelated addon, broad logging, or second translation pipeline is included.
