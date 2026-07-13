// <copyright file="HoverTooltipManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Concurrent;
using Echoglossian.PluginUI.Helpers;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Tracks hover rectangles and draws DelvUI-style tooltips on the cursor.
/// </summary>
public sealed class HoverTooltipManager
{
    private const int TooltipWrapLimit = 80;

    private readonly ConcurrentDictionary<string, HoverTooltipEntry> entries = new();
    private readonly TimeSpan staleEntryLifetime = TimeSpan.FromSeconds(30);
    private readonly Config config;
    private readonly UINewFontHandler fontHandler;
    private readonly RtlTexturePresentationService rtlTexturePresentationService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HoverTooltipManager" />
    ///     class.
    /// </summary>
    /// <param name="config">
    ///     The live plugin configuration used to style hover tooltips.
    /// </param>
    internal HoverTooltipManager(
        Config config,
        UINewFontHandler fontHandler,
        RtlTexturePresentationService rtlTexturePresentationService)
    {
        this.config = config;
        this.fontHandler = fontHandler;
        this.rtlTexturePresentationService = rtlTexturePresentationService;
    }

    /// <summary>
    ///     Registers or updates a tooltip target.
    /// </summary>
    public void Register(
        string key,
        Vector2 topLeft,
        Vector2 bottomRight,
        string title,
        string body,
        bool enabled = true,
        bool useGeneralFont = false)
    {
        var newEntry = new HoverTooltipEntry(
            topLeft,
            bottomRight,
            title,
            body,
            enabled,
            useGeneralFont,
            DateTime.UtcNow);

        this.entries[key] = newEntry;
    }

    /// <summary>
    ///     Removes tooltip targets by exact key.
    /// </summary>
    public void Remove(string key)
    {
        this.entries.TryRemove(key, out _);
    }

    /// <summary>
    ///     Removes tooltip targets whose keys start with the specified prefix.
    /// </summary>
    /// <param name="prefix">The key prefix to remove.</param>
    public void RemoveByPrefix(string prefix)
    {
        foreach (var (key, _) in this.entries)
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            this.entries.TryRemove(key, out _);
        }
    }

    /// <summary>
    ///     Refreshes the lifetime of tooltip targets whose keys start with the
    ///     specified prefix without rebuilding their geometry or text.
    /// </summary>
    /// <param name="prefix">The key prefix to refresh.</param>
    public void TouchByPrefix(string prefix)
    {
        foreach (var (key, entry) in this.entries)
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            this.entries[key] = entry with
            {
                LastUpdatedUtc = DateTime.UtcNow,
            };
        }
    }

    /// <summary>
    ///     Clears all registered targets.
    /// </summary>
    public void Clear()
    {
        this.entries.Clear();
        PluginRuntimeLog.Debug("[HoverTooltipManager] Cleared hover tooltip entries.");
    }

    /// <summary>
    ///     Draws a tooltip for the first hovered target.
    /// </summary>
    public void Draw()
    {
        if (this.entries.Count == 0)
        {
            return;
        }

        var mousePosition = ImGui.GetMousePos();
        string? hoveredKey = null;
        HoverTooltipEntry? hoveredEntry = null;
        var hoveredArea = float.MaxValue;
        foreach (var (key, entry) in this.entries)
        {
            if (!entry.Enabled)
            {
                continue;
            }

            if (mousePosition.X < entry.TopLeft.X ||
                mousePosition.Y < entry.TopLeft.Y ||
                mousePosition.X > entry.BottomRight.X ||
                mousePosition.Y > entry.BottomRight.Y)
            {
                continue;
            }

            var width = Math.Max(1f, entry.BottomRight.X - entry.TopLeft.X);
            var height = Math.Max(1f, entry.BottomRight.Y - entry.TopLeft.Y);
            var area = width * height;
            if (hoveredEntry != null && area >= hoveredArea)
            {
                continue;
            }

            hoveredKey = key;
            hoveredEntry = entry;
            hoveredArea = area;
        }

        this.RemoveStaleEntries(hoveredKey);

        if (hoveredKey == null || hoveredEntry == null)
        {
            return;
        }

        this.entries[hoveredKey] = hoveredEntry with
        {
            LastUpdatedUtc = DateTime.UtcNow,
        };

        var backgroundColor = new Vector4(
            this.config.HoverTooltipBackgroundColor.X,
            this.config.HoverTooltipBackgroundColor.Y,
            this.config.HoverTooltipBackgroundColor.Z,
            this.config.HoverTooltipBackgroundOpacity);
        var textColor = new Vector4(
            this.config.HoverTooltipTextColor.X,
            this.config.HoverTooltipTextColor.Y,
            this.config.HoverTooltipTextColor.Z,
            1f);
        var shouldRightAlign =
            LanguagePresentationPolicy.ShouldRightAlign(this.config.Lang);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, backgroundColor);
        ImGui.PushStyleColor(ImGuiCol.Text, textColor);
        var useTexturePresentation =
            LanguagePresentationPolicy.UsesTexturePresentation(
                this.config.Lang);
        if (!useTexturePresentation && hoveredEntry.UseGeneralFont)
        {
            this.fontHandler.GeneralFontHandle.Push();
        }
        else if (!useTexturePresentation)
        {
            this.fontHandler.LanguageFontHandle.Push();
        }

        ImGui.BeginTooltip();
        try
        {
            if (useTexturePresentation)
            {
                var titleBlock = this.TryRenderTooltipTextBlock(
                    hoveredEntry.Title,
                    hoveredEntry.UseGeneralFont,
                    textColor);
                var bodyBlock = this.TryRenderTooltipTextBlock(
                    hoveredEntry.Body,
                    hoveredEntry.UseGeneralFont,
                    textColor);

                if (titleBlock == null && bodyBlock == null)
                {
                    return;
                }

                if (titleBlock != null)
                {
                    DrawRenderedTooltipBlock(titleBlock);
                    if (bodyBlock != null)
                    {
                        ImGui.Separator();
                    }
                }

                if (bodyBlock != null)
                {
                    DrawRenderedTooltipBlock(bodyBlock);
                }

                return;
            }

            var title = WrapTooltipText(hoveredEntry.Title);
            var body = WrapTooltipText(hoveredEntry.Body);

            if (!string.IsNullOrWhiteSpace(title))
            {
                DrawPlainTooltipText(title, shouldRightAlign);
                ImGui.Separator();
            }

            DrawPlainTooltipText(body, shouldRightAlign);
        }
        finally
        {
            ImGui.EndTooltip();
            if (!useTexturePresentation && hoveredEntry.UseGeneralFont)
            {
                this.fontHandler.GeneralFontHandle.Pop();
            }
            else if (!useTexturePresentation)
            {
                this.fontHandler.LanguageFontHandle.Pop();
            }

            ImGui.PopStyleColor(2);
        }
    }

    private void RemoveStaleEntries(string? preserveKey)
    {
        var cutoff = DateTime.UtcNow - this.staleEntryLifetime;
        foreach (var (key, entry) in this.entries)
        {
            if (!string.IsNullOrWhiteSpace(preserveKey) &&
                string.Equals(key, preserveKey, StringComparison.Ordinal))
            {
                continue;
            }

            if (entry.LastUpdatedUtc >= cutoff)
            {
                continue;
            }

            this.entries.TryRemove(key, out _);
        }
    }

    private static string WrapTooltipText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalizedText = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var paragraphs = normalizedText.Split('\n');
        var builder = new System.Text.StringBuilder();

        for (var paragraphIndex = 0; paragraphIndex < paragraphs.Length; paragraphIndex++)
        {
            if (paragraphIndex > 0)
            {
                builder.AppendLine();
            }

            var paragraph = paragraphs[paragraphIndex].Trim();
            if (paragraph.Length == 0)
            {
                continue;
            }

            var words = paragraph.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);
            var line = new System.Text.StringBuilder();

            foreach (var word in words)
            {
                if (line.Length == 0)
                {
                    line.Append(word);
                    continue;
                }

                if (line.Length + 1 + word.Length > TooltipWrapLimit)
                {
                    builder.AppendLine(line.ToString());
                    line.Clear();
                    line.Append(word);
                    continue;
                }

                line.Append(' ').Append(word);
            }

            if (line.Length > 0)
            {
                builder.Append(line);
            }
        }

        return builder.ToString();
    }

    private static void DrawRenderedTooltipBlock(RenderedTextBlock block)
    {
        if (block.Texture == null)
        {
            return;
        }

        var availableWidth = ImGui.GetContentRegionAvail().X;
        if (block.RightAligned && availableWidth > block.MeasuredSize.X)
        {
            var offset = Math.Max(0f, availableWidth - block.MeasuredSize.X);
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
        }

        ImGui.Image(block.Texture.Handle, block.MeasuredSize);
    }

    private static void DrawPlainTooltipText(
        string text,
        bool rightAligned)
    {
        if (!rightAligned)
        {
            ImGui.TextUnformatted(text);
            return;
        }

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                ImGui.Spacing();
                continue;
            }

            var availableWidth = ImGui.GetContentRegionAvail().X;
            var lineWidth = ImGui.CalcTextSize(line).X;
            if (availableWidth > 0f && lineWidth < availableWidth)
            {
                var offset = Math.Max(0f, availableWidth - lineWidth);
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
            }

            ImGui.TextUnformatted(line);
        }
    }

    private RenderedTextBlock? TryRenderTooltipTextBlock(
        string text,
        bool useGeneralFont,
        Vector4 textColor)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var languageCode = LangDict.TryGetValue(this.config.Lang, out var language)
            ? language.Code
            : string.Empty;
        var fontScale =
            HoverTooltipLayoutPolicy.ResolveTextureFontScale(this.config);
        var request = new TextLayoutRequest(
            text,
            this.config.Lang,
            languageCode,
            0f,
            fontScale,
            useGeneralFont,
            textColor,
            Vector4.Zero,
            TranslationOverlaySurfaceId.ItemDetail,
            CenterAligned: false);
        var maxWidth =
            this.rtlTexturePresentationService.ResolveAdaptiveHoverTooltipMaxWidth(
                request,
                ImGui.GetMainViewport().Size.X);
        request = request with
        {
            MaxWidth = maxWidth,
        };

        return this.rtlTexturePresentationService.TryRender(request);
    }

    private sealed record HoverTooltipEntry(
        Vector2 TopLeft,
        Vector2 BottomRight,
        string Title,
        string Body,
        bool Enabled,
        bool UseGeneralFont,
        DateTime LastUpdatedUtc);
}


