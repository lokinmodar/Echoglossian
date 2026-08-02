// <copyright file="TranslationOverlayRenderer.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Interface.ImGuiSeStringRenderer;
using Echoglossian.PluginUI.Runtime;
using Echoglossian.UIOverlays.TextPresentation;

namespace Echoglossian.UIOverlays.TranslationOverlay;

/// <summary>
/// Renders translation overlays through the active ImGui runtime.
/// </summary>
internal sealed class TranslationOverlayRenderer : IDisposable
{
    private readonly Config configuration;
    private readonly IUiFontRuntime fontRuntime;
    private readonly RtlTexturePresentationService rtlTexturePresentationService;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TranslationOverlayRenderer"/> class.
    /// </summary>
    /// <param name="configuration">The active plugin configuration.</param>
    /// <param name="fontRuntime">The runtime font stack provider.</param>
    /// <param name="rtlTexturePresentationService">The raster-text backend.</param>
    public TranslationOverlayRenderer(
        Config configuration,
        IUiFontRuntime fontRuntime,
        RtlTexturePresentationService rtlTexturePresentationService)
    {
        this.configuration = configuration;
        this.fontRuntime = fontRuntime;
        this.rtlTexturePresentationService = rtlTexturePresentationService;
    }

    /// <summary>
    /// Draws one visible translation overlay.
    /// </summary>
    /// <param name="request">The overlay render request.</param>
    /// <returns>The actual rendered bounds and presentation backend.</returns>
    public TranslationOverlayRenderResult Draw(
        TranslationOverlayRenderRequest request)
    {
        return this.Draw(request, customTitle: null);
    }

    /// <summary>
    /// Draws one visible translation overlay with an optional runtime title.
    /// </summary>
    /// <param name="request">The overlay render request.</param>
    /// <param name="customTitle">The caller-provided title, if any.</param>
    /// <returns>The actual rendered bounds and presentation backend.</returns>
    internal TranslationOverlayRenderResult Draw(
        TranslationOverlayRenderRequest request,
        string? customTitle)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);

        var overlay = request.Overlay;
        if (!overlay.Display)
        {
            return NotDrawn();
        }

        string overlayText;
        bool shouldDraw;
        bool displaysOriginalSwapText;
        RichOriginalTextPresentation? richOriginalTextPresentation;
        overlay.Semaphore.Wait();
        try
        {
            overlayText = TranslationOverlayTextNormalizationHelper.NormalizeForDisplay(
                overlay.CurrentText);
            shouldDraw = !string.IsNullOrEmpty(overlayText) &&
                         overlayText != Resources.WaitingForTranslation;
            displaysOriginalSwapText = overlay.DisplaysOriginalSwapText;
            richOriginalTextPresentation = overlay.RichOriginalTextPresentation;
        }
        finally
        {
            overlay.Semaphore.Release();
        }
        if (!shouldDraw)
        {
            return NotDrawn();
        }

        var config = request.WindowConfig;
        var resolvedTitle = string.IsNullOrWhiteSpace(customTitle)
            ? !string.IsNullOrWhiteSpace(overlay.CurrentName)
                ? overlay.CurrentName
                : overlay.OriginalName
            : customTitle;
        var shouldUseGeneralFont = this.ShouldUseGeneralOverlayFont(config);
        var runtimeScaleMultiplier = Math.Clamp(request.ScaleMultiplier, 0.25f, 3f);
        var runtimeAlphaMultiplier = Math.Clamp(request.AlphaMultiplier, 0f, 1f);
        var effectiveFontScale = GetEffectiveOverlayFontScale(
            config.FontScale * runtimeScaleMultiplier);
        var shouldCenterOverlayText = ShouldCenterOverlayText(config.SurfaceId);
        var shouldRightAlignOverlayText =
            !shouldCenterOverlayText &&
            LanguagePresentationPolicy.ShouldRightAlign(this.configuration.Lang);
        var horizontalPadding = ImGui.GetStyle().WindowPadding.X * 2f;
        var textureMaxWidthOverride = this.ResolveTextureMaxWidthOverride(
            request,
            config);
        var textureLineHeightScaleOverride =
            this.ResolveTextureLineHeightScaleOverride(request, config);
        var preliminaryLayout = TranslationOverlayLayoutCalculator.Calculate(
            new TranslationOverlayLayoutRequest(
                request.ViewportPosition,
                request.ViewportSize,
                request.AddonPosition,
                request.AddonSize,
                overlay.ImGuiSize,
                Vector2.Zero,
                Vector2.Zero,
                horizontalPadding,
                config));
        var textRequest = new TextLayoutRequest(
            overlayText,
            this.configuration.Lang,
            SelectedLanguage.Code,
            this.ResolveMeasurementWrapWidth(
                request,
                config,
                horizontalPadding,
                preliminaryLayout.ContentWrapWidth),
            effectiveFontScale,
            shouldUseGeneralFont,
            new Vector4(config.TextColor.X, config.TextColor.Y, config.TextColor.Z, 1f),
            Vector4.Zero,
            config.SurfaceId,
            shouldCenterOverlayText,
            MaxWidthOverride: textureMaxWidthOverride,
            LineHeightScaleOverride: textureLineHeightScaleOverride);
        var backendKind = TextPresentationResolver.ResolveBackendKind(textRequest);
        var shouldRenderRichOriginalText = ShouldRenderRichOriginalText(
            backendKind,
            displaysOriginalSwapText && shouldUseGeneralFont,
            shouldCenterOverlayText || shouldRightAlignOverlayText,
            richOriginalTextPresentation);
        string[] overlayTextLines = [];
        RenderedTextBlock? bodyBlock = null;
        RenderedTextBlock? titleBlock = null;
        Vector2 measuredTextSize;
        Vector2 measuredTitleSize = Vector2.Zero;

        if (backendKind == TextPresentationBackendKind.RtlTexture)
        {
            bodyBlock = this.rtlTexturePresentationService.TryRender(textRequest);
            if (bodyBlock == null)
            {
                return NotDrawn(backendKind);
            }

            measuredTextSize = bodyBlock.MeasuredSize;
            if (config.ForceShowTitle && !string.IsNullOrWhiteSpace(resolvedTitle))
            {
                titleBlock = this.rtlTexturePresentationService.TryRender(
                    textRequest with
                    {
                        Text = resolvedTitle,
                        CenterAligned = false,
                    });
                measuredTitleSize = titleBlock?.MeasuredSize ?? Vector2.Zero;
            }
        }
        else
        {
            overlayTextLines = SplitOverlayTextLines(overlayText);
            measuredTextSize = this.MeasureOverlayTextSize(
                overlayText,
                overlayTextLines,
                shouldUseGeneralFont,
                effectiveFontScale);
        }

        using var bodyTextureLease = bodyBlock?.Texture;
        using var titleTextureLease = titleBlock?.Texture;
        var layout = TranslationOverlayLayoutCalculator.Calculate(
            new TranslationOverlayLayoutRequest(
                request.ViewportPosition,
                request.ViewportSize,
                request.AddonPosition,
                request.AddonSize,
                overlay.ImGuiSize,
                measuredTextSize,
                measuredTitleSize,
                horizontalPadding,
                config));
        if (request.IsPreview)
        {
            ImGui.SetNextWindowPos(layout.RequestedPosition);
        }
        else
        {
            ImGuiHelpers.SetNextWindowPosRelativeMainViewport(layout.RequestedPosition);
        }
        var maxHeight = Math.Max(180f, request.ViewportSize.Y - 80f);
        if (config.AutoSizeToTextWithMaxWidth || config.UseFixedWindowSize)
        {
            ImGui.SetNextWindowSizeConstraints(
                new Vector2(layout.RequestedSize.X, 0f),
                new Vector2(layout.RequestedSize.X, maxHeight));
        }
        else
        {
            ImGui.SetNextWindowSizeConstraints(
                new Vector2(layout.RequestedSize.X, 0f),
                new Vector2(layout.RequestedSize.X * 4f, maxHeight));
        }

        var pushedStyleColor = false;
        var pushedStyleAlpha = false;
        var beganWindow = false;
        IDisposable? fontScope = null;
        try
        {
            ImGui.PushStyleColor(
                ImGuiCol.Text,
                new Vector4(config.TextColor.X, config.TextColor.Y, config.TextColor.Z, 1f));
            pushedStyleColor = true;
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, runtimeAlphaMultiplier);
            pushedStyleAlpha = true;
            if (backendKind == TextPresentationBackendKind.PlainImGui)
            {
                fontScope = this.fontRuntime.Push(
                    shouldUseGeneralFont ? UiFontKind.General : UiFontKind.Language);
            }

            var flags = ImGuiWindowFlags.NoNav |
                        ImGuiWindowFlags.NoFocusOnAppearing |
                        ImGuiWindowFlags.NoMouseInputs |
                        ImGuiWindowFlags.NoScrollbar |
                        ImGuiWindowFlags.AlwaysAutoResize;
            var useInlineRtlTitle = ShouldUseInlineRtlTitle(
                backendKind,
                config.ForceShowTitle,
                resolvedTitle,
                titleBlock != null);
            if (useInlineRtlTitle || !config.ForceShowTitle ||
                string.IsNullOrWhiteSpace(resolvedTitle))
            {
                flags |= ImGuiWindowFlags.NoTitleBar;
            }

            if (config.NoBackground || config.BackgroundOpacity <= 0f)
            {
                flags |= ImGuiWindowFlags.NoBackground;
            }
            else
            {
                ImGui.SetNextWindowBgAlpha(Math.Clamp(config.BackgroundOpacity, 0f, 1f));
            }

            var windowLabel = BuildWindowLabel(
                backendKind,
                config.DefaultTitle,
                resolvedTitle,
                overlay.GetHashCode(),
                useInlineRtlTitle);
            ImGui.Begin(windowLabel, flags);
            beganWindow = true;
            if (backendKind == TextPresentationBackendKind.PlainImGui)
            {
                ImGui.SetWindowFontScale(effectiveFontScale);
            }

            var renderedPosition = ImGui.GetWindowPos();
            overlay.Semaphore.Wait();
            try
            {
                if (useInlineRtlTitle && titleBlock != null)
                {
                    DrawRenderedTextBlock(titleBlock, centerAligned: false);
                    ImGui.Separator();
                }

                if (backendKind == TextPresentationBackendKind.RtlTexture)
                {
                    DrawRenderedTextBlock(bodyBlock!, shouldCenterOverlayText);
                }
                else
                {
                    if (shouldRenderRichOriginalText &&
                        DrawRichOverlayText(richOriginalTextPresentation!))
                    {
                        // The shared SeString renderer advances the ImGui layout.
                    }
                    else
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
                }
            }
            finally
            {
                overlay.Semaphore.Release();
            }

            overlay.ImGuiSize = ImGui.GetWindowSize();
            return new TranslationOverlayRenderResult(
                true,
                renderedPosition,
                overlay.ImGuiSize,
                backendKind);
        }
        finally
        {
            try
            {
                if (beganWindow)
                {
                    ImGui.End();
                }
            }
            finally
            {
                try
                {
                    fontScope?.Dispose();
                }
                finally
                {
                    try
                    {
                        if (pushedStyleAlpha)
                        {
                            ImGui.PopStyleVar();
                        }
                    }
                    finally
                    {
                        if (pushedStyleColor)
                        {
                            ImGui.PopStyleColor();
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets whether the renderer should replace the normal title bar with an
    /// inline RTL title block.
    /// </summary>
    /// <param name="backendKind">The selected presentation backend.</param>
    /// <param name="forceShowTitle">Whether the surface requires a visible title.</param>
    /// <param name="resolvedTitle">The resolved window title.</param>
    /// <param name="hasTitleBlock">Whether the RTL title texture is ready.</param>
    /// <returns><see langword="true"/> when the inline RTL title path is ready.</returns>
    internal static bool ShouldUseInlineRtlTitle(
        TextPresentationBackendKind backendKind,
        bool forceShowTitle,
        string? resolvedTitle,
        bool hasTitleBlock)
    {
        return backendKind == TextPresentationBackendKind.RtlTexture &&
               forceShowTitle &&
               hasTitleBlock &&
               !string.IsNullOrWhiteSpace(resolvedTitle);
    }

    /// <summary>
    /// Gets whether an overlay can render its copied original SeString payload
    /// without changing the existing RTL or aligned-layout behavior.
    /// </summary>
    /// <param name="backendKind">The resolved text presentation backend.</param>
    /// <param name="displaysOriginalSwapText">
    /// Whether the overlay currently displays original swap content.
    /// </param>
    /// <param name="hasSpecialAlignment">
    /// Whether the plain overlay path requires centered or right-aligned text.
    /// </param>
    /// <param name="presentation">The optional owned original-text payload.</param>
    /// <returns><see langword="true" /> when rich ImGui drawing is safe.</returns>
    internal static bool ShouldRenderRichOriginalText(
        TextPresentationBackendKind backendKind,
        bool displaysOriginalSwapText,
        bool hasSpecialAlignment,
        RichOriginalTextPresentation? presentation)
    {
        return !hasSpecialAlignment &&
               RichOriginalTextPresentationPolicy.CanUseFormattedSeString(
                   backendKind,
                   displaysOriginalSwapText,
                   presentation);
    }

    /// <summary>
    /// Builds the ImGui window label while preserving stable overlay identity.
    /// </summary>
    /// <param name="backendKind">The selected presentation backend.</param>
    /// <param name="defaultTitle">The default surface title.</param>
    /// <param name="resolvedTitle">The resolved surface or speaker title.</param>
    /// <param name="overlayId">The stable overlay identity suffix.</param>
    /// <param name="useInlineRtlTitle">Whether the title is rendered inline.</param>
    /// <returns>The ImGui window label.</returns>
    internal static string BuildWindowLabel(
        TextPresentationBackendKind backendKind,
        string defaultTitle,
        string? resolvedTitle,
        int overlayId,
        bool useInlineRtlTitle)
    {
        var stableSuffix = $"##overlay-{overlayId}";
        if (backendKind == TextPresentationBackendKind.RtlTexture)
        {
            var visibleTitle = useInlineRtlTitle || string.IsNullOrWhiteSpace(resolvedTitle)
                ? defaultTitle
                : resolvedTitle;
            return $"{visibleTitle}{stableSuffix}";
        }

        var defaultVisibleTitle = !string.IsNullOrWhiteSpace(resolvedTitle)
            ? resolvedTitle
            : defaultTitle;
        return $"{defaultVisibleTitle}{stableSuffix}";
    }

    /// <summary>
    /// Disposes the renderer without disposing externally owned text and font runtimes.
    /// </summary>
    public void Dispose()
    {
        this.disposed = true;
    }

    /// <summary>
    /// Measures plain ImGui text using the same font and scale used for drawing.
    /// </summary>
    /// <param name="text">The complete text value.</param>
    /// <param name="lines">The logical lines in the text.</param>
    /// <param name="shouldUseGeneralFont">Whether to use the general font.</param>
    /// <param name="fontScale">The effective font scale.</param>
    /// <returns>The measured text dimensions.</returns>
    private Vector2 MeasureOverlayTextSize(
        string text,
        string[] lines,
        bool shouldUseGeneralFont,
        float fontScale)
    {
        using var fontScope = this.fontRuntime.Push(
            shouldUseGeneralFont ? UiFontKind.General : UiFontKind.Language);
        var measured = lines.Length == 0
            ? ImGui.CalcTextSize(text)
            : new Vector2(
                lines.Max(line => string.IsNullOrEmpty(line)
                    ? 0f
                    : ImGui.CalcTextSize(line).X),
                lines.Sum(line => string.IsNullOrEmpty(line)
                    ? ImGui.GetStyle().ItemSpacing.Y
                    : ImGui.CalcTextSize(line).Y));
        return measured * fontScale;
    }

    /// <summary>
    /// Preserves the legacy maximum-width measurement path for expanding and
    /// auto-sized overlays.
    /// </summary>
    /// <param name="request">The current render request.</param>
    /// <param name="config">The overlay configuration.</param>
    /// <param name="horizontalPadding">The total horizontal window padding.</param>
    /// <param name="finalWrapWidth">The final calculated content wrap width.</param>
    /// <returns>The width available while measuring or rasterizing text.</returns>
    private float ResolveMeasurementWrapWidth(
        TranslationOverlayRenderRequest request,
        TranslationWindowConfig config,
        float horizontalPadding,
        float finalWrapWidth)
    {
        var defaultMaxWidth = Math.Max(320f, request.ViewportSize.X - 80f);
        var maxWidth = config.MaxWidthViewportFraction > 0f
            ? Math.Min(
                request.ViewportSize.X * config.MaxWidthViewportFraction,
                defaultMaxWidth)
            : defaultMaxWidth;
        if (config.ExpandWidthToFitText)
        {
            var baseWidth = request.AddonSize.X * config.WidthMultiplier;
            return Math.Max(
                64f,
                Math.Min(maxWidth, baseWidth * config.MaxAutoExpandedWidthMultiplier) -
                horizontalPadding);
        }

        if (!config.AutoSizeToTextWithMaxWidth)
        {
            return finalWrapWidth;
        }

        return Math.Max(64f, maxWidth - horizontalPadding);
    }

    /// <summary>
    /// Resolves any surface-specific texture width override required by the
    /// current overlay request.
    /// </summary>
    /// <param name="request">The active render request.</param>
    /// <param name="config">The surface configuration.</param>
    /// <returns>The resolved raster width override, if any.</returns>
    private float? ResolveTextureMaxWidthOverride(
        TranslationOverlayRenderRequest request,
        TranslationWindowConfig config)
    {
        if (request.TextureMaxWidthOverride.HasValue &&
            request.TextureMaxWidthOverride.Value > 0f)
        {
            return request.TextureMaxWidthOverride.Value;
        }

        if (config.SurfaceId != TranslationOverlaySurfaceId.TooltipAddon)
        {
            return null;
        }

        var padding = Math.Max(0f, this.configuration.TooltipAddonOverlayPadding) * 2f;
        var nativeWidth = Math.Max(64f, request.AddonSize.X - padding);
        if (this.configuration.TooltipAddonOverlayMaxWidthMode ==
            TooltipAddonOverlayMaxWidthMode.ManualCap)
        {
            var manualWidth = Math.Max(
                64f,
                this.configuration.TooltipAddonOverlayManualMaxWidth - padding);
            return Math.Min(nativeWidth, manualWidth);
        }

        return nativeWidth;
    }

    /// <summary>
    /// Resolves any surface-specific raster line-height override required by
    /// the current overlay request.
    /// </summary>
    /// <param name="request">The active render request.</param>
    /// <param name="config">The surface configuration.</param>
    /// <returns>The resolved line-height override, if any.</returns>
    private float? ResolveTextureLineHeightScaleOverride(
        TranslationOverlayRenderRequest request,
        TranslationWindowConfig config)
    {
        if (request.TextureLineHeightScaleOverride.HasValue)
        {
            return request.TextureLineHeightScaleOverride.Value;
        }

        if (config.SurfaceId != TranslationOverlaySurfaceId.TooltipAddon)
        {
            return null;
        }

        return Math.Clamp(
            this.configuration.TooltipAddonOverlayLineHeightScale,
            0.8f,
            1.2f);
    }

    /// <summary>
    /// Determines whether the surface displays original text in swap mode.
    /// </summary>
    /// <param name="config">The overlay configuration being drawn.</param>
    /// <returns><see langword="true" /> when the general font is required.</returns>
    private bool ShouldUseGeneralOverlayFont(TranslationWindowConfig config)
    {
        var displayMode = config.SurfaceId switch
        {
            TranslationOverlaySurfaceId.Talk => this.configuration.TalkTranslationDisplayMode,
            TranslationOverlaySurfaceId.BattleTalk => this.configuration.BattleTalkTranslationDisplayMode,
            TranslationOverlaySurfaceId.TalkSubtitle => this.configuration.TalkSubtitleTranslationDisplayMode,
            TranslationOverlaySurfaceId.MiniTalk => this.configuration.MiniTalkTranslationDisplayMode,
            TranslationOverlaySurfaceId.CutSceneSelectString => this.configuration.CutSceneSelectStringTranslationDisplayMode,
            TranslationOverlaySurfaceId.SelectYesNo => this.configuration.SelectYesNoTranslationDisplayMode,
            TranslationOverlaySurfaceId.SelectOk => this.configuration.SelectOkTranslationDisplayMode,
            TranslationOverlaySurfaceId.SelectString => this.configuration.SelectStringTranslationDisplayMode,
            TranslationOverlaySurfaceId.TextGimmickHint => this.configuration.TextGimmickHintTranslationDisplayMode,
            TranslationOverlaySurfaceId.WideTextToast => ToastGuiSupportedToastPolicy.UseSupportedNormalToastRuntime(this.configuration)
                ? ToastGuiSupportedToastPolicy.GetNormalToastDisplayMode(this.configuration)
                : this.configuration.WideTextToastTranslationDisplayMode,
            TranslationOverlaySurfaceId.ErrorToast => this.configuration.ErrorToastTranslationDisplayMode,
            TranslationOverlaySurfaceId.AreaToast => this.configuration.AreaToastTranslationDisplayMode,
            TranslationOverlaySurfaceId.ClassChangeToast => this.configuration.ClassChangeToastTranslationDisplayMode,
            TranslationOverlaySurfaceId.QuestToast => this.configuration.QuestToastTranslationDisplayMode,
            TranslationOverlaySurfaceId.NamePlate => this.configuration.NamePlateTranslationDisplayMode,
            _ => JournalTranslationDisplayMode.TooltipTranslation,
        };
        return TranslationDisplayModeHelper.ShowsOriginalOverlayText(
            displayMode,
            this.configuration.OverlayOnlyLanguage);
    }

    /// <summary>
    /// Splits overlay text while retaining empty lines as spacing markers.
    /// </summary>
    /// <param name="text">The text to split.</param>
    /// <returns>The individual text lines.</returns>
    private static string[] SplitOverlayTextLines(string text)
    {
        return string.IsNullOrEmpty(text)
            ? []
            : TranslationOverlayTextNormalizationHelper
                .NormalizeForDisplay(text)
                .Split('\n');
    }

    /// <summary>
    /// Draws one plain ImGui text line with optional horizontal alignment.
    /// </summary>
    /// <param name="line">The text line to draw.</param>
    /// <param name="centerAligned">Whether to center the line.</param>
    /// <param name="rightAligned">Whether to right-align the line.</param>
    private static void DrawOverlayLine(
        string line,
        bool centerAligned,
        bool rightAligned)
    {
        var availableWidth = ImGui.GetContentRegionAvail().X;
        if ((!centerAligned && !rightAligned) || availableWidth <= 0f)
        {
            ImGui.TextWrapped(line);
            return;
        }

        foreach (var visualLine in SplitOverlayLineForAlignment(
                     line,
                     availableWidth,
                     text => ImGui.CalcTextSize(text).X))
        {
            var lineWidth = ImGui.CalcTextSize(visualLine).X;
            var offset = CalculateHorizontalTextOffset(
                availableWidth,
                lineWidth,
                centerAligned,
                rightAligned);
            if (offset > 0f)
            {
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
            }

            ImGui.TextUnformatted(visualLine);
        }
    }

    /// <summary>
    /// Draws one copied original SeString through Dalamud's ImGui renderer.
    /// </summary>
    /// <param name="presentation">The owned original-text payload to draw.</param>
    /// <returns><see langword="true" /> when the rich payload was drawn.</returns>
    private static bool DrawRichOverlayText(
        RichOriginalTextPresentation presentation)
    {
        if (!presentation.TryGetSeStringPayload(out var payload))
        {
            return false;
        }

        try
        {
            var drawParams = new SeStringDrawParams
            {
                Font = ImGui.GetFont(),
                ScreenOffset = ImGui.GetCursorScreenPos(),
                FontSize = ImGui.GetFontSize(),
                WrapWidth = Math.Max(1f, ImGui.GetContentRegionAvail().X),
            };
            ImGuiHelpers.SeStringWrapped(payload.Span, drawParams);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Splits one logical overlay line into visual lines that can be aligned
    /// individually without hyphenating words.
    /// </summary>
    /// <param name="line">The logical source line.</param>
    /// <param name="availableWidth">The available content width.</param>
    /// <param name="measureTextWidth">The active-font text measurement delegate.</param>
    /// <returns>The visual lines to draw.</returns>
    internal static IReadOnlyList<string> SplitOverlayLineForAlignment(
        string line,
        float availableWidth,
        Func<string, float> measureTextWidth)
    {
        if (string.IsNullOrWhiteSpace(line) ||
            availableWidth <= 0f ||
            measureTextWidth(line) <= availableWidth)
        {
            return [line];
        }

        var words = line.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= 1)
        {
            return [line];
        }

        var visualLines = new List<string>();
        var currentLine = string.Empty;
        foreach (var word in words)
        {
            if (string.IsNullOrEmpty(currentLine))
            {
                currentLine = word;
                continue;
            }

            var candidateLine = $"{currentLine} {word}";
            if (measureTextWidth(candidateLine) <= availableWidth)
            {
                currentLine = candidateLine;
                continue;
            }

            visualLines.Add(currentLine);
            currentLine = word;
        }

        if (!string.IsNullOrEmpty(currentLine))
        {
            visualLines.Add(currentLine);
        }

        return visualLines.Count > 0 ? visualLines : [line];
    }

    /// <summary>
    /// Calculates the horizontal text offset for the requested alignment.
    /// </summary>
    /// <param name="availableWidth">The available content width.</param>
    /// <param name="contentWidth">The measured content width.</param>
    /// <param name="centerAligned">Whether the surface explicitly centers text.</param>
    /// <param name="rightAligned">Whether the language presentation requests right alignment.</param>
    /// <returns>The horizontal offset to apply before drawing the content.</returns>
    internal static float CalculateHorizontalTextOffset(
        float availableWidth,
        float contentWidth,
        bool centerAligned,
        bool rightAligned)
    {
        if (availableWidth <= 0f || contentWidth >= availableWidth)
        {
            return 0f;
        }

        if (centerAligned)
        {
            return Math.Max(0f, (availableWidth - contentWidth) * 0.5f);
        }

        return rightAligned
            ? Math.Max(0f, availableWidth - contentWidth)
            : 0f;
    }

    /// <summary>
    /// Draws a texture-backed text block with its requested horizontal alignment.
    /// </summary>
    /// <param name="block">The texture-backed text block to draw.</param>
    /// <param name="centerAligned">Whether to center the block.</param>
    private static void DrawRenderedTextBlock(
        RenderedTextBlock block,
        bool centerAligned)
    {
        if (block.Texture == null)
        {
            return;
        }

        var availableWidth = ImGui.GetContentRegionAvail().X;
        if (availableWidth > 0f && block.MeasuredSize.X < availableWidth)
        {
            var offset = CalculateHorizontalTextOffset(
                availableWidth,
                block.MeasuredSize.X,
                centerAligned,
                block.RightAligned);
            if (offset > 0f)
            {
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
            }
        }

        ImGui.Image(block.Texture.Handle, block.MeasuredSize);
    }

    /// <summary>
    /// Determines whether a surface centers its displayed text.
    /// </summary>
    /// <param name="surfaceId">The overlay surface identifier.</param>
    /// <returns><see langword="true" /> when text is centered.</returns>
    internal static bool ShouldCenterOverlayText(TranslationOverlaySurfaceId surfaceId)
    {
        return surfaceId == TranslationOverlaySurfaceId.TalkSubtitle ||
               surfaceId is TranslationOverlaySurfaceId.TextGimmickHint
                   or TranslationOverlaySurfaceId.WideTextToast
                   or TranslationOverlaySurfaceId.ErrorToast
                   or TranslationOverlaySurfaceId.AreaToast
                   or TranslationOverlaySurfaceId.ClassChangeToast
                   or TranslationOverlaySurfaceId.QuestToast
                   or TranslationOverlaySurfaceId.NamePlate;
    }

    /// <summary>
    /// Normalizes configured font scales to the supported render range.
    /// </summary>
    /// <param name="configuredFontScale">The configured scale.</param>
    /// <returns>The effective positive font scale.</returns>
    private static float GetEffectiveOverlayFontScale(float configuredFontScale)
    {
        return Math.Clamp(configuredFontScale, 0.25f, 3f);
    }

    /// <summary>
    /// Creates a result for a skipped render pass.
    /// </summary>
    /// <param name="presentationMode">The resolved or default presentation mode.</param>
    /// <returns>A not-drawn result.</returns>
    private static TranslationOverlayRenderResult NotDrawn(
        TextPresentationBackendKind presentationMode = TextPresentationBackendKind.PlainImGui)
    {
        return new TranslationOverlayRenderResult(
            false,
            Vector2.Zero,
            Vector2.Zero,
            presentationMode);
    }
}
