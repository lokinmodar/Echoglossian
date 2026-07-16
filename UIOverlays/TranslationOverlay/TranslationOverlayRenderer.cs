// <copyright file="TranslationOverlayRenderer.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Runtime;

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
        overlay.Semaphore.Wait();
        try
        {
            overlayText = overlay.CurrentText;
            shouldDraw = !string.IsNullOrEmpty(overlayText) &&
                         overlayText != Resources.WaitingForTranslation;
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
        var effectiveFontScale = GetEffectiveOverlayFontScale(config.FontScale);
        var shouldCenterOverlayText = ShouldCenterOverlayText(config.SurfaceId);
        var shouldRightAlignOverlayText =
            LanguagePresentationPolicy.ShouldRightAlign(this.configuration.Lang);
        var horizontalPadding = ImGui.GetStyle().WindowPadding.X * 2f;
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
            shouldCenterOverlayText);
        var backendKind = TextPresentationResolver.ResolveBackendKind(textRequest);
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
        var beganWindow = false;
        IDisposable? fontScope = null;
        try
        {
            ImGui.PushStyleColor(
                ImGuiCol.Text,
                new Vector4(config.TextColor.X, config.TextColor.Y, config.TextColor.Z, 1f));
            pushedStyleColor = true;
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
                    if (pushedStyleColor)
                    {
                        ImGui.PopStyleColor();
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
                    ? ImGui.GetTextLineHeight()
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
            TranslationOverlaySurfaceId.TextGimmickHint => this.configuration.TextGimmickHintTranslationDisplayMode,
            TranslationOverlaySurfaceId.WideTextToast => ToastGuiSupportedToastPolicy.UseSupportedNormalToastRuntime(this.configuration)
                ? ToastGuiSupportedToastPolicy.GetNormalToastDisplayMode(this.configuration)
                : this.configuration.WideTextToastTranslationDisplayMode,
            TranslationOverlaySurfaceId.ErrorToast => this.configuration.ErrorToastTranslationDisplayMode,
            TranslationOverlaySurfaceId.AreaToast => this.configuration.AreaToastTranslationDisplayMode,
            TranslationOverlaySurfaceId.ClassChangeToast => this.configuration.ClassChangeToastTranslationDisplayMode,
            TranslationOverlaySurfaceId.QuestToast => this.configuration.QuestToastTranslationDisplayMode,
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
            : text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
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
        var lineWidth = ImGui.CalcTextSize(line).X;
        if ((!centerAligned && !rightAligned) || availableWidth <= 0f ||
            lineWidth >= availableWidth)
        {
            ImGui.TextWrapped(line);
            return;
        }

        var offset = rightAligned
            ? Math.Max(0f, availableWidth - lineWidth)
            : Math.Max(0f, (availableWidth - lineWidth) * 0.5f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
        ImGui.TextUnformatted(line);
    }

    /// <summary>
    /// Draws a texture-backed text block with its requested horizontal alignment.
    /// </summary>
    /// <param name="block">The texture-backed text block to draw.</param>
    /// <param name="centerAligned">Whether to center a non-RTL block.</param>
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
            var offset = block.RightAligned
                ? Math.Max(0f, availableWidth - block.MeasuredSize.X)
                : centerAligned
                    ? Math.Max(0f, (availableWidth - block.MeasuredSize.X) * 0.5f)
                    : 0f;
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
    private static bool ShouldCenterOverlayText(TranslationOverlaySurfaceId surfaceId)
    {
        return surfaceId == TranslationOverlaySurfaceId.TalkSubtitle ||
               surfaceId is TranslationOverlaySurfaceId.TextGimmickHint
                   or TranslationOverlaySurfaceId.WideTextToast
                   or TranslationOverlaySurfaceId.ErrorToast
                   or TranslationOverlaySurfaceId.AreaToast
                   or TranslationOverlaySurfaceId.ClassChangeToast
                   or TranslationOverlaySurfaceId.QuestToast;
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
