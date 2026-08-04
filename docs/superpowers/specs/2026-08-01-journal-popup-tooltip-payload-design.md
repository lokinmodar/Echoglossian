# JournalAccept/JournalResult popup tooltip and payload design

Status: approved design, pending implementation

## Goal

Make the `JournalAccept` and `JournalResult` popup flows reliably present the
translated title and body while preserving the readable parts of the original
`SeString` payloads. The native UI, structured tooltip/overlay, and swap modes
must continue to follow the existing addon mode configuration.

## Context and root cause

The current handlers already capture popup text during setup and use the shared
translation broker, cache, and persistence paths. The remaining failures are
narrower:

- The body hover registration is based primarily on a readable text node. The
  addon probes show a larger visible `Collision`/component structure around the
  body, so the text node alone does not provide a reliable hover surface.
- The body node must remain available for readable text and rich swap capture,
  but the tooltip bounds should come from the containing popup structure.
- `ReadableSeStringPayloadHelper` compares the plain captured text with the
  extracted payload text. Setup text can contain formatting wrappers such as
  `**` that are not part of the payload's extracted text. That mismatch causes
  the handler to fall back to writing the formatted string as plain text,
  producing visible markup and losing payload structure.
- `JournalResult` currently allows session/cache and popup fallbacks to precede
  the canonical `QuestPlate` lookup. The required order is canonical quest row
  by quest id, then canonical row by title, then realtime/fallback paths.

The addon probes also show that the body structure differs between popup
variants. This design therefore selects a suitable containing structure from
the live node relationships instead of relying on synthetic node ids or fixed
coordinates.

## Scope

### In scope

- A narrow normalization used only when matching captured readable text to an
  existing `SeString` payload, preserving payload macros and control data.
- A shared popup-body geometry resolver that can select the containing visible
  component/collision structure while retaining the body `AtkTextNode` for
  readable text and swap capture.
- Applying that resolver to `JournalAccept` and `JournalResult`.
- Reordering `JournalResult` lookup to prefer `QuestPlate` by id, then title,
  before realtime and other fallback paths.
- Regression tests for payload matching/projection, geometry selection, and
  lookup precedence.

### Out of scope

- Changes to `Journal`, `JournalDetail`, `ScenarioTree`, `ToDoList`,
  `RecommendList`, or the shared tooltip renderer beyond the smallest helper
  seam required by these two handlers.
- Synthetic node ids, hardcoded popup coordinates, or a broad popup abstraction.
- A database schema change. Existing dedicated popup persistence remains the
  source of truth when no reliable quest id is available.
- Inventing or translating a `JournalResult` body when no complete stored body
  exists. The current setup/title-only behavior remains intact.
- A second translation queue or duplicate cache.

## Design

### 1. Preserve readable `SeString` payloads

Extend the existing `ReadableSeStringPayloadHelper` behavior so payload/text
matching ignores only known presentation wrappers that can be introduced by
the addon capture path, such as balanced outer `**` markers. The comparison
must continue to ignore insignificant whitespace/control presentation details
through the existing normalization, but it must not flatten or rebuild the
payload into plain text.

When the normalized source text matches the payload's extracted text, retain
the original payload and use the existing projection path to replace only the
readable text with the translation. This preserves relevant `SeString`
payloads/macros for native writes and structured tooltip content. If the source
cannot be matched safely, keep the existing conservative plain-text fallback;
never write an unvalidated payload over native UI state.

The helper remains independent of native node traversal. Native application
continues to decide whether to write a payload or plain text, while tooltip
rendering continues to consume the readable projected text.

### 2. Resolve the popup body hover structure

Add a narrowly scoped shared helper near the existing popup section resolver.
Given the body text node and the surrounding addon relationships, it should:

1. identify a visible containing component or collision node associated with the
   popup body;
2. prefer the largest practical body-region bounds that are still specific to
   the body section, rather than the entire addon;
3. reject invisible, zero-sized, unrelated, or title-only candidates; and
4. return the live body text node separately for readable text and swap logic.

The existing heading-based fallback remains useful for locating the body text,
but its structural candidate should be passed through the new selection logic.
The implementation must use node relationships and measured bounds, not fixed
node ids or hardcoded dimensions. The existing tooltip manager's smallest-hit
rectangle behavior remains unchanged.

Both handlers should register the body tooltip with the structural bounds and
the live text node. If no safe structural candidate exists, retain the current
conservative text-node/addon fallback rather than registering an oversized
global hit area.

### 3. JournalAccept behavior

- Keep capturing title and body during setup and using the existing async
  translation/persistence flow.
- Preserve the original title/body payloads whenever they safely match the
  captured readable text, including wrapped setup text.
- Register the title tooltip against the title text node as today.
- Register the body tooltip against the resolved body-region structure while
  retaining the body text node for rich swap capture.
- Keep native mutation, structured tooltip presentation, and swap behavior
  separate and mode-aware. Do not restore native state on paths that did not
  mutate it.
- Keep the current visible-body promotion behavior for richer text when setup
  exposes only the short `Quest Sync` marker.

### 4. JournalResult behavior

Change only the translation resolution precedence:

1. `QuestPlate` lookup by a reliable quest id;
2. `QuestPlate` lookup by title/name when no id match is available;
3. existing dedicated popup/session/cache/broker paths as fallback, including
   realtime translation when no canonical row exists.

The handler must continue to avoid replacing canonical data with an uncertain
popup capture. It should still translate the captured title in realtime and
show a body only when a complete stored body translation is available. Apply
the same structural body hover registration used by `JournalAccept`, while
preserving the existing title and addon-level fallbacks.

## Tests

Tests must be written before the implementation changes and should remain
pure where native allocation is unnecessary:

- A payload regression test proves that a rich payload whose extracted text
  omits outer setup wrappers can still be retained and projected, and does not
  fall back to a literal `**...**` string.
- A geometry/helper test proves that a visible containing collision/component
  candidate is selected over the text-node bounds and that invalid or
  unrelated candidates are rejected. The selector should be exercised without
  unsafe native allocations where possible.
- A `JournalResult` precedence test proves id-based `QuestPlate` lookup wins,
  title-based lookup is next, and realtime/fallback resolution is used only
  when neither canonical lookup succeeds.
- Existing unit, locale, and build tests remain green.

If the current seams do not expose the lookup or geometry decisions cleanly,
add the smallest internal pure selector/decision seam rather than testing
private native memory behavior through reflection.

## Acceptance criteria

- `JournalAccept` title tooltip remains stable across repeated setup/refresh
  cycles.
- `JournalAccept` body tooltip is hoverable over the practical body region,
  not only over the text glyphs, and does not cover unrelated popup controls.
- Formatted strings such as `Quest Sync` retain readable payload structure in
  native and structured tooltip paths when the original payload is available.
- `JournalResult` follows id → title → realtime/fallback precedence and
  supports the same native, structured tooltip, and swap modes.
- No new per-frame translation loop, duplicate cache, broad logging, or
  unrelated addon changes are introduced.
- Build and tests pass using the branch's established commands.

## Risks and in-game verification

The main behavior-sensitive risk is selecting a body collision region that is
too broad or belongs to the wrong popup section. Verify in game with the
existing addon probes and by hovering title, body, and neighboring controls in
both popup variants. Also verify native, overlay/tooltip, and swap modes for
both handlers, including a formatted `Quest Sync` string and a result with and
without a reliable quest id.

No live debugger attachment is part of this design. Static source, existing
logs, addon probes, and the repository's test harness are the permitted
evidence until a separately authorized runtime investigation provides the
required executable identity, address, question, and evidence plan.
