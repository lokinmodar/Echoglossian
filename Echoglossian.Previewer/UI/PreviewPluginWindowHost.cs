// <copyright file="PreviewPluginWindowHost.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.DBManagerUI;
using Echoglossian.EFCoreSqlite;
using Echoglossian.NativeUI.AddonHandlers.Talk;
using Echoglossian.PluginUI;

using System.Drawing;

namespace Echoglossian.Previewer.UI;

/// <summary>
/// Owns and draws the real plugin windows inside the standalone ImGui host.
/// </summary>
internal sealed class PreviewPluginWindowHost : IDisposable
{
    private readonly PluginConfigWindowRenderer configWindowRenderer;
    private readonly EchoglossianDbContext? dbContext;
    private readonly DbEditorWindow? dbEditorWindow;
    private readonly TranslatorMetricsWindow translatorMetricsWindow;
    private readonly PluginConfigWindowContext configWindowContext;
    private RectangleF? configWindowBounds;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewPluginWindowHost" /> class.
    /// </summary>
    /// <param name="configWindowRenderer">The shared real config-window renderer.</param>
    /// <param name="configWindowContext">Preview-safe config-window dependencies.</param>
    /// <param name="dbContext">The optional preview-owned database context.</param>
    /// <param name="configuration">The preview-owned editable configuration.</param>
    internal PreviewPluginWindowHost(
        PluginConfigWindowRenderer configWindowRenderer,
        PluginConfigWindowContext configWindowContext,
        EchoglossianDbContext? dbContext,
        Config configuration)
    {
        this.configWindowRenderer = configWindowRenderer ??
            throw new ArgumentNullException(nameof(configWindowRenderer));
        this.configWindowContext = configWindowContext ??
            throw new ArgumentNullException(nameof(configWindowContext));
        this.dbContext = dbContext;
        this.dbEditorWindow = dbContext is null
            ? null
            : new DbEditorWindow(dbContext, static _ => { });
        this.translatorMetricsWindow = new TranslatorMetricsWindow(
            configuration ?? throw new ArgumentNullException(nameof(configuration)),
            table => this.dbEditorWindow?.OpenAndSelectTable(table),
            () => Task.FromResult(
                new VisibleDialogueRetranslationResult(
                    false,
                    false,
                    null,
                    "Preview",
                    "Preview mode does not retranslate live dialogue.")));
    }

    /// <summary>
    /// Gets a value indicating whether a database snapshot is available.
    /// </summary>
    internal bool DbManagerAvailable => this.dbEditorWindow is not null;

    /// <summary>
    /// Draws all plugin windows requested by the workbench state.
    /// </summary>
    /// <param name="state">The shared workbench state.</param>
    internal void Draw(PreviewWorkbenchState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.ConfigWindowOpen)
        {
            var configOpen = true;
            this.configWindowRenderer.Draw(this.configWindowContext, ref configOpen);
            this.configWindowBounds = configOpen
                ? this.configWindowRenderer.LastWindowBounds
                : null;
            state.ConfigWindowOpen = configOpen;
        }
        else
        {
            this.configWindowBounds = null;
        }

        if (this.dbEditorWindow is not null)
        {
            this.dbEditorWindow.IsOpen = state.DbManagerWindowOpen;
            this.dbEditorWindow.Draw();
            state.DbManagerWindowOpen = this.dbEditorWindow.IsOpen;
        }
        else
        {
            state.DbManagerWindowOpen = false;
        }

        this.translatorMetricsWindow.IsOpen = state.TranslatorMetricsWindowOpen;
        this.translatorMetricsWindow.Draw();
        state.TranslatorMetricsWindowOpen = this.translatorMetricsWindow.IsOpen;
    }

    /// <summary>
    /// Gets the integer capture bounds for a rendered plugin window.
    /// </summary>
    /// <param name="target">The requested plugin-window target.</param>
    /// <returns>The capture rectangle, or <see langword="null" /> when unavailable.</returns>
    internal Rectangle? TryGetCrop(PreviewCaptureTarget target)
    {
        var bounds = target switch
        {
            PreviewCaptureTarget.ConfigWindow => this.configWindowBounds,
            PreviewCaptureTarget.DbManagerWindow => this.dbEditorWindow?.LastWindowBounds,
            PreviewCaptureTarget.TranslatorMetricsWindow => this.translatorMetricsWindow.LastWindowBounds,
            _ => null,
        };

        if (bounds is not { Width: > 0, Height: > 0 })
        {
            return null;
        }

        return Rectangle.FromLTRB(
            (int)MathF.Floor(bounds.Value.Left),
            (int)MathF.Floor(bounds.Value.Top),
            (int)MathF.Ceiling(bounds.Value.Right),
            (int)MathF.Ceiling(bounds.Value.Bottom));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.dbContext?.Dispose();
        this.disposed = true;
    }
}
