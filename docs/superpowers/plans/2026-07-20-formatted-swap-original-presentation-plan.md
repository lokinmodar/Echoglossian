# Formatted Swap Original Presentation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Render owned formatted original game text in Echoglossian plugin overlays and hover tooltips whenever swap mode is showing the original text and a safe formatted payload is available.

**Architecture:** Add one shared rich-original presentation model under `UIOverlays/TextPresentation`, wire it through `TranslationOverlay` and `HoverTooltipManager`, and let surface capture paths attach optional owned payloads. Renderers use Dalamud's `ImGuiHelpers.SeStringWrapped(ReadOnlySpan<byte>, ref SeStringDrawParams, ImGuiId, ImGuiButtonFlags)` only when the shared policy says the active presentation is swap-original on the plain ImGui backend; all other cases keep the current plain string or RTL texture behavior.

**Tech Stack:** C#/.NET 10, Dalamud Release v15.0.2.3, Lumina `ReadOnlySeString`, Dalamud `ImGuiHelpers.SeStringWrapped`, ImGui.NET bindings, xUnit, Echoglossian.Mock/DalaMock.

## Global Constraints

- Do not translate formatted payloads in this slice.
- Do not reinject translated text into the original payload structure.
- Do not change DB persistence schemas for translations.
- Do not make `ActionDetail` or `ItemDetail` special cases.
- Do not replace the existing string overlay renderer.
- Do not make native UI writes depend on formatted original rendering.
- Never retain `AtkTextNode*`, unmanaged pointers, `CStringPointer`, `ReadOnlySeStringSpan`, or other frame-lifetime views beyond the frame in which they were read.
- Use `CStringPointer.AsReadOnlySeString()` when copying native text node SeString content because Dalamud documents it as returning a copied `ReadOnlySeString`.
- Use `ReadOnlySeString.Data.ToArray()` when converting an owned `ReadOnlySeString` to renderer-owned bytes.
- Use `ImGuiHelpers.SeStringWrapped(ReadOnlySpan<byte>, ref SeStringDrawParams, ImGuiId, ImGuiButtonFlags)` for formatted plain-ImGui rendering.
- RTL texture-backed languages must fall back to existing plain string behavior in this slice.
- Keep overlay-only mode rendering translated strings through the current path.
- Keep native-only mode from drawing plugin presentation.
- Do not add hot-path log spam; formatted render failures must fall back quietly.
- Validate implementation changes with `dotnet build Echoglossian.sln -c Debug --no-restore`.
- Validate unit tests with `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`.
- Validate runtime/game-data-adjacent wiring with Echoglossian.Mock or DalaMock where a test can exercise it without a live game client.

---

## File Structure

- Create `UIOverlays/TextPresentation/RichOriginalTextPresentation.cs` to hold the plain fallback text and optional owned SeString bytes.
- Create `UIOverlays/TextPresentation/RichOriginalTextPresentationPolicy.cs` to decide whether a renderer should use formatted original bytes or plain string fallback.
- Create `UIOverlays/TextPresentation/IFormattedOriginalTextRenderer.cs` to provide a headless-test seam around the actual ImGui draw call.
- Create `UIOverlays/TextPresentation/FormattedOriginalTextRenderer.cs` to call `ImGuiHelpers.SeStringWrapped`.
- Create `UIOverlays/TextPresentation/NullFormattedOriginalTextRenderer.cs` for tests and non-rendering fallbacks.
- Modify `UIOverlays/TranslationOverlay/TranslationOverlay.cs` to carry an optional rich-original payload with overlay state.
- Modify `UIOverlays/TranslationOverlay/TranslationOverlayRenderer.cs` to use the shared policy and renderer before falling back to existing string drawing.
- Modify `NativeUI/Helpers/HoverTooltipManager.cs` to carry and draw an optional rich-original payload on tooltip bodies.
- Modify `NativeUI/Helpers/HoverTooltipRegistration.cs` to pass rich-original payloads through the existing translated tooltip helpers.
- Modify `NativeUI/Helpers/ActionItemDetailUiRuntime.cs` only if the existing action/item detail structured tooltip path needs explicit payload clearing or forwarding after the shared API exists.
- Modify targeted surface handlers that call `RegisterTranslatedHoverTooltip(AtkTextNode*)` only when they can pass the copied `ReadOnlySeString` immediately.
- Add tests in `Echoglossian.Tests/RichOriginalTextPresentationTests.cs`.
- Add tests in `Echoglossian.Tests/RichOriginalTextPresentationPolicyTests.cs`.
- Add tests in `Echoglossian.Tests/HoverTooltipManagerRichOriginalTests.cs`.
- Add tests in `Echoglossian.Tests/TranslationOverlayRichOriginalTests.cs`.
- Add or extend mock validation in `Echoglossian.Mock.Tests` if an existing hosted startup scenario can assert the new renderer seam without an ImGui context.

---

### Task 1: Shared Rich-Original Model and Policy

**Files:**
- Create: `UIOverlays/TextPresentation/RichOriginalTextPresentation.cs`
- Create: `UIOverlays/TextPresentation/RichOriginalTextPresentationPolicy.cs`
- Test: `Echoglossian.Tests/RichOriginalTextPresentationTests.cs`
- Test: `Echoglossian.Tests/RichOriginalTextPresentationPolicyTests.cs`

**Interfaces:**
- Consumes: `TextPresentationBackendKind` from `UIOverlays/TextPresentation/TextPresentationBackendKind.cs`.
- Produces: `RichOriginalTextPresentation.PlainText`, `RichOriginalTextPresentation.PayloadSpan`, `RichOriginalTextPresentation.HasFormattedPayload`, `RichOriginalTextPresentation.Plain(string)`, `RichOriginalTextPresentation.FromOwnedSeString(string, ReadOnlySeString)`, `RichOriginalTextPresentation.FromOwnedPayloadBytes(string, ReadOnlySpan<byte>)`.
- Produces: `RichOriginalTextRenderDecision` enum with `PlainString` and `FormattedOriginal`.
- Produces: `RichOriginalTextPresentationPolicy.Decide(bool presentationShowsOriginal, TextPresentationBackendKind backendKind, RichOriginalTextPresentation? presentation)`.

- [ ] **Step 1: Write the failing model tests**

Add `Echoglossian.Tests/RichOriginalTextPresentationTests.cs`:

```csharp
// <copyright file="RichOriginalTextPresentationTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.UIOverlays.TextPresentation;
using Lumina.Text.ReadOnly;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the owned formatted-original payload model.
/// </summary>
public sealed class RichOriginalTextPresentationTests
{
    /// <summary>
    ///     Plain presentations preserve fallback text and expose no formatted payload.
    /// </summary>
    [Fact]
    public void Plain_StoresFallbackTextWithoutPayload()
    {
        var presentation = RichOriginalTextPresentation.Plain("original text");

        Assert.Equal("original text", presentation.PlainText);
        Assert.False(presentation.HasFormattedPayload);
        Assert.True(presentation.PayloadSpan.IsEmpty);
    }

    /// <summary>
    ///     Owned SeString presentations copy their bytes for renderer-safe reuse.
    /// </summary>
    [Fact]
    public void FromOwnedSeString_CopiesPayloadBytes()
    {
        var source = ReadOnlySeString.FromText("Acceleration");
        var presentation = RichOriginalTextPresentation.FromOwnedSeString(
            "Acceleration",
            source);

        Assert.Equal("Acceleration", presentation.PlainText);
        Assert.True(presentation.HasFormattedPayload);
        Assert.Equal(source.Data.ToArray(), presentation.PayloadSpan.ToArray());
    }

    /// <summary>
    ///     Byte-span factory copies the incoming span and does not keep caller-owned memory.
    /// </summary>
    [Fact]
    public void FromOwnedPayloadBytes_CopiesIncomingSpan()
    {
        var source = ReadOnlySeString.FromText("Impact").Data.ToArray();
        var presentation = RichOriginalTextPresentation.FromOwnedPayloadBytes(
            "Impact",
            source.AsSpan());

        source[0] = 0;

        Assert.Equal("Impact", presentation.PlainText);
        Assert.True(presentation.HasFormattedPayload);
        Assert.NotEqual(source, presentation.PayloadSpan.ToArray());
    }

    /// <summary>
    ///     Empty byte payloads degrade to plain fallback presentations.
    /// </summary>
    [Fact]
    public void FromOwnedPayloadBytes_EmptySpanProducesPlainFallback()
    {
        var presentation = RichOriginalTextPresentation.FromOwnedPayloadBytes(
            "Fallback",
            ReadOnlySpan<byte>.Empty);

        Assert.Equal("Fallback", presentation.PlainText);
        Assert.False(presentation.HasFormattedPayload);
        Assert.True(presentation.PayloadSpan.IsEmpty);
    }
}
```

- [ ] **Step 2: Write the failing policy tests**

Add `Echoglossian.Tests/RichOriginalTextPresentationPolicyTests.cs`:

```csharp
// <copyright file="RichOriginalTextPresentationPolicyTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.UIOverlays.TextPresentation;
using Lumina.Text.ReadOnly;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the shared formatted-original rendering decision.
/// </summary>
public sealed class RichOriginalTextPresentationPolicyTests
{
    /// <summary>
    ///     Swap-original on plain ImGui with payload selects formatted rendering.
    /// </summary>
    [Fact]
    public void Decide_SwapOriginalPlainImGuiWithPayload_UsesFormattedOriginal()
    {
        var presentation = RichOriginalTextPresentation.FromOwnedSeString(
            "Original",
            ReadOnlySeString.FromText("Original"));

        var decision = RichOriginalTextPresentationPolicy.Decide(
            presentationShowsOriginal: true,
            TextPresentationBackendKind.PlainImGui,
            presentation);

        Assert.Equal(RichOriginalTextRenderDecision.FormattedOriginal, decision);
    }

    /// <summary>
    ///     Non-swap presentation uses the existing plain string path.
    /// </summary>
    [Fact]
    public void Decide_NotShowingOriginal_UsesPlainString()
    {
        var presentation = RichOriginalTextPresentation.FromOwnedSeString(
            "Original",
            ReadOnlySeString.FromText("Original"));

        var decision = RichOriginalTextPresentationPolicy.Decide(
            presentationShowsOriginal: false,
            TextPresentationBackendKind.PlainImGui,
            presentation);

        Assert.Equal(RichOriginalTextRenderDecision.PlainString, decision);
    }

    /// <summary>
    ///     RTL texture presentation keeps the existing string fallback.
    /// </summary>
    [Fact]
    public void Decide_RtlTextureBackend_UsesPlainString()
    {
        var presentation = RichOriginalTextPresentation.FromOwnedSeString(
            "Original",
            ReadOnlySeString.FromText("Original"));

        var decision = RichOriginalTextPresentationPolicy.Decide(
            presentationShowsOriginal: true,
            TextPresentationBackendKind.RtlTexture,
            presentation);

        Assert.Equal(RichOriginalTextRenderDecision.PlainString, decision);
    }

    /// <summary>
    ///     Missing payload keeps the existing string fallback.
    /// </summary>
    [Fact]
    public void Decide_MissingPayload_UsesPlainString()
    {
        var decision = RichOriginalTextPresentationPolicy.Decide(
            presentationShowsOriginal: true,
            TextPresentationBackendKind.PlainImGui,
            RichOriginalTextPresentation.Plain("Original"));

        Assert.Equal(RichOriginalTextRenderDecision.PlainString, decision);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~RichOriginalTextPresentation"
```

Expected: test discovery or compilation fails because `RichOriginalTextPresentation`, `RichOriginalTextPresentationPolicy`, and `RichOriginalTextRenderDecision` do not exist.

- [ ] **Step 4: Add the model**

Add `UIOverlays/TextPresentation/RichOriginalTextPresentation.cs`:

```csharp
// <copyright file="RichOriginalTextPresentation.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Lumina.Text.ReadOnly;

namespace Echoglossian.UIOverlays.TextPresentation;

/// <summary>
///     Carries plain fallback text and optional owned formatted original bytes.
/// </summary>
/// <param name="PlainText">The plain string used by existing fallback renderers.</param>
/// <param name="SeStringPayloadBytes">Owned SeString bytes, if formatting is available.</param>
internal sealed record RichOriginalTextPresentation(
    string PlainText,
    byte[]? SeStringPayloadBytes)
{
    /// <summary>
    ///     Gets a value indicating whether formatted payload bytes are available.
    /// </summary>
    public bool HasFormattedPayload => this.SeStringPayloadBytes is { Length: > 0 };

    /// <summary>
    ///     Gets the owned payload bytes as a read-only span.
    /// </summary>
    public ReadOnlySpan<byte> PayloadSpan =>
        this.SeStringPayloadBytes?.AsSpan() ?? ReadOnlySpan<byte>.Empty;

    /// <summary>
    ///     Creates a plain fallback presentation.
    /// </summary>
    /// <param name="plainText">The plain fallback text.</param>
    /// <returns>A plain presentation with no formatted payload.</returns>
    public static RichOriginalTextPresentation Plain(string plainText)
    {
        return new RichOriginalTextPresentation(plainText ?? string.Empty, null);
    }

    /// <summary>
    ///     Creates a presentation by copying bytes from an owned SeString.
    /// </summary>
    /// <param name="plainText">The plain fallback text.</param>
    /// <param name="seString">The owned formatted SeString.</param>
    /// <returns>A rich presentation with copied payload bytes.</returns>
    public static RichOriginalTextPresentation FromOwnedSeString(
        string plainText,
        ReadOnlySeString seString)
    {
        var payloadBytes = seString.Data.ToArray();
        return payloadBytes.Length == 0
            ? Plain(plainText)
            : new RichOriginalTextPresentation(plainText ?? string.Empty, payloadBytes);
    }

    /// <summary>
    ///     Creates a presentation by copying the supplied payload bytes.
    /// </summary>
    /// <param name="plainText">The plain fallback text.</param>
    /// <param name="payloadBytes">The formatted payload bytes to copy.</param>
    /// <returns>A rich presentation or plain presentation when the payload is empty.</returns>
    public static RichOriginalTextPresentation FromOwnedPayloadBytes(
        string plainText,
        ReadOnlySpan<byte> payloadBytes)
    {
        var fallback = plainText ?? string.Empty;
        return payloadBytes.IsEmpty
            ? Plain(fallback)
            : new RichOriginalTextPresentation(fallback, payloadBytes.ToArray());
    }
}
```

- [ ] **Step 5: Add the policy**

Add `UIOverlays/TextPresentation/RichOriginalTextPresentationPolicy.cs`:

```csharp
// <copyright file="RichOriginalTextPresentationPolicy.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.UIOverlays.TextPresentation;

/// <summary>
///     Identifies which rendering path should draw a presentation body.
/// </summary>
internal enum RichOriginalTextRenderDecision
{
    /// <summary>
    ///     Draw the existing plain string path.
    /// </summary>
    PlainString,

    /// <summary>
    ///     Draw the owned formatted original payload.
    /// </summary>
    FormattedOriginal,
}

/// <summary>
///     Shared decision policy for formatted original rendering.
/// </summary>
internal static class RichOriginalTextPresentationPolicy
{
    /// <summary>
    ///     Decides whether a renderer should use formatted original bytes.
    /// </summary>
    /// <param name="presentationShowsOriginal">Whether the current mode is showing original text in plugin presentation.</param>
    /// <param name="backendKind">The resolved text presentation backend.</param>
    /// <param name="presentation">The optional rich original presentation.</param>
    /// <returns>The rendering decision.</returns>
    public static RichOriginalTextRenderDecision Decide(
        bool presentationShowsOriginal,
        TextPresentationBackendKind backendKind,
        RichOriginalTextPresentation? presentation)
    {
        if (!presentationShowsOriginal ||
            backendKind != TextPresentationBackendKind.PlainImGui ||
            presentation is not { HasFormattedPayload: true })
        {
            return RichOriginalTextRenderDecision.PlainString;
        }

        return RichOriginalTextRenderDecision.FormattedOriginal;
    }
}
```

- [ ] **Step 6: Run the focused tests**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter "FullyQualifiedName~RichOriginalTextPresentation"
```

Expected: all `RichOriginalTextPresentation` and `RichOriginalTextPresentationPolicy` tests pass.

- [ ] **Step 7: Commit Task 1**

Run:

```powershell
git add UIOverlays/TextPresentation/RichOriginalTextPresentation.cs UIOverlays/TextPresentation/RichOriginalTextPresentationPolicy.cs Echoglossian.Tests/RichOriginalTextPresentationTests.cs Echoglossian.Tests/RichOriginalTextPresentationPolicyTests.cs
git commit -m "feat: add rich original presentation policy"
git push
```

Expected: commit and push succeed on `feature/issues-230-233-234`.

---

### Task 2: Renderer Seam Around Dalamud Formatted SeString Drawing

**Files:**
- Create: `UIOverlays/TextPresentation/IFormattedOriginalTextRenderer.cs`
- Create: `UIOverlays/TextPresentation/FormattedOriginalTextRenderer.cs`
- Create: `UIOverlays/TextPresentation/NullFormattedOriginalTextRenderer.cs`
- Test: `Echoglossian.Tests/FormattedOriginalTextRendererTests.cs`

**Interfaces:**
- Consumes: `RichOriginalTextPresentation`.
- Produces: `IFormattedOriginalTextRenderer.TryDraw(RichOriginalTextPresentation presentation, ref SeStringDrawParams drawParams, ImGuiId imGuiId, ImGuiButtonFlags buttonFlags)`.
- Produces: `FormattedOriginalTextRenderer.Instance`.
- Produces: `NullFormattedOriginalTextRenderer.Instance`.

- [ ] **Step 1: Write failing seam tests**

Add `Echoglossian.Tests/FormattedOriginalTextRendererTests.cs`:

```csharp
// <copyright file="FormattedOriginalTextRendererTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiSeStringRenderer;
using Dalamud.Interface.Utility;
using Echoglossian.UIOverlays.TextPresentation;
using Lumina.Text.ReadOnly;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the formatted-original renderer seam used by overlay and tooltip renderers.
/// </summary>
public sealed class FormattedOriginalTextRendererTests
{
    /// <summary>
    ///     Null renderer never draws and is safe for headless tests.
    /// </summary>
    [Fact]
    public void NullRenderer_NeverDraws()
    {
        var presentation = RichOriginalTextPresentation.FromOwnedSeString(
            "Original",
            ReadOnlySeString.FromText("Original"));
        var drawParams = new SeStringDrawParams();

        var drawn = NullFormattedOriginalTextRenderer.Instance.TryDraw(
            presentation,
            ref drawParams,
            default,
            ImGuiButtonFlags.None);

        Assert.False(drawn);
    }

    /// <summary>
    ///     Real renderer rejects missing payload before touching ImGui.
    /// </summary>
    [Fact]
    public void FormattedRenderer_MissingPayloadReturnsFalse()
    {
        var drawParams = new SeStringDrawParams();

        var drawn = FormattedOriginalTextRenderer.Instance.TryDraw(
            RichOriginalTextPresentation.Plain("Original"),
            ref drawParams,
            default,
            ImGuiButtonFlags.None);

        Assert.False(drawn);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~FormattedOriginalTextRenderer"
```

Expected: test discovery or compilation fails because the renderer types do not exist.

- [ ] **Step 3: Add the renderer interface**

Add `UIOverlays/TextPresentation/IFormattedOriginalTextRenderer.cs`:

```csharp
// <copyright file="IFormattedOriginalTextRenderer.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiSeStringRenderer;
using Dalamud.Interface.Utility;

namespace Echoglossian.UIOverlays.TextPresentation;

/// <summary>
///     Draws an owned formatted original payload in an ImGui context.
/// </summary>
internal interface IFormattedOriginalTextRenderer
{
    /// <summary>
    ///     Tries to draw formatted original text.
    /// </summary>
    /// <param name="presentation">The rich original presentation.</param>
    /// <param name="drawParams">The Dalamud SeString draw parameters.</param>
    /// <param name="imGuiId">Optional ImGui id for link behavior.</param>
    /// <param name="buttonFlags">Button flags for link behavior.</param>
    /// <returns><see langword="true"/> when the formatted renderer accepted the payload.</returns>
    bool TryDraw(
        RichOriginalTextPresentation presentation,
        ref SeStringDrawParams drawParams,
        ImGuiId imGuiId,
        ImGuiButtonFlags buttonFlags);
}
```

- [ ] **Step 4: Add the real renderer**

Add `UIOverlays/TextPresentation/FormattedOriginalTextRenderer.cs`:

```csharp
// <copyright file="FormattedOriginalTextRenderer.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiSeStringRenderer;
using Dalamud.Interface.Utility;

namespace Echoglossian.UIOverlays.TextPresentation;

/// <summary>
///     Uses Dalamud's ImGui SeString renderer for owned original payloads.
/// </summary>
internal sealed class FormattedOriginalTextRenderer : IFormattedOriginalTextRenderer
{
    /// <summary>
    ///     Gets the shared renderer instance.
    /// </summary>
    public static FormattedOriginalTextRenderer Instance { get; } = new();

    private FormattedOriginalTextRenderer()
    {
    }

    /// <inheritdoc/>
    public bool TryDraw(
        RichOriginalTextPresentation presentation,
        ref SeStringDrawParams drawParams,
        ImGuiId imGuiId,
        ImGuiButtonFlags buttonFlags)
    {
        if (!presentation.HasFormattedPayload)
        {
            return false;
        }

        try
        {
            _ = ImGuiHelpers.SeStringWrapped(
                presentation.PayloadSpan,
                ref drawParams,
                imGuiId,
                buttonFlags);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
```

- [ ] **Step 5: Add the null renderer**

Add `UIOverlays/TextPresentation/NullFormattedOriginalTextRenderer.cs`:

```csharp
// <copyright file="NullFormattedOriginalTextRenderer.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiSeStringRenderer;
using Dalamud.Interface.Utility;

namespace Echoglossian.UIOverlays.TextPresentation;

/// <summary>
///     Non-rendering formatted-original renderer for headless tests.
/// </summary>
internal sealed class NullFormattedOriginalTextRenderer : IFormattedOriginalTextRenderer
{
    /// <summary>
    ///     Gets the shared null renderer instance.
    /// </summary>
    public static NullFormattedOriginalTextRenderer Instance { get; } = new();

    private NullFormattedOriginalTextRenderer()
    {
    }

    /// <inheritdoc/>
    public bool TryDraw(
        RichOriginalTextPresentation presentation,
        ref SeStringDrawParams drawParams,
        ImGuiId imGuiId,
        ImGuiButtonFlags buttonFlags)
    {
        return false;
    }
}
```

- [ ] **Step 6: Run the focused tests**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter "FullyQualifiedName~FormattedOriginalTextRenderer"
```

Expected: renderer seam tests pass.

- [ ] **Step 7: Commit Task 2**

Run:

```powershell
git add UIOverlays/TextPresentation/IFormattedOriginalTextRenderer.cs UIOverlays/TextPresentation/FormattedOriginalTextRenderer.cs UIOverlays/TextPresentation/NullFormattedOriginalTextRenderer.cs Echoglossian.Tests/FormattedOriginalTextRendererTests.cs
git commit -m "feat: add formatted original renderer seam"
git push
```

Expected: commit and push succeed.

---

### Task 3: Wire Formatted Originals Into Translation Overlays

**Files:**
- Modify: `UIOverlays/TranslationOverlay/TranslationOverlay.cs`
- Modify: `UIOverlays/TranslationOverlay/TranslationOverlayRenderer.cs`
- Test: `Echoglossian.Tests/TranslationOverlayRichOriginalTests.cs`

**Interfaces:**
- Consumes: `RichOriginalTextPresentation`.
- Consumes: `IFormattedOriginalTextRenderer`.
- Produces: `TranslationOverlay.CurrentRichOriginalPresentation`.
- Produces: `TranslationOverlayRenderer` constructor overload with `IFormattedOriginalTextRenderer formattedOriginalTextRenderer`.
- Produces: `TranslationOverlayRenderer.ShouldUseFormattedOriginalBody(bool showsOriginal, TextPresentationBackendKind backendKind, RichOriginalTextPresentation? presentation)`.

- [ ] **Step 1: Write failing overlay policy wiring tests**

Add `Echoglossian.Tests/TranslationOverlayRichOriginalTests.cs`:

```csharp
// <copyright file="TranslationOverlayRichOriginalTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.UIOverlays.TextPresentation;
using Echoglossian.UIOverlays.TranslationOverlay;
using Lumina.Text.ReadOnly;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers overlay formatted-original routing without requiring an ImGui frame.
/// </summary>
public sealed class TranslationOverlayRichOriginalTests
{
    /// <summary>
    ///     Overlay state can carry a rich original presentation.
    /// </summary>
    [Fact]
    public void TranslationOverlay_CarriesRichOriginalPresentation()
    {
        var overlay = new TranslationOverlay();
        var presentation = RichOriginalTextPresentation.FromOwnedSeString(
            "Original",
            ReadOnlySeString.FromText("Original"));

        overlay.CurrentRichOriginalPresentation = presentation;

        Assert.Same(presentation, overlay.CurrentRichOriginalPresentation);
    }

    /// <summary>
    ///     Renderer routing uses formatted original only when the shared policy allows it.
    /// </summary>
    [Fact]
    public void ShouldUseFormattedOriginalBody_RequiresSharedPolicyDecision()
    {
        var presentation = RichOriginalTextPresentation.FromOwnedSeString(
            "Original",
            ReadOnlySeString.FromText("Original"));

        Assert.True(TranslationOverlayRenderer.ShouldUseFormattedOriginalBody(
            showsOriginal: true,
            TextPresentationBackendKind.PlainImGui,
            presentation));
        Assert.False(TranslationOverlayRenderer.ShouldUseFormattedOriginalBody(
            showsOriginal: false,
            TextPresentationBackendKind.PlainImGui,
            presentation));
        Assert.False(TranslationOverlayRenderer.ShouldUseFormattedOriginalBody(
            showsOriginal: true,
            TextPresentationBackendKind.RtlTexture,
            presentation));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~TranslationOverlayRichOriginal"
```

Expected: compilation fails because `CurrentRichOriginalPresentation` and `ShouldUseFormattedOriginalBody` do not exist.

- [ ] **Step 3: Add overlay state**

Modify `UIOverlays/TranslationOverlay/TranslationOverlay.cs` by adding this property next to `CurrentText`:

```csharp
    /// <summary>
    /// Gets or sets the optional owned formatted original presentation for swap mode.
    /// </summary>
    public RichOriginalTextPresentation? CurrentRichOriginalPresentation { get; set; }
```

- [ ] **Step 4: Add renderer dependency and routing helper**

Modify `UIOverlays/TranslationOverlay/TranslationOverlayRenderer.cs` constructor fields:

```csharp
    private readonly IFormattedOriginalTextRenderer formattedOriginalTextRenderer;
```

Change the existing constructor to delegate to a new overload:

```csharp
    public TranslationOverlayRenderer(
        Config configuration,
        IUiFontRuntime fontRuntime,
        RtlTexturePresentationService rtlTexturePresentationService)
        : this(
            configuration,
            fontRuntime,
            rtlTexturePresentationService,
            FormattedOriginalTextRenderer.Instance)
    {
    }

    internal TranslationOverlayRenderer(
        Config configuration,
        IUiFontRuntime fontRuntime,
        RtlTexturePresentationService rtlTexturePresentationService,
        IFormattedOriginalTextRenderer formattedOriginalTextRenderer)
    {
        this.configuration = configuration;
        this.fontRuntime = fontRuntime;
        this.rtlTexturePresentationService = rtlTexturePresentationService;
        this.formattedOriginalTextRenderer = formattedOriginalTextRenderer;
    }
```

Add the routing helper near the other internal static helpers:

```csharp
    /// <summary>
    /// Gets whether the overlay body should use formatted original rendering.
    /// </summary>
    /// <param name="showsOriginal">Whether this overlay currently shows original text.</param>
    /// <param name="backendKind">The resolved presentation backend.</param>
    /// <param name="presentation">The optional rich original presentation.</param>
    /// <returns><see langword="true"/> when formatted original rendering is eligible.</returns>
    internal static bool ShouldUseFormattedOriginalBody(
        bool showsOriginal,
        TextPresentationBackendKind backendKind,
        RichOriginalTextPresentation? presentation)
    {
        return RichOriginalTextPresentationPolicy.Decide(
            showsOriginal,
            backendKind,
            presentation) == RichOriginalTextRenderDecision.FormattedOriginal;
    }
```

- [ ] **Step 5: Snapshot rich state under the overlay semaphore**

Modify the first overlay semaphore block in `TranslationOverlayRenderer.Draw(...)` from only reading `overlayText` to reading both values:

```csharp
        string overlayText;
        RichOriginalTextPresentation? richOriginalPresentation;
        bool shouldDraw;
        overlay.Semaphore.Wait();
        try
        {
            overlayText = TranslationOverlayTextNormalizationHelper.NormalizeForDisplay(
                overlay.CurrentText);
            richOriginalPresentation = overlay.CurrentRichOriginalPresentation;
            shouldDraw = !string.IsNullOrEmpty(overlayText) &&
                         overlayText != Resources.WaitingForTranslation;
        }
        finally
        {
            overlay.Semaphore.Release();
        }
```

Keep the existing `shouldDraw` behavior unchanged.

- [ ] **Step 6: Draw formatted original before plain string fallback**

Inside the `backendKind == TextPresentationBackendKind.PlainImGui` body drawing branch, replace the plain-only loop with this pattern:

```csharp
                    var drewFormattedOriginal = false;
                    if (ShouldUseFormattedOriginalBody(
                        shouldUseGeneralFont,
                        backendKind,
                        richOriginalPresentation))
                    {
                        var drawParams = new SeStringDrawParams
                        {
                            Color = ImGui.ColorConvertFloat4ToU32(
                                new Vector4(config.TextColor.X, config.TextColor.Y, config.TextColor.Z, 1f)),
                            WrapWidth = ImGui.GetContentRegionAvail().X,
                        };
                        drewFormattedOriginal = this.formattedOriginalTextRenderer.TryDraw(
                            richOriginalPresentation!,
                            ref drawParams,
                            default,
                            ImGuiButtonFlags.None);
                    }

                    if (!drewFormattedOriginal)
                    {
                        foreach (var line in overlayTextLines)
                        {
                            if (string.IsNullOrEmpty(line))
                            {
                                ImGui.Spacing();
                                continue;
                            }

                            DrawOverlayLine(
                                line,
                                shouldCenterOverlayText,
                                shouldRightAlignOverlayText);
                        }
                    }
```

Use `shouldUseGeneralFont` as the current overlay "shows original" signal because it is already derived from `TranslationDisplayModeHelper.ShowsOriginalOverlayText(...)`.

- [ ] **Step 7: Ensure overlay state is cleared when plain current text changes to translated-only**

Where handlers set `overlay.CurrentText` to translated text in non-swap modes, set:

```csharp
overlay.CurrentRichOriginalPresentation = null;
```

Use `rg -n "CurrentText =" NativeUI UIOverlays` and update only paths that also manipulate plugin presentation text. Do not touch DB, translation queues, or native mutation paths.

- [ ] **Step 8: Run focused overlay tests**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter "FullyQualifiedName~TranslationOverlayRichOriginal"
```

Expected: overlay rich-original tests pass.

- [ ] **Step 9: Commit Task 3**

Run:

```powershell
git add UIOverlays/TranslationOverlay/TranslationOverlay.cs UIOverlays/TranslationOverlay/TranslationOverlayRenderer.cs Echoglossian.Tests/TranslationOverlayRichOriginalTests.cs
git commit -m "feat: route formatted originals through overlays"
git push
```

Expected: commit and push succeed.

---

### Task 4: Wire Formatted Originals Into Hover Tooltips

**Files:**
- Modify: `NativeUI/Helpers/HoverTooltipManager.cs`
- Modify: `NativeUI/Helpers/HoverTooltipRegistration.cs`
- Test: `Echoglossian.Tests/HoverTooltipManagerRichOriginalTests.cs`

**Interfaces:**
- Consumes: `RichOriginalTextPresentation`.
- Consumes: `IFormattedOriginalTextRenderer`.
- Produces: optional `RichOriginalTextPresentation? richOriginalBody` parameter on `HoverTooltipManager.Register(...)`.
- Produces: `HoverTooltipEntry.RichOriginalBody`.
- Produces: `HoverTooltipManager.ShouldUseFormattedOriginalBody(bool usesTexturePresentation, bool useGeneralFont, RichOriginalTextPresentation? presentation)`.
- Produces: optional `RichOriginalTextPresentation? richOriginalBody = null` parameter on all private `RegisterHoverTooltip(...)` helpers and `RegisterTranslatedHoverTooltip(...)` helpers.

- [ ] **Step 1: Write failing tooltip routing tests**

Add `Echoglossian.Tests/HoverTooltipManagerRichOriginalTests.cs`:

```csharp
// <copyright file="HoverTooltipManagerRichOriginalTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;
using Echoglossian.UIOverlays.TextPresentation;
using Lumina.Text.ReadOnly;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers hover tooltip formatted-original routing without requiring an ImGui frame.
/// </summary>
public sealed class HoverTooltipManagerRichOriginalTests
{
    /// <summary>
    ///     Tooltip body uses formatted original only for swap-original plain-ImGui rendering.
    /// </summary>
    [Fact]
    public void ShouldUseFormattedOriginalBody_RequiresPlainSwapOriginalBody()
    {
        var presentation = RichOriginalTextPresentation.FromOwnedSeString(
            "Original",
            ReadOnlySeString.FromText("Original"));

        Assert.True(HoverTooltipManager.ShouldUseFormattedOriginalBody(
            usesTexturePresentation: false,
            useGeneralFont: true,
            presentation));
        Assert.False(HoverTooltipManager.ShouldUseFormattedOriginalBody(
            usesTexturePresentation: true,
            useGeneralFont: true,
            presentation));
        Assert.False(HoverTooltipManager.ShouldUseFormattedOriginalBody(
            usesTexturePresentation: false,
            useGeneralFont: false,
            presentation));
        Assert.False(HoverTooltipManager.ShouldUseFormattedOriginalBody(
            usesTexturePresentation: false,
            useGeneralFont: true,
            RichOriginalTextPresentation.Plain("Original")));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~HoverTooltipManagerRichOriginal"
```

Expected: compilation fails because `HoverTooltipManager.ShouldUseFormattedOriginalBody` does not exist.

- [ ] **Step 3: Add renderer dependency to tooltip manager**

Modify `NativeUI/Helpers/HoverTooltipManager.cs` fields and constructor:

```csharp
    private readonly IFormattedOriginalTextRenderer formattedOriginalTextRenderer;
```

Replace the constructor with a public existing-shape constructor that delegates to an internal overload:

```csharp
    internal HoverTooltipManager(
        Config config,
        UINewFontHandler fontHandler,
        RtlTexturePresentationService rtlTexturePresentationService)
        : this(
            config,
            fontHandler,
            rtlTexturePresentationService,
            FormattedOriginalTextRenderer.Instance)
    {
    }

    internal HoverTooltipManager(
        Config config,
        UINewFontHandler fontHandler,
        RtlTexturePresentationService rtlTexturePresentationService,
        IFormattedOriginalTextRenderer formattedOriginalTextRenderer)
    {
        this.config = config;
        this.fontHandler = fontHandler;
        this.rtlTexturePresentationService = rtlTexturePresentationService;
        this.formattedOriginalTextRenderer = formattedOriginalTextRenderer;
    }
```

- [ ] **Step 4: Extend `Register` and entry state**

Change `HoverTooltipManager.Register(...)` signature to:

```csharp
    public void Register(
        string key,
        Vector2 topLeft,
        Vector2 bottomRight,
        string title,
        string body,
        bool enabled = true,
        bool useGeneralFont = false,
        RichOriginalTextPresentation? richOriginalBody = null)
```

Change the `HoverTooltipEntry` creation to include the new value:

```csharp
        var newEntry = new HoverTooltipEntry(
            topLeft,
            bottomRight,
            title,
            body,
            enabled,
            useGeneralFont,
            richOriginalBody,
            DateTime.UtcNow);
```

Update the private record at the bottom of `HoverTooltipManager.cs` to this shape:

```csharp
internal sealed record HoverTooltipEntry(
    Vector2 TopLeft,
    Vector2 BottomRight,
    string Title,
    string Body,
    bool Enabled,
    bool UseGeneralFont,
    RichOriginalTextPresentation? RichOriginalBody,
    DateTime LastUpdatedUtc);
```

- [ ] **Step 5: Add shared tooltip decision helper**

Add this helper near other internal static helpers:

```csharp
    /// <summary>
    /// Gets whether a tooltip body should use formatted original rendering.
    /// </summary>
    /// <param name="usesTexturePresentation">Whether the active language uses texture rendering.</param>
    /// <param name="useGeneralFont">Whether the tooltip is showing original swap text.</param>
    /// <param name="presentation">The optional rich original presentation.</param>
    /// <returns><see langword="true"/> when formatted original rendering is eligible.</returns>
    internal static bool ShouldUseFormattedOriginalBody(
        bool usesTexturePresentation,
        bool useGeneralFont,
        RichOriginalTextPresentation? presentation)
    {
        var backendKind = usesTexturePresentation
            ? TextPresentationBackendKind.RtlTexture
            : TextPresentationBackendKind.PlainImGui;
        return RichOriginalTextPresentationPolicy.Decide(
            useGeneralFont,
            backendKind,
            presentation) == RichOriginalTextRenderDecision.FormattedOriginal;
    }
```

Use `useGeneralFont` as the tooltip "shows original" signal because the translated tooltip registration already sets `useGeneralFont: shouldSwap`.

- [ ] **Step 6: Draw rich body before plain body fallback**

Inside `HoverTooltipManager.Draw()`, after title handling in the non-texture branch, replace the plain body draw call with:

```csharp
            var drewFormattedBody = false;
            if (ShouldUseFormattedOriginalBody(
                useTexturePresentation,
                hoveredEntry.UseGeneralFont,
                hoveredEntry.RichOriginalBody))
            {
                var drawParams = new SeStringDrawParams
                {
                    Color = ImGui.ColorConvertFloat4ToU32(textColor),
                    WrapWidth = Math.Max(240f, ImGui.GetContentRegionAvail().X),
                };
                drewFormattedBody = this.formattedOriginalTextRenderer.TryDraw(
                    hoveredEntry.RichOriginalBody!,
                    ref drawParams,
                    default,
                    ImGuiButtonFlags.None);
            }

            if (!drewFormattedBody)
            {
                DrawPlainTooltipText(body, shouldRightAlign);
            }
```

Keep the existing texture branch unchanged, so RTL languages continue to draw plain string textures.

- [ ] **Step 7: Pass rich payloads through tooltip registration helpers**

Modify each private `RegisterHoverTooltip(...)` overload in `NativeUI/Helpers/HoverTooltipRegistration.cs` to accept:

```csharp
      RichOriginalTextPresentation? richOriginalBody = null)
```

Pass the value to `this.hoverTooltipManager.Register(...)`:

```csharp
        useGeneralFont,
        richOriginalBody);
```

Modify each `RegisterTranslatedHoverTooltip(...)` overload to accept:

```csharp
      RichOriginalTextPresentation? richOriginalOriginalText = null)
```

When calling `RegisterHoverTooltip(...)`, pass:

```csharp
        useGeneralFont: shouldSwap,
        richOriginalBody: shouldSwap ? richOriginalOriginalText : null);
```

- [ ] **Step 8: Run focused tooltip tests**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter "FullyQualifiedName~HoverTooltipManagerRichOriginal"
```

Expected: tooltip routing tests pass.

- [ ] **Step 9: Commit Task 4**

Run:

```powershell
git add NativeUI/Helpers/HoverTooltipManager.cs NativeUI/Helpers/HoverTooltipRegistration.cs Echoglossian.Tests/HoverTooltipManagerRichOriginalTests.cs
git commit -m "feat: route formatted originals through hover tooltips"
git push
```

Expected: commit and push succeed.

---

### Task 5: Add Safe Native Text-Node Capture Helpers

**Files:**
- Create: `NativeUI/Helpers/RichOriginalTextCaptureHelper.cs`
- Test: `Echoglossian.Tests/RichOriginalTextCaptureHelperTests.cs`

**Interfaces:**
- Consumes: `AtkTextNode*`.
- Consumes: `CStringPointer.AsReadOnlySeString()` from Dalamud.
- Produces: `RichOriginalTextCaptureHelper.TryCreateFromTextNode(AtkTextNode* textNode, string plainText, out RichOriginalTextPresentation? presentation)`.
- Produces: `RichOriginalTextCaptureHelper.TryCreateFromOwnedSeString(string plainText, ReadOnlySeString seString, out RichOriginalTextPresentation? presentation)`.

- [ ] **Step 1: Write failing owned-SeString capture tests**

Add `Echoglossian.Tests/RichOriginalTextCaptureHelperTests.cs`:

```csharp
// <copyright file="RichOriginalTextCaptureHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;
using Lumina.Text.ReadOnly;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers safe formatted-original capture helpers.
/// </summary>
public sealed class RichOriginalTextCaptureHelperTests
{
    /// <summary>
    ///     Owned SeString capture returns a presentation with copied payload bytes.
    /// </summary>
    [Fact]
    public void TryCreateFromOwnedSeString_CreatesCopiedPresentation()
    {
        var seString = ReadOnlySeString.FromText("Verthunder III");

        var created = RichOriginalTextCaptureHelper.TryCreateFromOwnedSeString(
            "Verthunder III",
            seString,
            out var presentation);

        Assert.True(created);
        Assert.NotNull(presentation);
        Assert.Equal("Verthunder III", presentation.PlainText);
        Assert.Equal(seString.Data.ToArray(), presentation.PayloadSpan.ToArray());
    }

    /// <summary>
    ///     Blank fallback text is rejected so callers keep existing plain-string behavior.
    /// </summary>
    [Fact]
    public void TryCreateFromOwnedSeString_BlankPlainTextReturnsFalse()
    {
        var created = RichOriginalTextCaptureHelper.TryCreateFromOwnedSeString(
            "   ",
            ReadOnlySeString.FromText("Hidden"),
            out var presentation);

        Assert.False(created);
        Assert.Null(presentation);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~RichOriginalTextCaptureHelper"
```

Expected: compilation fails because `RichOriginalTextCaptureHelper` does not exist.

- [ ] **Step 3: Add the capture helper**

Add `NativeUI/Helpers/RichOriginalTextCaptureHelper.cs`:

```csharp
// <copyright file="RichOriginalTextCaptureHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Text.ReadOnly;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Creates owned formatted-original presentations from safe sources.
/// </summary>
internal static unsafe class RichOriginalTextCaptureHelper
{
    /// <summary>
    ///     Copies a text node's original SeString into an owned rich presentation.
    /// </summary>
    /// <param name="textNode">The text node to read during the current frame.</param>
    /// <param name="plainText">The plain fallback text already captured by the caller.</param>
    /// <param name="presentation">The created rich presentation, if available.</param>
    /// <returns><see langword="true"/> when a rich presentation was created.</returns>
    public static bool TryCreateFromTextNode(
        AtkTextNode* textNode,
        string plainText,
        out RichOriginalTextPresentation? presentation)
    {
        presentation = null;
        if (textNode == null || string.IsNullOrWhiteSpace(plainText))
        {
            return false;
        }

        try
        {
            var copied = textNode->OriginalTextPointer.AsReadOnlySeString();
            return TryCreateFromOwnedSeString(plainText, copied, out presentation);
        }
        catch
        {
            presentation = null;
            return false;
        }
    }

    /// <summary>
    ///     Converts an already-owned SeString into a rich presentation.
    /// </summary>
    /// <param name="plainText">The plain fallback text.</param>
    /// <param name="seString">The owned formatted SeString.</param>
    /// <param name="presentation">The created rich presentation, if available.</param>
    /// <returns><see langword="true"/> when a rich presentation was created.</returns>
    public static bool TryCreateFromOwnedSeString(
        string plainText,
        ReadOnlySeString seString,
        out RichOriginalTextPresentation? presentation)
    {
        presentation = null;
        if (string.IsNullOrWhiteSpace(plainText) || seString.Data.IsEmpty)
        {
            return false;
        }

        presentation = RichOriginalTextPresentation.FromOwnedSeString(
            plainText,
            seString);
        return presentation.HasFormattedPayload;
    }
}
```

- [ ] **Step 4: Run focused capture tests**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter "FullyQualifiedName~RichOriginalTextCaptureHelper"
```

Expected: owned-SeString capture tests pass.

- [ ] **Step 5: Commit Task 5**

Run:

```powershell
git add NativeUI/Helpers/RichOriginalTextCaptureHelper.cs Echoglossian.Tests/RichOriginalTextCaptureHelperTests.cs
git commit -m "feat: add safe rich original capture helper"
git push
```

Expected: commit and push succeed.

---

### Task 6: Attach Rich Originals to First Real Surfaces

**Files:**
- Modify: surface handlers that already call `RegisterTranslatedHoverTooltip(...)` with `AtkTextNode*`.
- Modify: overlay update sites that already have both original `AtkTextNode*` and translated overlay text available.
- Test: extend existing focused tests for the touched surfaces when they can run without live UI pointers.
- Mock: add or extend one Echoglossian.Mock/DalaMock scenario for owned-SeString payload creation if existing test harness can instantiate the helper path.

**Interfaces:**
- Consumes: `RichOriginalTextCaptureHelper.TryCreateFromTextNode(...)`.
- Consumes: `RegisterTranslatedHoverTooltip(..., RichOriginalTextPresentation? richOriginalOriginalText = null)`.
- Consumes: `TranslationOverlay.CurrentRichOriginalPresentation`.
- Produces: rich payloads only for swap-original plugin presentations.

- [ ] **Step 1: Inventory existing tooltip call sites**

Run:

```powershell
rg -n "RegisterTranslatedHoverTooltip\\(" NativeUI
```

Expected: output lists all translated tooltip call sites. Classify them into:

```text
AtkTextNode-backed: can pass RichOriginalTextCaptureHelper.TryCreateFromTextNode(...)
AtkResNode or bounds-only: keep plain fallback unless they also have owned SeString source
Addon-wide: keep plain fallback unless they also have owned SeString source
```

- [ ] **Step 2: Patch only text-node-backed call sites with immediate copied payloads**

For each `AtkTextNode* textNode` call site where `originalText` is the visible original for the same node, add this pattern immediately before registration:

```csharp
RichOriginalTextPresentation? richOriginalText = null;
if (shouldSwap)
{
    _ = RichOriginalTextCaptureHelper.TryCreateFromTextNode(
        textNode,
        originalText,
        out richOriginalText);
}

this.RegisterTranslatedHoverTooltip(
    key,
    textNode,
    originalText,
    translatedText,
    translatedPayloadReady,
    swapEnabled: shouldSwap,
    forceEnabled,
    denseHitbox,
    richOriginalOriginalText: richOriginalText);
```

If the call site does not have a local `shouldSwap`, compute it using the same expression already passed to `swapEnabled` or let the helper receive `null` by using:

```csharp
var effectiveSwap = swapEnabled ?? this.configuration.SwapTextsUsingImGui;
```

Do not add rich capture to surfaces where the original string is from StringArrayData, `AtkValue`, database text, or manually composed strings.

- [ ] **Step 3: Patch overlay update sites only where source node and original overlay text match**

Run:

```powershell
rg -n "CurrentRichOriginalPresentation|CurrentText =" NativeUI UIOverlays
```

For overlay update sites with a matching `AtkTextNode* textNode` and original text variable, use this pattern inside the existing overlay semaphore:

```csharp
overlay.CurrentRichOriginalPresentation = null;
if (showsOriginal)
{
    _ = RichOriginalTextCaptureHelper.TryCreateFromTextNode(
        textNode,
        originalText,
        out var richOriginalText);
    overlay.CurrentRichOriginalPresentation = richOriginalText;
}
```

Use the surface's existing swap/original mode variable for `showsOriginal`. Do not infer swap mode from translated text being non-empty.

- [ ] **Step 4: Add focused tests for updated pure helpers**

If a touched surface has existing tests that can verify registration arguments without live ImGui or live `AtkTextNode*`, extend the test with:

```csharp
Assert.True(registeredEntry.RichOriginalBody?.HasFormattedPayload);
```

If the manager entry remains private, test the public/internal static routing helper and capture helper instead. Do not make hot-path production fields public solely for tests.

- [ ] **Step 5: Add mock validation for owned-SeString capture path**

If `Echoglossian.Mock.Tests` already has a hosted test that can create `ReadOnlySeString.FromText(...)`, add:

```csharp
Assert.True(RichOriginalTextCaptureHelper.TryCreateFromOwnedSeString(
    "Grand Impact Ready",
    ReadOnlySeString.FromText("Grand Impact Ready"),
    out var presentation));
Assert.NotNull(presentation);
Assert.True(presentation.HasFormattedPayload);
```

Use the existing mock fixture naming and setup style. Do not create a new mock host just for this assertion if no compatible host exists.

- [ ] **Step 6: Run focused tests**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter "FullyQualifiedName~RichOriginalText"
```

Expected: all rich-original model, policy, renderer seam, overlay, tooltip, and capture tests pass.

If a mock test was added, run:

```powershell
dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --filter "FullyQualifiedName~RichOriginalText"
```

Expected: the added mock validation passes.

- [ ] **Step 7: Commit Task 6**

Run:

```powershell
git add NativeUI UIOverlays Echoglossian.Tests Echoglossian.Mock.Tests
git commit -m "feat: attach rich originals to swap presentations"
git push
```

Expected: commit and push succeed.

---

### Task 7: Full Validation and Runtime Checklist

**Files:**
- Modify: `Echoglossian.xml` only if XML documentation output changed and the build updates it.
- No new production code unless validation finds a concrete defect.

**Interfaces:**
- Consumes: all previous tasks.
- Produces: final validated branch state.

- [ ] **Step 1: Run formatting and diff checks**

Run:

```powershell
git diff --check
```

Expected: no whitespace errors.

- [ ] **Step 2: Run Debug build**

Run:

```powershell
dotnet build Echoglossian.sln -c Debug --no-restore
```

Expected: build succeeds with zero new errors. Existing warnings are acceptable only if they were already present before this feature.

- [ ] **Step 3: Run main tests without rebuild**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
```

Expected: all tests pass.

- [ ] **Step 4: Run mock tests without rebuild**

Run:

```powershell
dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build
```

Expected: all mock tests pass or the same known environment-only failure appears with no rich-original regression evidence.

- [ ] **Step 5: Confirm generated plugin DLL path**

Run:

```powershell
Get-ChildItem -Path bin\x64\Debug\win-x64 -Filter Echoglossian.dll | Select-Object -ExpandProperty FullName
```

Expected: output includes:

```text
C:\Dante\_dalamud\Echoglossian\.worktrees\issues-230-233-234\bin\x64\Debug\win-x64\Echoglossian.dll
```

- [ ] **Step 6: In-game runtime validation**

Install the Debug DLL above and verify:

```text
Swap mode, ActionDetail or ItemDetail: plugin-owned original tooltip/overlay shows formatted original text when the source payload is captured.
Swap mode, surfaces without rich payload: behavior stays exactly as before with plain original text.
Overlay-only translation mode: plugin presentation shows translated text, not formatted original text.
Native-only mode: no plugin tooltip or overlay appears solely because a rich original payload exists.
RTL texture-backed language: existing texture presentation path is used and no formatted SeString draw is attempted.
No crash or stack overflow when logging in, opening AreaMap, hovering RecommendList, hovering ScenarioTree, hovering action details, or hovering item details.
```

- [ ] **Step 7: Commit validation artifacts if needed**

If `Echoglossian.xml` changed because the build regenerated documentation, run:

```powershell
git add Echoglossian.xml
git commit -m "docs: update generated xml docs"
git push
```

If no files changed, do not create an empty commit.

- [ ] **Step 8: Final status**

Run:

```powershell
git status --short --branch
```

Expected:

```text
## feature/issues-230-233-234...origin/feature/issues-230-233-234
```

with no unstaged or uncommitted files.

---

## Self-Review

Spec coverage:

- Optional formatted-original payload in shared presentation path: Task 1.
- Shared overlay and hover-tooltip use: Tasks 3 and 4.
- Safe ownership and no pointer retention: Task 5 and Global Constraints.
- String fallback for missing payloads: Tasks 1, 3, and 4.
- RTL texture fallback: Tasks 1, 3, 4, and 7.
- No translated rich payload or native reinjection: Global Constraints and Task 6 limits.
- Global capability rather than item/action special case: File Structure and Task 6 classification.
- Tests and mock validation: Tasks 1 through 7.

Red-flag scan:

- This plan contains no unresolved task labels and no instruction to invent unspecified behavior.
- Every code-changing task includes concrete file paths, concrete signatures, and test commands.

Type consistency:

- `RichOriginalTextPresentation` is defined in Task 1 and consumed by Tasks 2 through 6.
- `RichOriginalTextPresentationPolicy.Decide(...)` is defined in Task 1 and consumed by Tasks 3 and 4.
- `IFormattedOriginalTextRenderer.TryDraw(...)` is defined in Task 2 and consumed by Tasks 3 and 4.
- `RichOriginalTextCaptureHelper.TryCreateFromTextNode(...)` and `TryCreateFromOwnedSeString(...)` are defined in Task 5 and consumed by Task 6.
