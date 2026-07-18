namespace DalaMock.Core.Windows;

/// <summary>
/// Base class you can use to implement an ImGui window for use with the built-in <see cref="WindowSystem"/>.
/// </summary>
public class MockWindowHost
{
    private readonly IImGuiComponents imGuiComponents;
    private readonly ILogger<MockWindowHost> logger;
    private readonly IStringLocalizer<MockWindowHost> localizer;
    private readonly IKeyState keyState;
    private readonly IFont font;
    private readonly MockWindowHostState windowHostState;
    private const float BlurNoiseOpacity = 0.17f;
    private const float MaxBlurStrength = 14f;
    private static readonly Vector4 BlurTintMultiplier = new(158 / 255f, 158 / 255f, 158 / 255f, 25 / 255f);

    private static bool wasEscPressedLastFrame = false;

    private bool internalLastIsOpen = false;
    private bool didPushInternalAlpha = false;
    private float? internalAlpha = null;

    private float? internalBlurFactorOverride = null;

    private bool hasInitializedFromPreset = false;
    private bool windowStateDirty = true;

    private bool hasError = false;
    private Exception? lastError;

    public delegate MockWindowHost Factory(IWindow window);

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowHost"/> class.
    /// </summary>
    /// <param name="window">A plugin provided window.</param>
    public MockWindowHost(
        IWindow window,
        IImGuiComponents imGuiComponents,
        ILogger<MockWindowHost> logger,
        IStringLocalizer<MockWindowHost> localizer,
        IKeyState keyState,
        IFont font,
        MockWindowHostState windowHostState)
    {
        this.imGuiComponents = imGuiComponents;
        this.logger = logger;
        this.localizer = localizer;
        this.keyState = keyState;
        this.font = font;
        this.windowHostState = windowHostState;
        this.Window = window;
    }

    /// <summary>
    /// Flags to control window behavior.
    /// </summary>
    [Flags]
    internal enum WindowDrawFlags
    {
        /// <summary>
        /// Nothing.
        /// </summary>
        None = 0,

        /// <summary>
        /// Enable window opening/closing sound effects.
        /// </summary>
        UseSoundEffects = 1 << 0,

        /// <summary>
        /// Hook into the game's focus management.
        /// </summary>
        UseFocusManagement = 1 << 1,

        /// <summary>
        /// Enable the built-in "additional options" menu on the title bar.
        /// </summary>
        UseAdditionalOptions = 1 << 2,

        /// <summary>
        /// Do not draw non-critical animations.
        /// </summary>
        IsReducedMotion = 1 << 3,
    }

    /// <summary>
    /// Gets or sets the backing window provided by the plugin.
    /// </summary>
    public IWindow Window { get; set; }

    private bool CanShowCloseButton => this.Window.ShowCloseButton;

    /// <summary>
    /// Draw the window via ImGui.
    /// </summary>
    /// <param name="internalDrawParams">Parameters controlling window behavior.</param>
    /// <param name="persistence">Handler for window persistence data.</param>
    internal void DrawInternal(WindowDrawParameters internalDrawParams)
    {
        this.Window.PreOpenCheck();

        if (!this.Window.IsOpen)
        {
            if (this.Window.IsOpen != this.internalLastIsOpen)
            {
                this.internalLastIsOpen = this.Window.IsOpen;
                this.Window.OnClose();

                this.Window.IsFocused = false;

                if (internalDrawParams.Flags.HasFlag(WindowDrawFlags.UseSoundEffects) && !this.Window.DisableWindowSounds)
                {
                    unsafe
                    {
                        //UIGlobals.PlaySoundEffect(this.Window.OnCloseSfxId); //TODO: play sound
                    }
                }
            }

            return;
        }

        this.Window.Update();
        if (!this.Window.DrawConditions())
        {
            return;
        }

        var hasNamespace = !string.IsNullOrEmpty(this.Window.Namespace);

        if (hasNamespace)
        {
            ImGui.PushID(this.Window.Namespace);
        }

        this.PreHandlePreset();

        if (this.internalLastIsOpen != this.Window.IsOpen && this.Window.IsOpen)
        {
            this.internalLastIsOpen = this.Window.IsOpen;
            this.Window.OnOpen();

            if (internalDrawParams.Flags.HasFlag(WindowDrawFlags.UseSoundEffects) && !this.Window.DisableWindowSounds)
            {
                unsafe
                {
                    //UIGlobals.PlaySoundEffect(this.Window.OnOpenSfxId); //TODO: play sound
                }
            }
        }

        // TODO: We may have to allow for windows to configure if they should fade
        if (this.internalAlpha.HasValue)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, this.internalAlpha.Value);
            this.didPushInternalAlpha = true;
        }

        this.Window.PreDraw();
        this.ApplyConditionals();

        if (this.Window.ForceMainWindow)
        {
            ImGuiHelpers.ForceNextWindowMainViewport();
        }

        var wasFocused = this.Window.IsFocused;
        if (wasFocused && this.Window is not StyleEditorWindow)
        {
            var style = ImGui.GetStyle();
            var focusedHeaderColor = style.Colors[(int)ImGuiCol.TitleBgActive];
            ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, focusedHeaderColor);
        }

        if (this.Window.RequestFocus)
        {
            ImGui.SetNextWindowFocus();
            this.Window.RequestFocus = false;
        }

        var flags = this.Window.Flags;

        if (this.Window.IsPinned)
        {
            flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize;
        }

        // Determine window background alpha
        float effectiveWindowBgAlpha;
        {
            ref var nextWindowData = ref ImGui.GetCurrentContext().NextWindowData;
            effectiveWindowBgAlpha = ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg].W;
            if (nextWindowData.Flags.HasFlag(ImGuiNextWindowDataFlags.HasBgAlpha))
            {
                effectiveWindowBgAlpha = nextWindowData.BgAlphaVal;
            }

            if (flags.HasFlag(ImGuiWindowFlags.NoBackground))
            {
                effectiveWindowBgAlpha = 0;
            }

            effectiveWindowBgAlpha *= this.internalAlpha ?? 1f;
        }

        var windowHasBackground = effectiveWindowBgAlpha != 0f;

        var isWindowOpen = this.Window.IsOpen;

        if (this.CanShowCloseButton ? ImGui.Begin(this.Window.WindowName, ref isWindowOpen, flags) : ImGui.Begin(this.Window.WindowName, flags))
        {
            // Apply background blur
            {
                var effectiveBlurFactor = this.internalBlurFactorOverride ?? internalDrawParams.DefaultBackgroundBlurStrength;
                var shouldBlur = this.Window.AllowBackgroundBlur &&
                                 effectiveBlurFactor != 0f &&
                                 ImGui.GetWindowViewport().ID == ImGui.GetMainViewport().ID &&
                                 windowHasBackground;

                // TODO: Fade between active/inactive tint?
                if (shouldBlur)
                {
                    var wPos = ImGui.GetWindowPos();
                    ImGuiHelpers.PrependBlurBehind(
                        ImGui.GetWindowDrawList(),
                        wPos,
                        wPos + ImGui.GetWindowSize(),
                        effectiveBlurFactor * MaxBlurStrength,
                        ImGui.GetStyle().WindowRounding,
                        tintColor: ImGui.GetStyle().Colors[ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) ? (int)ImGuiCol.TitleBgActive : (int)ImGuiCol.TitleBg] * BlurTintMultiplier,
                        noiseOpacity: BlurNoiseOpacity * effectiveWindowBgAlpha);
                }
            }

            // Not supported yet on non-main viewports
            if ((this.Window.IsPinned || this.internalAlpha.HasValue) &&
                ImGui.GetWindowViewport().ID != ImGui.GetMainViewport().ID)
            {
                this.internalAlpha = null;
                this.Window.IsPinned = false;
                this.windowStateDirty = true;
            }

            // Draw the actual window contents
            if (this.hasError)
            {
                this.DrawErrorMessage();
            }
            else
            {
                // Draw the actual window contents
                try
                {
                    this.Window.Draw();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error during Draw(): {WindowName}", this.Window.WindowName);

                    this.hasError = true;
                    this.lastError = ex;
                }
            }
        }

        if (this.Window.IsOpen && !isWindowOpen)
        {
            this.Window.IsOpen = false;
        }

        const string additionsPopupName = "WindowSystemContextActions";
        var flagsApplicableForTitleBarIcons = !flags.HasFlag(ImGuiWindowFlags.NoDecoration) &&
                                              !flags.HasFlag(ImGuiWindowFlags.NoTitleBar);
        var showAdditions = (this.Window.AllowPinning || this.Window.AllowClickthrough || this.Window.AllowBackgroundBlur) &&
                            internalDrawParams.Flags.HasFlag(WindowDrawFlags.UseAdditionalOptions) &&
                            flagsApplicableForTitleBarIcons;
        var printWindow = false;
        if (showAdditions)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 1f);

            if (ImGui.BeginPopup(additionsPopupName, ImGuiWindowFlags.NoMove))
            {
                var isAvailable = ImGuiHelpers.CheckIsWindowOnMainViewport();

                if (!isAvailable)
                {
                    ImGui.BeginDisabled();
                }

                if (this.Window.AllowPinning)
                {
                    var showAsPinned = this.Window.IsPinned;
                    if (ImGui.Checkbox(this.localizer.GetString("WindowSystemContextActionPin", "Pin Window"), ref showAsPinned))
                    {
                        this.Window.IsPinned = showAsPinned;
                        this.windowStateDirty = true;
                    }

                    this.imGuiComponents.HelpMarker(
                        this.localizer.GetString("WindowSystemContextActionPinHint", "Pinned windows will not move or resize when you click and drag them, nor will they close when escape is pressed."));
                }

                var alpha = (this.internalAlpha ?? ImGui.GetStyle().Alpha) * 100f;
                if (ImGui.SliderFloat(this.localizer.GetString("WindowSystemContextActionAlpha", "Opacity"), ref alpha, 20f,
                                      100f))
                {
                    this.internalAlpha = Math.Clamp(alpha / 100f, 0.2f, 1f);
                    this.windowStateDirty = true;
                }

                ImGui.SameLine();
                if (ImGui.Button(this.localizer.GetString("WindowSystemContextActionReset", "Reset") + "##resetAlpha"))
                {
                    this.internalAlpha = null;
                    this.windowStateDirty = true;
                }

                if (this.Window.AllowBackgroundBlur)
                {
                    var blurOverride =
                        (this.internalBlurFactorOverride ?? internalDrawParams.DefaultBackgroundBlurStrength) * 100f;
                    if (ImGui.SliderFloat(this.localizer.GetString("WindowSystemContextActionBlur", "Background Blur"), ref blurOverride, 0f, 100f, "%.1f%%"))
                    {
                        this.internalBlurFactorOverride = blurOverride / 100f;
                        this.windowStateDirty = true;
                    }

                    ImGui.SameLine();
                    if (ImGui.Button(this.localizer.GetString("WindowSystemContextActionReset", "Reset") + "##resetBlur"))
                    {
                        this.internalBlurFactorOverride = null;
                        this.windowStateDirty = true;
                    }
                }

                if (isAvailable)
                {
                    ImGui.TextColored(ImGuiColors.DalamudGrey,
                                      this.localizer.GetString("WindowSystemContextActionClickthroughDisclaimer",
                                                   "Open this menu again by clicking the three dashes to disable clickthrough."));
                }
                else
                {
                    ImGui.TextColored(ImGuiColors.DalamudGrey,
                                      this.localizer.GetString("WindowSystemContextActionViewportDisclaimer",
                                                   "These features are only available if this window is inside the game window."));
                }

                if (!isAvailable)
                {
                    ImGui.EndDisabled();
                }

                ImGui.EndPopup();
            }

            ImGui.PopStyleVar();
        }

        unsafe
        {
            var window = ImGuiP.GetCurrentWindow();

            ImRect outRect;
            ImGuiP.TitleBarRect(&outRect, window);

            var additionsButton = new TitleBarButton
            {
                Icon = FontAwesomeIcon.Bars,
                IconOffset = new Vector2(2.5f, 1),
                Click = _ =>
                {
                    this.windowStateDirty = false;
                    ImGui.OpenPopup(additionsPopupName);
                },
                Priority = int.MinValue,
                AvailableClickthrough = true,
            };

            if (flagsApplicableForTitleBarIcons)
            {
                this.DrawTitleBarButtons(window, flags, outRect,
                                         showAdditions
                                             ? this.Window.TitleBarButtons.Append(additionsButton)
                                             : this.Window.TitleBarButtons);
            }
        }

        if (wasFocused && this.Window is not StyleEditorWindow)
        {
            ImGui.PopStyleColor();
        }

        this.Window.IsFocused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);

        if (internalDrawParams.Flags.HasFlag(WindowDrawFlags.UseFocusManagement) && !this.Window.IsPinned)
        {
            var escapeDown = this.keyState[VirtualKey.ESCAPE];
            if (escapeDown && this.Window.IsFocused && !wasEscPressedLastFrame && this.Window.RespectCloseHotkey)
            {
                this.Window.IsOpen = false;
                wasEscPressedLastFrame = true;
            }
            else if (!escapeDown && wasEscPressedLastFrame)
            {
                wasEscPressedLastFrame = false;
            }
        }

        var isCollapsed = ImGui.IsWindowCollapsed();
        var isDocked = ImGui.IsWindowDocked();

        ImGui.End();

        if (this.didPushInternalAlpha)
        {
            ImGui.PopStyleVar();
            this.didPushInternalAlpha = false;
        }

        this.Window.PostDraw();

        this.PostHandlePreset();

        if (hasNamespace)
        {
            ImGui.PopID();
        }
    }

    private unsafe void ApplyConditionals()
    {
        if (this.Window.Position.HasValue)
        {
            var pos = this.Window.Position.Value;

            if (this.Window.ForceMainWindow)
            {
                pos += ImGuiHelpers.MainViewport.Pos;
            }

            ImGui.SetNextWindowPos(pos, this.Window.PositionCondition);
        }

        if (this.Window.Size.HasValue)
        {
            ImGui.SetNextWindowSize(this.Window.Size.Value * ImGuiHelpers.GlobalScale, this.Window.SizeCondition);
        }

        if (this.Window.Collapsed.HasValue)
        {
            ImGui.SetNextWindowCollapsed(this.Window.Collapsed.Value, this.Window.CollapsedCondition);
        }

        if (this.Window.SizeConstraints.HasValue)
        {
            var (min, max) = this.GetValidatedConstraints(this.Window.SizeConstraints.Value);
            ImGui.SetNextWindowSizeConstraints(
                min * ImGuiHelpers.GlobalScale,
                max * ImGuiHelpers.GlobalScale);
        }

        var maxBgAlpha = this.internalAlpha ?? this.Window.BgAlpha;

        if (maxBgAlpha.HasValue)
        {
            ImGui.SetNextWindowBgAlpha(maxBgAlpha.Value);
        }
    }

    private (Vector2 Min, Vector2 Max) GetValidatedConstraints(WindowSizeConstraints constraints)
    {
        var min = constraints.MinimumSize;
        var max = constraints.MaximumSize;

        // If max < min, treat as "no constraint" (float.MaxValue)
        if (max.X < min.X || max.Y < min.Y)
        {
            max = new Vector2(float.MaxValue);
        }

        return (min, max);
    }

    private void PreHandlePreset()
    {
        this.hasInitializedFromPreset = true;

        this.Window.IsPinned = this.windowHostState.IsPinned;
        this.internalAlpha = this.windowHostState.Alpha;
    }

    private void PostHandlePreset()
    {
        if (this.windowStateDirty)
        {
            this.windowHostState.IsPinned = this.Window.IsPinned;
            this.windowHostState.Alpha = this.internalAlpha;

            this.windowStateDirty = false;

            Log.Verbose("Saved preset for {WindowName}", this.Window.WindowName);
        }
    }

    private unsafe void DrawTitleBarButtons(ImGuiWindowPtr window, ImGuiWindowFlags flags, ImRect titleBarRect, IEnumerable<TitleBarButton> buttons)
    {
        ImGui.PushClipRect(ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize(), false);

        var style = ImGui.GetStyle();
        var fontSize = ImGui.GetFontSize();
        var drawList = ImGui.GetWindowDrawList();

        var padR = 0f;
        var buttonSize = ImGui.GetFontSize();

        var numNativeButtons = 0;
        if (this.CanShowCloseButton)
        {
            numNativeButtons++;
        }

        if (!flags.HasFlag(ImGuiWindowFlags.NoCollapse) && style.WindowMenuButtonPosition == ImGuiDir.Right)
        {
            numNativeButtons++;
        }

        // If there are no native buttons, pad from the right to make some space
        if (numNativeButtons == 0)
        {
            padR += style.FramePadding.X;
        }

        // Pad to the left, to get out of the way of the native buttons
        padR += numNativeButtons * (buttonSize + style.ItemInnerSpacing.X);

        Vector2 GetCenter(ImRect rect) => new((rect.Min.X + rect.Max.X) * 0.5f, (rect.Min.Y + rect.Max.Y) * 0.5f);

        var numButtons = 0;
        bool DrawButton(TitleBarButton button, Vector2 pos)
        {
            var id = ImGui.GetID($"###CustomTbButton{numButtons}");
            numButtons++;

            var max = pos + new Vector2(fontSize, fontSize);
            ImRect bb = new(pos, max);
            var isClipped = !ImGuiP.ItemAdd(bb, id, null, 0);
            bool hovered, held;
            var pressed = ImGuiP.ButtonBehavior(bb, id, &hovered, &held, ImGuiButtonFlags.None);

            if (isClipped)
            {
                return pressed;
            }

            // Render
            var bgCol = ImGui.GetColorU32((held && hovered) ? ImGuiCol.ButtonActive : hovered ? ImGuiCol.ButtonHovered : ImGuiCol.Button);
            var textCol = button.IconColor.HasValue ? ImGui.GetColorU32(button.IconColor.Value) : ImGui.GetColorU32(ImGuiCol.Text);
            if (hovered || held)
            {
                drawList.AddCircleFilled(GetCenter(bb) + new Vector2(0.0f, -0.5f), (fontSize * 0.5f) + 1.0f, bgCol);
            }

            var offset = button.IconOffset * ImGuiHelpers.GlobalScale;
            drawList.AddText(this.font.IconFont, (float)(fontSize * 0.8), new Vector2(bb.Min.X + offset.X, bb.Min.Y + offset.Y), textCol, button.Icon.ToIconString());

            if (hovered)
            {
                button.ShowTooltip?.Invoke();
            }

            // Switch to moving the window after mouse is moved beyond the initial drag threshold
            if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            {
                ImGuiP.StartMouseMovingWindow(window);
            }

            return pressed;
        }

        foreach (var button in buttons.OrderBy(x => x.Priority))
        {
            Vector2 position = new(titleBarRect.Max.X - padR - buttonSize, titleBarRect.Min.Y + style.FramePadding.Y);
            padR += buttonSize + style.ItemInnerSpacing.X;

            if (DrawButton(button, position))
            {
                button.Click?.Invoke(ImGuiMouseButton.Left);
            }
        }

        ImGui.PopClipRect();
    }

    private void DrawErrorMessage()
    {
        // TODO: Once window systems are services, offer to reload the plugin
        ImGui.TextColoredWrapped(ImGuiColors.ErrorForeground, this.localizer.GetString("WindowSystemErrorOccurred", "An error occurred while rendering this window. Please contact the developer for details."));

        ImGuiHelpers.ScaledDummy(5);

        if (ImGui.Button(this.localizer.GetString("WindowSystemErrorRecoverButton", "Attempt to retry")))
        {
            this.hasError = false;
            this.lastError = null;
        }

        ImGui.SameLine();

        if (ImGui.Button(this.localizer.GetString("WindowSystemErrorClose", "Close Window")))
        {
            this.Window.IsOpen = false;
            this.hasError = false;
            this.lastError = null;
        }

        ImGuiHelpers.ScaledDummy(10);

        if (this.lastError != null)
        {
            using var child = ImRaii.Child("##ErrorDetails", new Vector2(0, 200 * ImGuiHelpers.GlobalScale), true);
            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudGrey))
            {
                ImGui.TextWrapped(this.localizer.GetString("WindowSystemErrorDetails", "Error Details:"));
                ImGui.Separator();
                ImGui.TextWrapped(this.lastError.ToString());
            }

            var childWindowSize = ImGui.GetWindowSize();
            var copyText = this.localizer.GetString("WindowSystemErrorCopy", "Copy");
            var buttonWidth = this.imGuiComponents.GetIconButtonWithTextWidth(FontAwesomeIcon.Copy, copyText);
            ImGui.SetCursorPos(new Vector2(childWindowSize.X - buttonWidth - ImGui.GetStyle().FramePadding.X,
                                           ImGui.GetStyle().FramePadding.Y));
            if (this.imGuiComponents.IconButtonWithText(FontAwesomeIcon.Copy, copyText))
            {
                ImGui.SetClipboardText(this.lastError.ToString());
            }
        }
    }

    /// <summary>
    /// Parameters used when drawing a window through a <see cref="WindowSystem"/>.
    /// </summary>
    internal struct WindowDrawParameters
    {
        /// <summary>
        /// Gets flags that control window behavior.
        /// </summary>
        public WindowDrawFlags Flags { get; init; }

        /// <summary>
        /// Gets the sigma value to be used for background blur, if enabled..
        /// </summary>
        public float DefaultBackgroundBlurStrength { get; init; }
    }
}
