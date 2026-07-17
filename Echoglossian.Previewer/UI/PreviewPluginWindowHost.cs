// <copyright file="PreviewPluginWindowHost.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Bindings.ImGui;

using Echoglossian.DBManagerUI;
using Echoglossian.EFCoreSqlite;
using Echoglossian.NativeUI.AddonHandlers.Talk;
using Echoglossian.PluginUI;
using Echoglossian.Properties;

using System.Drawing;
using System.Numerics;

namespace Echoglossian.Previewer.UI;

/// <summary>
/// Owns and draws the real plugin windows inside the standalone ImGui host.
/// </summary>
internal sealed class PreviewPluginWindowHost : IDisposable
{
    private const int RequiredCaptureStableFrames = 3;
    private const int MaximumCaptureObservationFrames = 180;
    private readonly PluginConfigWindowRenderer configWindowRenderer;
    private readonly EchoglossianDbContext? dbContext;
    private readonly DbEditorWindow? dbEditorWindow;
    private readonly TranslatorMetricsWindow translatorMetricsWindow;
    private readonly PluginConfigWindowContext configWindowContext;
    private readonly PreviewCaptureStabilityTracker captureStabilityTracker = new(
        RequiredCaptureStableFrames,
        MaximumCaptureObservationFrames);
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
                    "Preview mode does not retranslate live dialogue.")),
            runtimeActionsAvailable: false);
    }

    /// <summary>
    /// Gets a value indicating whether a database snapshot is available.
    /// </summary>
    internal bool DbManagerAvailable => this.dbEditorWindow is not null;

    /// <summary>
    /// Gets a value indicating whether the active target failed to stabilize.
    /// </summary>
    internal bool CaptureFailed => this.captureStabilityTracker.CaptureFailed;

    /// <summary>
    /// Starts deterministic layout and stabilization for one plugin window.
    /// </summary>
    /// <param name="target">The requested plugin-window target.</param>
    internal void BeginCapture(PreviewCaptureTarget target)
    {
        if (!IsPluginWindowTarget(target))
        {
            throw new ArgumentException(
                "Capture stabilization requires a plugin-window target.",
                nameof(target));
        }

        if (target == PreviewCaptureTarget.DbManagerWindow && !this.DbManagerAvailable)
        {
            throw new InvalidOperationException(
                "DbManagerWindow capture requires an available preview database snapshot.");
        }

        this.captureStabilityTracker.Begin(target);
    }

    /// <summary>
    /// Ends capture stabilization and releases deterministic window layout.
    /// </summary>
    internal void EndCapture()
    {
        this.captureStabilityTracker.End();
    }

    /// <summary>
    /// Draws all plugin windows requested by the workbench state.
    /// </summary>
    /// <param name="state">The shared workbench state.</param>
    internal void Draw(PreviewWorkbenchState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.ConfigWindowOpen)
        {
            this.ApplyCaptureLayout(PreviewCaptureTarget.ConfigWindow);
            var configOpen = true;
            this.configWindowRenderer.Draw(this.configWindowContext, ref configOpen);
            this.EnforceCaptureLayout(
                PreviewCaptureTarget.ConfigWindow,
                $"{Resources.ConfigWindowTitle} - Plugin Version: " +
                this.configWindowContext.PluginVersion);
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
            this.ApplyCaptureLayout(PreviewCaptureTarget.DbManagerWindow);
            this.dbEditorWindow.IsOpen = state.DbManagerWindowOpen;
            this.dbEditorWindow.Draw();
            this.EnforceCaptureLayout(
                PreviewCaptureTarget.DbManagerWindow,
                Resources.EchoglossianDBEditor);
        }

        SynchronizeDbManagerState(state, this.dbEditorWindow);

        this.translatorMetricsWindow.IsOpen = state.TranslatorMetricsWindowOpen;
        this.ApplyCaptureLayout(PreviewCaptureTarget.TranslatorMetricsWindow);
        this.translatorMetricsWindow.Draw();
        this.EnforceCaptureLayout(
            PreviewCaptureTarget.TranslatorMetricsWindow,
            Resources.TranslatorDebuggerWindowTitle);
        state.TranslatorMetricsWindowOpen = this.translatorMetricsWindow.IsOpen;
        SynchronizeDbManagerState(state, this.dbEditorWindow);

        if (this.captureStabilityTracker.Target is { } captureTarget &&
            IsPluginWindowTarget(captureTarget))
        {
            this.captureStabilityTracker.Observe(
                captureTarget,
                this.TryGetValidCaptureBounds(captureTarget));
        }
    }

    /// <summary>
    /// Copies the real DB window state after dependent windows may have opened it.
    /// </summary>
    /// <param name="state">The shared workbench state.</param>
    /// <param name="dbEditorWindow">The optional real DB editor window.</param>
    internal static void SynchronizeDbManagerState(
        PreviewWorkbenchState state,
        DbEditorWindow? dbEditorWindow)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.DbManagerWindowOpen = dbEditorWindow?.IsOpen ?? false;
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

    /// <summary>
    /// Gets stable integer capture bounds for a plugin window.
    /// </summary>
    /// <param name="target">The requested plugin-window target.</param>
    /// <returns>The stable capture rectangle, or <see langword="null" /> while unavailable.</returns>
    internal Rectangle? TryGetStableCrop(PreviewCaptureTarget target)
    {
        return this.captureStabilityTracker.TryGetStableBounds(
            target,
            out var bounds) &&
            IsCaptureBoundsValid(target, this.IsCaptureTargetVisible(target), bounds)
            ? bounds
            : null;
    }

    /// <summary>
    /// Determines whether observed bounds represent the visible target at its
    /// deterministic capture dimensions.
    /// </summary>
    /// <param name="target">The plugin-window capture target.</param>
    /// <param name="isVisible"><see langword="true" /> when the target window remains open and visible.</param>
    /// <param name="bounds">The observed integer window bounds.</param>
    /// <returns><see langword="true" /> when the bounds are valid for capture; otherwise, <see langword="false" />.</returns>
    internal static bool IsCaptureBoundsValid(
        PreviewCaptureTarget target,
        bool isVisible,
        Rectangle? bounds)
    {
        var expectedSize = GetCaptureLayoutSize(target);
        return IsPluginWindowTarget(target) &&
            isVisible &&
            expectedSize.Width > 0 &&
            expectedSize.Height > 0 &&
            bounds is { } candidate &&
            candidate.Width == expectedSize.Width &&
            candidate.Height == expectedSize.Height;
    }

    /// <summary>
    /// Gets the deterministic logical size assigned to a plugin-window target.
    /// </summary>
    /// <param name="target">The requested capture target.</param>
    /// <returns>The fixed logical window size.</returns>
    internal static Size GetCaptureLayoutSize(PreviewCaptureTarget target)
    {
        return target switch
        {
            PreviewCaptureTarget.ConfigWindow => new Size(1000, 900),
            PreviewCaptureTarget.DbManagerWindow => new Size(1200, 800),
            PreviewCaptureTarget.TranslatorMetricsWindow => new Size(1100, 480),
            _ => Size.Empty,
        };
    }

    /// <summary>
    /// Determines whether a capture target identifies a real plugin window.
    /// </summary>
    /// <param name="target">The capture target to inspect.</param>
    /// <returns><see langword="true" /> for plugin-window targets; otherwise, <see langword="false" />.</returns>
    internal static bool IsPluginWindowTarget(PreviewCaptureTarget target)
    {
        return target is PreviewCaptureTarget.ConfigWindow or
            PreviewCaptureTarget.DbManagerWindow or
            PreviewCaptureTarget.TranslatorMetricsWindow;
    }

    /// <summary>
    /// Applies fixed position and size to the active capture target on every frame.
    /// </summary>
    /// <param name="target">The window about to be drawn.</param>
    private void ApplyCaptureLayout(PreviewCaptureTarget target)
    {
        if (this.captureStabilityTracker.Target != target)
        {
            return;
        }

        var size = GetCaptureLayoutSize(target);
        ImGui.SetNextWindowPos(Vector2.Zero, ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(size.Width, size.Height), ImGuiCond.Always);
    }

    /// <summary>
    /// Gets current bounds only when the requested target remains visible at
    /// its fixed capture dimensions.
    /// </summary>
    /// <param name="target">The requested plugin-window target.</param>
    /// <returns>The valid capture bounds, or <see langword="null" /> when unavailable.</returns>
    private Rectangle? TryGetValidCaptureBounds(PreviewCaptureTarget target)
    {
        var bounds = this.TryGetCrop(target);
        return IsCaptureBoundsValid(target, this.IsCaptureTargetVisible(target), bounds)
            ? bounds
            : null;
    }

    /// <summary>
    /// Determines whether the requested plugin window remains open after its
    /// current frame has been drawn.
    /// </summary>
    /// <param name="target">The plugin-window target to inspect.</param>
    /// <returns><see langword="true" /> when the target remains visible; otherwise, <see langword="false" />.</returns>
    private bool IsCaptureTargetVisible(PreviewCaptureTarget target)
    {
        return target switch
        {
            PreviewCaptureTarget.ConfigWindow => this.configWindowBounds is not null,
            PreviewCaptureTarget.DbManagerWindow => this.dbEditorWindow?.IsOpen == true,
            PreviewCaptureTarget.TranslatorMetricsWindow => this.translatorMetricsWindow.IsOpen,
            _ => false,
        };
    }

    /// <summary>
    /// Enforces fixed geometry after a hosted window's own first-use hints run.
    /// </summary>
    /// <param name="target">The window that was drawn.</param>
    /// <param name="windowName">The exact ImGui window name.</param>
    private void EnforceCaptureLayout(
        PreviewCaptureTarget target,
        string windowName)
    {
        if (this.captureStabilityTracker.Target != target)
        {
            return;
        }

        var size = GetCaptureLayoutSize(target);
        ImGui.SetWindowPos(windowName, Vector2.Zero, ImGuiCond.Always);
        ImGui.SetWindowSize(
            windowName,
            new Vector2(size.Width, size.Height),
            ImGuiCond.Always);
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

/// <summary>
/// Tracks consecutive non-empty plugin-window bounds for deterministic capture.
/// </summary>
internal sealed class PreviewCaptureStabilityTracker
{
    private readonly int requiredStableFrames;
    private readonly int maximumObservationFrames;
    private PreviewCaptureTarget? target;
    private PreviewCaptureTarget? completedTarget;
    private Rectangle? lastBounds;
    private Rectangle? completedBounds;
    private int stableFrameCount;
    private int observationFrameCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewCaptureStabilityTracker" /> class.
    /// </summary>
    /// <param name="requiredStableFrames">The consecutive matching bounds required.</param>
    /// <param name="maximumObservationFrames">The maximum frames allowed before failure.</param>
    internal PreviewCaptureStabilityTracker(
        int requiredStableFrames,
        int maximumObservationFrames)
    {
        if (requiredStableFrames <= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(requiredStableFrames));
        }

        if (maximumObservationFrames < requiredStableFrames)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumObservationFrames));
        }

        this.requiredStableFrames = requiredStableFrames;
        this.maximumObservationFrames = maximumObservationFrames;
    }

    /// <summary>
    /// Gets a value indicating whether the active capture exhausted its observation frames.
    /// </summary>
    internal bool CaptureFailed =>
        this.target is not null &&
        this.observationFrameCount >= this.maximumObservationFrames &&
        this.stableFrameCount < this.requiredStableFrames;

    /// <summary>
    /// Gets the active plugin-window capture target.
    /// </summary>
    internal PreviewCaptureTarget? Target => this.target;

    /// <summary>
    /// Resets tracking for a new plugin-window target.
    /// </summary>
    /// <param name="target">The plugin-window capture target.</param>
    internal void Begin(PreviewCaptureTarget target)
    {
        this.target = target;
        this.completedTarget = null;
        this.lastBounds = null;
        this.completedBounds = null;
        this.stableFrameCount = 0;
        this.observationFrameCount = 0;
    }

    /// <summary>
    /// Clears all active capture and stabilization state.
    /// </summary>
    internal void End()
    {
        if (this.target is { } activeTarget &&
            this.stableFrameCount >= this.requiredStableFrames &&
            this.lastBounds is { } stableBounds)
        {
            this.completedTarget = activeTarget;
            this.completedBounds = stableBounds;
        }
        else
        {
            this.completedTarget = null;
            this.completedBounds = null;
        }

        this.target = null;
        this.lastBounds = null;
        this.stableFrameCount = 0;
        this.observationFrameCount = 0;
    }

    /// <summary>
    /// Observes one frame of bounds for the active target.
    /// </summary>
    /// <param name="target">The observed target.</param>
    /// <param name="bounds">The current non-empty bounds, if available.</param>
    internal void Observe(PreviewCaptureTarget target, Rectangle? bounds)
    {
        if (this.target != target || this.CaptureFailed)
        {
            return;
        }

        this.observationFrameCount++;
        if (bounds is not { Width: > 0, Height: > 0 } currentBounds)
        {
            this.lastBounds = null;
            this.stableFrameCount = 0;
            return;
        }

        if (this.lastBounds == currentBounds)
        {
            this.stableFrameCount++;
            return;
        }

        this.lastBounds = currentBounds;
        this.stableFrameCount = 1;
    }

    /// <summary>
    /// Gets stable bounds after enough consecutive matching observations.
    /// </summary>
    /// <param name="target">The requested capture target.</param>
    /// <param name="bounds">The stable bounds when this method returns successfully.</param>
    /// <returns><see langword="true" /> when stable bounds are ready; otherwise, <see langword="false" />.</returns>
    internal bool TryGetStableBounds(
        PreviewCaptureTarget target,
        out Rectangle bounds)
    {
        if (this.target == target &&
            this.stableFrameCount >= this.requiredStableFrames &&
            this.lastBounds is { } stableBounds)
        {
            bounds = stableBounds;
            return true;
        }

        if (this.completedTarget == target &&
            this.completedBounds is { } completedStableBounds)
        {
            bounds = completedStableBounds;
            return true;
        }

        bounds = Rectangle.Empty;
        return false;
    }
}
