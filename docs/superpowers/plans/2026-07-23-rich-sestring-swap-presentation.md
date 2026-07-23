# Rich SeString Swap Presentation Implementation Plan

> **For Codex:** Execute this plan inline, one task at a time. Keep each task test-first and commit/push each completed, validated slice.

**Goal:** Render the original FFXIV `SeString` payload with Dalamud's ImGui renderer whenever swap presentation is showing the original text, while preserving the existing plain-text and RTL fallback paths.

**Architecture:** Presentation gets an owned `RichOriginalTextPresentation` value containing plain fallback text and optional copied `SeString` bytes. Capture copies bytes synchronously while a native text node is valid; it never stores pointers, spans, delegates, or unmanaged references. The shared hover-tooltip and overlay renderers use that value only for original swap presentation in the normal ImGui backend. All other cases retain their existing rendering behavior.

**Tech Stack:** C#/.NET 10, Dalamud `ReadOnlySeString`, `ISeStringEvaluator`, `ImGuiHelpers.SeStringWrapped`, ImGui.NET, Echoglossian.Mock, NUnit.

## Constraints

- This is presentation-only. It must not write to native addon nodes, `AtkValue`s, or `StringArrayData`.
- Rich payloads are captured only from original game text. They are never sent to translation engines and are never persisted in the database.
- Convert `ReadOnlySeStringSpan` to owned bytes before returning from the native-node access scope. Do not retain a native pointer, span, or callback beyond that scope.
- Render formatted bytes only when the plugin is displaying the original as swap content and `LanguagePresentationPolicy` selects the normal ImGui backend.
- RTL / texture presentation, missing bytes, evaluation failures, and rendering failures must use the existing plain-text behavior without hot-path warnings or retries.
- Reuse the existing shared overlay and hover-tooltip paths. Do not add a separate renderer, queue, or cache.

## Task 1: Add Owned Rich Presentation Model And Policy

**Files:**
- Create: `PluginUI/Runtime/RichOriginalTextPresentation.cs`
- Create: `PluginUI/Runtime/RichOriginalTextPresentationPolicy.cs`
- Create: `Echoglossian.Tests/PluginUI/Runtime/RichOriginalTextPresentationTests.cs`

1. Write tests that prove the model clones supplied bytes and exposes only immutable byte memory.
2. Write tests that prove the policy permits rich rendering only for original swap content in the normal ImGui backend and rejects RTL / missing payloads.
3. Run the focused tests and confirm they fail because the types do not exist.
4. Implement the immutable model with a required plain-text fallback and an optional owned `byte[]` payload.
5. Implement the small policy type with no renderer-specific state.
6. Run the focused tests and confirm they pass.

## Task 2: Add Rich Body Rendering To Shared Hover Tooltips

**Files:**
- Modify: `NativeUI/Helpers/HoverTooltipManager.cs`
- Modify: `NativeUI/Helpers/HoverTooltipRegistration.cs`
- Modify: `NativeUI/AddonHandlers/Common/DbFirstGameWindowAddonHandler.cs`
- Modify: `NativeUI/AddonHandlers/Quest/QuestAddonHandlerBase.cs`
- Modify: `NativeUI/AddonHandlers/Quest/QuestAddonHandlerDependencies.cs`
- Create or modify: focused hover-tooltip presentation tests under `Echoglossian.Tests/NativeUI/Helpers/`

1. Write tests for a hover entry that carries an optional rich original-body presentation and keeps the plain body as its fallback.
2. Write tests for registration policy: only swap registration may supply the rich payload; overlay-only and native translated presentation must supply none.
3. Run the focused tests and confirm the new assertions fail.
4. Extend `HoverTooltipManager` entries and registration with an optional factory invoked synchronously only when text changes. The manager must reuse the current owned presentation when the entry content is unchanged.
5. In the text-node registration overload, evaluate/copy the original `SeString` immediately and return the owned presentation only when its evaluated plain text matches the original text selected for display.
6. Render with `ImGuiHelpers.SeStringWrapped` inside the existing ImGui tooltip window, passing the current font, cursor screen position, font size, and existing wrap width. If it cannot render, use the current plain body draw path.
7. Preserve the texture/RTL path exactly as it is today.
8. Run focused tests, then the full `Echoglossian.Tests` suite.

## Task 3: Make Shared Overlay Rendering Rich-Presentation Ready

**Files:**
- Modify: `UIOverlays/TranslationOverlay/TranslationOverlay.cs`
- Modify: `UIOverlays/TranslationOverlay/TranslationOverlayDrawer.cs`
- Modify: `UIOverlays/TranslationOverlay/TranslationOverlayRenderer.cs`
- Modify: `UIOverlays/TranslationOverlay/TranslationOverlayRenderRequest.cs` only if necessary
- Create or modify: focused overlay renderer tests under `Echoglossian.Tests/UIOverlays/TranslationOverlay/`

1. Write tests for `UpdateOverlayContent` retaining a rich original presentation only when the overlay is showing original swap content, and clearing it with the overlay state.
2. Write tests for renderer selection: formatted bytes are eligible only under the shared policy; normal plain, translated, RTL, and missing-data cases remain plain/texture.
3. Run focused tests and confirm they fail.
4. Add the optional owned presentation to `TranslationOverlay`, guarded by its existing semaphore.
5. Extend the drawer update API with an optional presentation parameter and make all existing callers unchanged by default.
6. In the renderer, estimate dimensions with existing plain text layout, then call `ImGuiHelpers.SeStringWrapped` only within the normal ImGui draw path. Continue to use existing texture presentation for RTL.
7. Do not add native capture to `ActionDetail` / `ItemDetail` while their native UI mode remains disabled. The renderer becomes ready for a future safe producer after FFXIVClientStructs PR #1891 is available.
8. Run focused tests, then the full `Echoglossian.Tests` suite.

## Task 4: Validate Runtime Contracts And Documentation

**Files:**
- Modify: `docs/superpowers/specs/2026-07-20-formatted-swap-original-presentation-design.md`
- Modify: `Echoglossian.Mock.Tests/` only if the current mock can represent a copied SeString payload without a game process
- Modify: `AGENTS.md` only if a new reusable validation rule is discovered

1. Update the design status to implemented/validated and record the initial producer coverage.
2. Add or extend mock tests that create a `ReadOnlySeString` with formatting payloads, verify that capture clones bytes, and verify fallback behavior when rendering is ineligible.
3. Run:
   - `dotnet build Echoglossian.sln -c Debug --no-restore`
   - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
   - `dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build`
4. Build the plugin DLL and verify the output path.
5. In-game verification:
   - enable a swap mode on a text-node-backed hover tooltip containing game formatting;
   - confirm formatting survives in the plugin tooltip;
   - confirm overlay-only remains translation-only and makes no native writes;
   - confirm RTL still uses the prior texture presentation;
   - confirm `ActionDetail` / `ItemDetail` remain Plugin Tooltips only until their native mappings are available.

## Commit Plan

1. Commit Task 1 after its focused tests pass.
2. Commit Task 2 after hover tests and full plugin tests pass.
3. Commit Task 3 after overlay tests and full plugin tests pass.
4. Commit Task 4 only after all validation commands pass.
5. Push every commit to `origin/feature/issues-230-233-234`.
