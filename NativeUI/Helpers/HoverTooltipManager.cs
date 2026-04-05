// <copyright file="HoverTooltipManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Concurrent;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Tracks hover rectangles and draws DelvUI-style tooltips on the cursor.
/// </summary>
public sealed class HoverTooltipManager
{
    private readonly ConcurrentDictionary<string, HoverTooltipEntry> entries = new();
    private readonly TimeSpan staleEntryLifetime = TimeSpan.FromSeconds(8);

    /// <summary>
    ///     Registers or updates a tooltip target.
    /// </summary>
    public void Register(
        string key,
        Vector2 topLeft,
        Vector2 bottomRight,
        string title,
        string body,
        bool enabled = true)
    {
        this.entries[key] = new HoverTooltipEntry(
            topLeft,
            bottomRight,
            title,
            body,
            enabled,
            DateTime.UtcNow);
    }

    /// <summary>
    ///     Removes tooltip targets by exact key.
    /// </summary>
    public void Remove(string key)
    {
        this.entries.TryRemove(key, out _);
    }

    /// <summary>
    ///     Clears all registered targets.
    /// </summary>
    public void Clear()
    {
        this.entries.Clear();
    }

    /// <summary>
    ///     Draws a tooltip for the first hovered target.
    /// </summary>
    public void Draw()
    {
        this.RemoveStaleEntries();

        if (this.entries.Count == 0)
        {
            return;
        }

        var mousePosition = ImGui.GetMousePos();
        foreach (var entry in this.entries.Values)
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

            ImGui.SetNextWindowBgAlpha(0.95f);
            ImGui.BeginTooltip();
            try
            {
                if (!string.IsNullOrWhiteSpace(entry.Title))
                {
                    ImGui.TextUnformatted(entry.Title);
                    ImGui.Separator();
                }

                ImGui.TextWrapped(string.IsNullOrWhiteSpace(entry.Body)
                    ? string.Empty
                    : entry.Body);
            }
            finally
            {
                ImGui.EndTooltip();
            }

            break;
        }
    }

    private void RemoveStaleEntries()
    {
        var cutoff = DateTime.UtcNow - this.staleEntryLifetime;
        foreach (var (key, entry) in this.entries)
        {
            if (entry.LastUpdatedUtc >= cutoff)
            {
                continue;
            }

            this.entries.TryRemove(key, out _);
        }
    }

    private sealed record HoverTooltipEntry(
        Vector2 TopLeft,
        Vector2 BottomRight,
        string Title,
        string Body,
        bool Enabled,
        DateTime LastUpdatedUtc);
}
