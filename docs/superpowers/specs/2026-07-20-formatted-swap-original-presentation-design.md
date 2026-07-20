# Formatted Swap Original Presentation Design

## Status

Written on 2026-07-20 after discussion. Pending final user review before
implementation planning.

## Summary

This spec defines a global presentation capability for Echoglossian swap mode:
when a translated surface can safely capture the original text as a formatted
game text payload, the plugin should render that original payload with its
formatting in the plugin-owned presentation layer.

This is not an `ActionDetail` / `ItemDetail` feature. It is a shared overlay
and hover-tooltip capability. `ActionDetail` and `ItemDetail` are likely early
beneficiaries because their original game text often contains useful colors and
style payloads, but the rule is surface-agnostic:

- if the active mode is swap
- and the presentation layer is showing original text
- and a safe formatted original payload exists
- render the original with formatting
- otherwise keep the existing plain-string behavior

## Problem

Echoglossian currently separates the translation stages correctly:

- capture
- translation
- overlay or tooltip rendering
- native mutation

The weak point is that plugin overlays and plugin hover tooltips mostly receive
plain strings. Once formatted game text has been flattened into a `string`,
payload information such as emphasis, color, and some inline semantic structure
is no longer available to the ImGui renderer.

The DelvUI PR #1322 is a useful reference because it demonstrates a focused
pattern for drawing formatted Lumina `ReadOnlySeString` content in an ImGui
tooltip through `ImGuiHelpers.SeStringWrapped`. Echoglossian should adapt the
idea as a shared presentation capability rather than copying DelvUI's helper or
scoping the behavior to one surface.

Reference: https://github.com/DelvUI/DelvUI/pull/1322

## Goals

- Add an optional formatted-original payload to the shared presentation path.
- Use that payload in swap mode whenever the plugin-owned presentation layer is
  showing the original text.
- Keep existing string rendering as the fallback for every surface.
- Support both persistent overlays and hover tooltips through one shared model.
- Avoid holding pointers or spans over game-owned text memory past the frame in
  which they were read.
- Keep native UI mutation separate from formatted original rendering.
- Preserve the existing RTL texture path until it has a dedicated rich-span
  renderer.

## Non-Goals

- Do not translate formatted payloads in this slice.
- Do not reinject translated text into the original payload structure.
- Do not change DB persistence schemas for translations.
- Do not make `ActionDetail` or `ItemDetail` special cases.
- Do not replace the existing string overlay renderer.
- Do not make native UI writes depend on formatted original rendering.

## Current Architecture

Persistent plugin overlays are rendered by
`UIOverlays/TranslationOverlay/TranslationOverlayRenderer.cs`.

Hover plugin tooltips are rendered by
`NativeUI/Helpers/HoverTooltipManager.cs`.

Both paths currently consume plain `string` values for the content they draw.
The RTL and complex-script path renders strings through
`UIOverlays/TextPresentation/RtlTexturePresentationService.cs`.

Swap mode already follows the correct high-level rule: translated text can be
written into the native UI while the plugin-owned presentation shows the
original. This spec only improves the presentation of that original payload
when the original payload is available in a safe owned form.

## Proposed Architecture

Introduce a small shared presentation value that can travel from capture code
to overlay and hover-tooltip renderers:

```csharp
internal sealed record RichOriginalTextPresentation(
    string PlainText,
    byte[]? SeStringPayloadBytes);
```

The exact type name can change during implementation, but the responsibility
must stay narrow:

- hold the plain fallback text
- hold an optional owned formatted payload
- never hold `AtkTextNode*`, `SeStringPointer`, raw unmanaged pointers, or
  frame-lifetime spans
- expose no translation behavior

Renderers should treat this value as optional. Existing callers can continue to
pass strings. New capture paths can attach a formatted original only when it is
safe and useful.

## Data Flow

### Capture

Surface handlers should capture formatted original text only from sources that
can be converted to owned data immediately.

For native text nodes, this means reading the original SeString payload and
copying it into an owned representation during the current frame. A handler must
not store a pointer-backed `ReadOnlySeStringSpan` or any other view into game UI
memory.

For sheet-backed or evaluator-backed sources that already provide owned
`ReadOnlySeString`, the capture path can store the serialized bytes or an
equivalent owned representation.

For `AtkValue`, StringArrayData, or sources where formatting is unavailable,
the capture path stores only the existing plain string.

### Translation

Translation remains string and canonical-payload based in this slice. The rich
original payload is not sent to translation engines and is not persisted as a
translated payload.

### Mode Resolution

Formatted original rendering is eligible only when the effective mode shows
original text in a plugin-owned presentation layer:

- `NativeUiTranslationWithOriginalTooltips`
- `NativeUiTranslationWithOriginalOverlay`
- any existing per-surface swap mode that semantically means translated native
  UI plus original plugin presentation

Overlay-only translation mode should continue to show translated text through
the current presentation path. It should not draw the original formatted payload.

Native-only mode should not draw plugin presentation.

### Presentation

`TranslationOverlayRenderer` and `HoverTooltipManager` should share the same
decision rule:

- if a rich original payload exists
- and the active backend is plain ImGui
- and the active presentation text is the original swap text
- draw the rich payload through `ImGuiHelpers.SeStringWrapped`
- otherwise draw the plain string using the existing renderer

The rich renderer must participate in existing width and wrap calculations. It
should measure and draw with the same font selection, text color fallback, and
wrap width used by the current string renderer.

### RTL and Texture Rendering

For languages that use `RtlTexturePresentationService`, the formatted payload
must fall back to the plain string in this slice.

Reason: `ImGuiHelpers.SeStringWrapped` handles game-text payload rendering for
ImGui text, but it does not solve the plugin's texture-backed shaping and
bidirectional rendering path. Adding colored or formatted spans to
`RtlTexturePresentationService` is a separate feature.

## Surface Eligibility

Eligibility is not a hardcoded surface allowlist. A surface participates when
it can provide an owned formatted original payload and already uses the shared
overlay or hover-tooltip presentation layer.

Expected early surfaces include:

- `ActionDetail`
- `ItemDetail`
- quest or journal surfaces where the original text is captured from text nodes
- map or recommendation surfaces if a formatted original payload is available

Surfaces that only expose plain strings continue unchanged.

## Error Handling

If formatted payload parsing or rendering fails, the renderer must fall back to
the plain string for that frame and avoid repeated hot-path logging.

Recoverable failures should be throttled or logged through existing diagnostic
paths only when diagnostics are enabled.

Malformed or unsupported formatted payloads must never block translation,
native mutation, overlay display, or hover-tooltip display.

## Performance

The implementation must avoid per-frame serialization work where possible.

Formatted payload ownership should be established at capture time and reused
while the same original content remains active. Existing cache keys or source
content hashes should be reused where available.

Renderers should not allocate large byte arrays every frame just to decide that
the payload is unchanged.

## Testing Strategy

Unit tests should cover the shared model and mode-selection policy:

- rich original is eligible only for swap presentations
- overlay-only translation does not draw rich original
- native-only mode does not draw plugin presentation
- missing rich payload falls back to plain string
- RTL texture backend falls back to plain string

Renderer contract tests should verify that `TranslationOverlayRenderer` and
`HoverTooltipManager` consume the same shared rich presentation path instead of
implementing surface-specific logic.

Mock or DalaMock validation should cover at least one hosted startup scenario
with swap mode enabled, confirming that the new capability is wired without
requiring a live game text node.

In-game validation should cover a formatted tooltip or overlay source with
visible colored terms, such as action/status text similar to the DelvUI PR
example. The expected result is that the plugin-owned swap presentation shows
the original with formatting where the payload was captured, and shows the
existing plain text where no payload was captured.

## Risks

The primary risk is lifetime safety. The implementation must copy formatted
payload data out of game-owned memory immediately and never retain pointer-backed
views.

The second risk is silently diverging overlay and hover-tooltip behavior. This
is why the feature should use one shared presentation value and one shared
eligibility policy.

The third risk is overreaching into translated rich payloads. This spec does
not attempt translated formatting. That should remain separate because it
requires text-token translation, payload preservation, and reinjection rules.

## Acceptance Criteria

- Swap presentations can render an owned formatted original payload through the
  shared plugin presentation path.
- Overlays and hover tooltips use the same rich-original model and eligibility
  policy.
- No implementation path stores unmanaged text pointers or frame-lifetime spans.
- Every surface without a formatted original payload behaves exactly as before.
- RTL texture-backed languages fall back to plain string behavior.
- Automated tests cover policy, fallback behavior, and renderer wiring.
- Documentation states that this is global for swap presentations, not limited
  to `ActionDetail` or `ItemDetail`.
