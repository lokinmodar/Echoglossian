namespace DalaMock.Core.Fonts.Chooser;

using DalaMock.Shared.Interfaces;

/// <summary>
/// A cross-platform port of Dalamud's <c>SingleFontChooserDialog</c> for use inside DalaMock.
/// <para>
/// Differs from the real dialog in three ways: it builds its preview atlas from an
/// <see cref="IUiBuilder"/> rather than the internal <c>FontAtlasFactory</c>; it reads the display
/// language from an injected <see cref="MockDalamudConfiguration"/> instead of Dalamud's service
/// locator; and it sources system fonts from <see cref="MockSystemFontProvider"/> instead of
/// DirectWrite.
/// </para>
/// </summary>
[SuppressMessage(
    "StyleCop.CSharp.LayoutRules",
    "SA1519:Braces should not be omitted from multi-line child statement",
    Justification = "Multiple fixed blocks")]
internal sealed class MockSingleFontChooserDialog : IFontChooserDialog
{
    private const float MinFontSizePt = 1;

    private const float MaxFontSizePt = 127;

    private static readonly List<IFontId> EmptyIFontList = [];

    private static readonly (string Name, float Value)[] FontSizeList =
    [
        ("9.6", 9.6f),
        ("10", 10f),
        ("12", 12f),
        ("14", 14f),
        ("16", 16f),
        ("18", 18f),
        ("18.4", 18.4f),
        ("20", 20),
        ("23", 23),
        ("34", 34),
        ("36", 36),
        ("40", 40),
        ("45", 45),
        ("46", 46),
        ("68", 68),
        ("90", 90),
    ];

    private static int counterStatic;

    private readonly int counter;
    private readonly byte[] fontPreviewText = new byte[2048];
    private readonly TaskCompletionSource<SingleFontSpec> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly IFontAtlas atlas;
    private readonly MockDalamudConfiguration config;
    private readonly MockSystemFontProvider systemFontProvider;

    private string popupImGuiName;
    private string title;

    private bool firstDraw = true;
    private bool firstDrawAfterRefresh;
    private int setFocusOn = -1;

    private bool useAdvancedOptions;
    private AdvancedOptionsUiState advUiState;

    private Task<List<IFontFamilyId>>? fontFamilies;
    private int selectedFamilyIndex = -1;
    private int selectedFontIndex = -1;
    private int selectedFontWeight = (int)DWRITE_FONT_WEIGHT.DWRITE_FONT_WEIGHT_NORMAL;
    private int selectedFontStretch = (int)DWRITE_FONT_STRETCH.DWRITE_FONT_STRETCH_NORMAL;
    private int selectedFontStyle = (int)DWRITE_FONT_STYLE.DWRITE_FONT_STYLE_NORMAL;

    private string familySearch = string.Empty;
    private string fontSearch = string.Empty;
    private string fontSizeSearch = "12";
    private IFontHandle? fontHandle;
    private SingleFontSpec selectedFont;

    private bool popupPositionChanged;
    private bool popupSizeChanged;
    private Vector2 popupPosition = new(float.NaN);
    private Vector2 popupSize = new(float.NaN);

    /// <summary>Initializes a new instance of the <see cref="MockSingleFontChooserDialog"/> class.</summary>
    /// <param name="uiBuilder">The UI builder used to create the temporary preview font atlas.</param>
    /// <param name="config">The mock Dalamud configuration, used for the display language.</param>
    /// <param name="systemFontProvider">The system font provider.</param>
    /// <param name="isGlobalScaled">Whether the fonts in the atlas are global scaled.</param>
    /// <param name="debugAtlasName">Atlas name for debugging purposes.</param>
    public MockSingleFontChooserDialog(
        IUiBuilder uiBuilder,
        MockDalamudConfiguration config,
        MockSystemFontProvider systemFontProvider,
        bool isGlobalScaled = true,
        string? debugAtlasName = null)
    {
        this.config = config;
        this.systemFontProvider = systemFontProvider;
        this.counter = Interlocked.Increment(ref counterStatic);
        this.title = "Choose a font...";
        this.popupImGuiName = $"{this.title}##{nameof(MockSingleFontChooserDialog)}[{this.counter}]";
        this.atlas = uiBuilder.CreateFontAtlas(
            FontAtlasAutoRebuildMode.Async,
            isGlobalScaled,
            debugAtlasName ?? $"{nameof(MockSingleFontChooserDialog)}[{this.counter}]");
        this.selectedFont = new() { FontId = DalamudDefaultFontAndFamilyId.Instance };
        Encoding.UTF8.GetBytes(
            "Font preview.\n0123456789!\n遍角次亮采之门，门上插刀、直字拐弯、天上平板、船顶漏雨。\n다람쥐 헌 쳇바퀴에 타고파",
            this.fontPreviewText);
    }

    /// <inheritdoc/>
    public event Action<SingleFontSpec>? SelectedFontSpecChanged;

    /// <inheritdoc/>
    public string Title
    {
        get => this.title;
        set
        {
            this.title = value;
            this.popupImGuiName = $"{this.title}##{nameof(MockSingleFontChooserDialog)}[{this.counter}]";
        }
    }

    /// <inheritdoc/>
    public string PreviewText
    {
        get
        {
            var n = this.fontPreviewText.AsSpan().IndexOf((byte)0);
            return n < 0
                       ? Encoding.UTF8.GetString(this.fontPreviewText)
                       : Encoding.UTF8.GetString(this.fontPreviewText, 0, n);
        }
        set => Encoding.UTF8.GetBytes(value, this.fontPreviewText);
    }

    /// <inheritdoc/>
    public Task<SingleFontSpec> ResultTask => this.tcs.Task;

    /// <inheritdoc/>
    public SingleFontSpec SelectedFont
    {
        get => this.selectedFont;
        set
        {
            this.selectedFont = value;

            var familyName = value.FontId.Family.ToString() ?? string.Empty;
            var fontName = value.FontId.ToString() ?? string.Empty;
            this.familySearch = this.ExtractName(value.FontId.Family);
            this.fontSearch = this.ExtractName(value.FontId);
            if (this.fontFamilies?.IsCompletedSuccessfully is true)
            {
                this.UpdateSelectedFamilyAndFontIndices(this.fontFamilies.Result, familyName, fontName);
            }

            this.fontSizeSearch = $"{value.SizePt:0.##}";
            this.advUiState = new(value);
            this.useAdvancedOptions |= Math.Abs(value.LineHeight - 1f) > 0.000001;
            this.useAdvancedOptions |= value.GlyphOffset != default;
            this.useAdvancedOptions |= value.LetterSpacing != 0f;

            this.SelectedFontSpecChanged?.Invoke(this.selectedFont);
        }
    }

    /// <inheritdoc/>
    public Predicate<IFontFamilyId>? FontFamilyExcludeFilter { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to ignore the global scale on preview text input.
    /// </summary>
    public bool IgnorePreviewGlobalScale { get; set; }

    /// <summary>Gets or sets a value indicating whether this popup should be modal.</summary>
    public bool IsModal { get; set; } = true;

    /// <summary>Gets or sets the window flags.</summary>
    public ImGuiWindowFlags WindowFlags { get; set; }

    /// <summary>Gets or sets the popup window position.</summary>
    public Vector2 PopupPosition
    {
        get => this.popupPosition;
        set
        {
            this.popupPositionChanged = true;
            this.popupPosition = value;
        }
    }

    /// <summary>Gets or sets the popup window size.</summary>
    public Vector2 PopupSize
    {
        get => this.popupSize;
        set
        {
            this.popupSizeChanged = true;
            this.popupSize = value;
        }
    }

    /// <summary>Gets the default popup size before clamping to monitor work area.</summary>
    /// <returns>The default popup size.</returns>
    public static Vector2 GetDefaultPopupSizeNonClamped()
    {
        return new Vector2(40, 30) * ImGui.GetTextLineHeight();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.fontHandle?.Dispose();
        this.atlas.Dispose();
    }

    /// <inheritdoc/>
    public void Cancel()
    {
        this.tcs.TrySetCanceled();
        ImGui.GetIO().WantCaptureKeyboard = false;
        ImGui.GetIO().WantTextInput = false;
    }

    /// <inheritdoc/>
    public void SetPopupPositionAndSizeToCurrentWindowCenter(Vector2 preferredPopupSize)
    {
        this.PopupSize = preferredPopupSize;
        this.PopupPosition = ImGui.GetWindowPos() + ((ImGui.GetWindowSize() - preferredPopupSize) / 2);
    }

    /// <inheritdoc/>
    public void SetPopupPositionAndSizeToCurrentWindowCenter() =>
        this.SetPopupPositionAndSizeToCurrentWindowCenter(GetDefaultPopupSizeNonClamped());

    /// <inheritdoc/>
    public void Draw()
    {
        const float popupMinWidth = 320;
        const float popupMinHeight = 240;

        ImGui.GetIO().WantCaptureKeyboard = true;
        ImGui.GetIO().WantTextInput = true;
        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            this.Cancel();
            return;
        }

        if (this.firstDraw)
        {
            if (this.IsModal)
            {
                ImGui.OpenPopup(this.popupImGuiName);
            }
        }

        if (this.firstDraw || this.popupPositionChanged || this.popupSizeChanged)
        {
            var preferProvidedSize = !float.IsNaN(this.popupSize.X) && !float.IsNaN(this.popupSize.Y);
            var size = preferProvidedSize ? this.popupSize : GetDefaultPopupSizeNonClamped();
            size.X = Math.Max(size.X, popupMinWidth);
            size.Y = Math.Max(size.Y, popupMinHeight);

            var preferProvidedPos = !float.IsNaN(this.popupPosition.X) && !float.IsNaN(this.popupPosition.Y);
            var monitorLocatorPos = preferProvidedPos ? this.popupPosition + (size / 2) : ImGui.GetMousePos();

            var monitors = ImGui.GetPlatformIO().Monitors;
            Vector2 lt;
            Vector2 workSize;
            if (monitors.Size > 0)
            {
                var preferredMonitor = 0;
                var preferredDistance = GetDistanceFromMonitor(monitorLocatorPos, monitors[0]);
                for (var i = 1; i < monitors.Size; i++)
                {
                    var distance = GetDistanceFromMonitor(monitorLocatorPos, monitors[i]);
                    if (distance < preferredDistance)
                    {
                        preferredMonitor = i;
                        preferredDistance = distance;
                    }
                }

                lt = monitors[preferredMonitor].WorkPos;
                workSize = monitors[preferredMonitor].WorkSize;
            }
            else
            {
                var viewport = ImGui.GetMainViewport();
                lt = viewport.WorkPos;
                workSize = viewport.WorkSize;
            }

            size.X = Math.Min(size.X, workSize.X);
            size.Y = Math.Min(size.Y, workSize.Y);
            var rb = (lt + workSize) - size;

            var pos =
                preferProvidedPos
                    ? new(Math.Clamp(this.PopupPosition.X, lt.X, rb.X), Math.Clamp(this.PopupPosition.Y, lt.Y, rb.Y))
                    : (lt + rb) / 2;

            ImGui.SetNextWindowSize(size, ImGuiCond.Always);
            ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
            this.popupPositionChanged = this.popupSizeChanged = false;
        }

        ImGui.SetNextWindowSizeConstraints(new(popupMinWidth, popupMinHeight), new(float.MaxValue));
        if (this.IsModal)
        {
            var open = true;
            if (!ImGui.BeginPopupModal(this.popupImGuiName, ref open, this.WindowFlags) || !open)
            {
                this.Cancel();
                return;
            }
        }
        else
        {
            var open = true;
            if (!ImGui.Begin(this.popupImGuiName, ref open, this.WindowFlags) || !open)
            {
                ImGui.End();
                this.Cancel();
                return;
            }
        }

        var framePad = ImGui.GetStyle().FramePadding;
        var windowPad = ImGui.GetStyle().WindowPadding;
        var baseOffset = ImGui.GetCursorPos() - windowPad;

        var actionSize = Vector2.Zero;
        actionSize = Vector2.Max(actionSize, ImGui.CalcTextSize("OK"u8));
        actionSize = Vector2.Max(actionSize, ImGui.CalcTextSize("Cancel"u8));
        actionSize = Vector2.Max(actionSize, ImGui.CalcTextSize("Refresh"u8));
        actionSize = Vector2.Max(actionSize, ImGui.CalcTextSize("Reset"u8));
        actionSize += framePad * 2;

        var bodySize = ImGui.GetContentRegionAvail();
        ImGui.SetCursorPos(baseOffset + windowPad);
        if (ImGui.BeginChild(
                "##choicesBlock"u8,
                bodySize with { X = bodySize.X - windowPad.X - actionSize.X },
                false,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            this.DrawChoices();
        }

        ImGui.EndChild();

        ImGui.SetCursorPos(baseOffset + windowPad + new Vector2(bodySize.X - actionSize.X, 0));

        if (ImGui.BeginChild("##actionsBlock"u8, bodySize with { X = actionSize.X }))
        {
            this.DrawActionButtons(actionSize);
        }

        ImGui.EndChild();

        this.popupPosition = ImGui.GetWindowPos();
        this.popupSize = ImGui.GetWindowSize();
        if (this.IsModal)
        {
            ImGui.EndPopup();
        }
        else
        {
            ImGui.End();
        }

        this.firstDraw = false;
        this.firstDrawAfterRefresh = false;
    }

    private static float GetDistanceFromMonitor(Vector2 point, ImGuiPlatformMonitor monitor)
    {
        var lt = monitor.MainPos;
        var rb = monitor.MainPos + monitor.MainSize;
        var xoff =
            point.X < lt.X
                ? lt.X - point.X
                : point.X > rb.X
                    ? point.X - rb.X
                    : 0;
        var yoff =
            point.Y < lt.Y
                ? lt.Y - point.Y
                : point.Y > rb.Y
                    ? point.Y - rb.Y
                    : 0;
        return MathF.Sqrt((xoff * xoff) + (yoff * yoff));
    }

    private void DrawChoices()
    {
        var lineHeight = ImGui.GetTextLineHeight();
        var previewHeight = (ImGui.GetFrameHeightWithSpacing() - lineHeight) +
                            Math.Max(lineHeight, this.selectedFont.LineHeightPx * 2);

        var advancedOptionsHeight = ImGui.GetFrameHeightWithSpacing() * (this.useAdvancedOptions ? 4 : 1);

        var tableSize = ImGui.GetContentRegionAvail() -
                        new Vector2(0, ImGui.GetStyle().WindowPadding.Y + previewHeight + advancedOptionsHeight);
        if (ImGui.BeginChild(
                "##tableContainer"u8,
                tableSize,
                false,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
            && ImGui.BeginTable("##table"u8, 3, ImGuiTableFlags.None))
        {
            ImGui.PushStyleColor(ImGuiCol.TableHeaderBg, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, Vector4.Zero);
            ImGui.TableSetupColumn(
                "Font:##familyColumn"u8,
                ImGuiTableColumnFlags.WidthStretch,
                0.4f);
            ImGui.TableSetupColumn(
                "Style:##fontColumn"u8,
                ImGuiTableColumnFlags.WidthStretch,
                0.4f);
            ImGui.TableSetupColumn(
                "Size:##sizeColumn"u8,
                ImGuiTableColumnFlags.WidthStretch,
                0.2f);
            ImGui.TableHeadersRow();
            ImGui.PopStyleColor(3);

            ImGui.TableNextRow();

            var pad = (int)MathF.Round(8 * ImGuiHelpers.GlobalScale);
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(pad));
            ImGui.TableNextColumn();
            var changed = this.DrawFamilyListColumn();

            ImGui.TableNextColumn();
            changed |= this.DrawFontListColumn(changed);

            ImGui.TableNextColumn();
            changed |= this.DrawSizeListColumn();

            if (changed)
            {
                this.fontHandle?.Dispose();
                this.fontHandle = null;
            }

            ImGui.PopStyleVar();

            ImGui.EndTable();
        }

        ImGui.EndChild();

        ImGui.Checkbox("Show advanced options"u8, ref this.useAdvancedOptions);
        if (this.useAdvancedOptions)
        {
            if (this.DrawAdvancedOptions())
            {
                this.fontHandle?.Dispose();
                this.fontHandle = null;
            }
        }

        if (this.fontHandle is null)
        {
            if (this.IgnorePreviewGlobalScale)
            {
                this.fontHandle = this.selectedFont.CreateFontHandle(
                    this.atlas,
                    tk => tk.OnPreBuild(e => e.SetFontScaleMode(e.Font, FontScaleMode.UndoGlobalScale)));
            }
            else
            {
                this.fontHandle = this.selectedFont.CreateFontHandle(this.atlas);
            }

            this.SelectedFontSpecChanged?.Invoke(this.selectedFont);
        }

        if (this.fontHandle is null)
        {
            ImGui.SetCursorPos(ImGui.GetCursorPos() + ImGui.GetStyle().FramePadding);
            ImGui.Text("Select a font."u8);
        }
        else if (this.fontHandle.LoadException is { } loadException)
        {
            ImGui.SetCursorPos(ImGui.GetCursorPos() + ImGui.GetStyle().FramePadding);
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.ErrorForeground);
            ImGui.Text(loadException.Message);
            ImGui.PopStyleColor();
        }
        else if (!this.fontHandle.Available)
        {
            ImGui.SetCursorPos(ImGui.GetCursorPos() + ImGui.GetStyle().FramePadding);
            ImGui.Text("Loading font..."u8);
        }
        else
        {
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            using (this.fontHandle?.Push())
            {
                ImGui.InputTextMultiline(
                    "##fontPreviewText"u8,
                    this.fontPreviewText,
                    ImGui.GetContentRegionAvail());
            }
        }
    }

    private bool DrawFamilyListColumn()
    {
        if (this.fontFamilies?.IsCompleted is not true)
        {
            ImGui.SetScrollY(0);
            ImGui.Text("Loading..."u8);
            return false;
        }

        if (!this.fontFamilies.IsCompletedSuccessfully)
        {
            ImGui.SetScrollY(0);
            ImGui.Text("Error: " + this.fontFamilies.Exception);
            return false;
        }

        var families = this.fontFamilies.Result;
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);

        if (this.setFocusOn == 0)
        {
            this.setFocusOn = -1;
            ImGui.SetKeyboardFocusHere();
        }

        var changed = false;
        if (ImGui.InputText(
                "##familySearch"u8,
                ref this.familySearch,
                255,
                ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.CallbackHistory,
                (ref ImGuiInputTextCallbackData data) =>
                {
                    if (families.Count == 0)
                    {
                        return 0;
                    }

                    var baseIndex = this.selectedFamilyIndex;
                    if (data.SelectionStart == 0 && data.SelectionEnd == data.BufTextLen)
                    {
                        switch (data.EventKey)
                        {
                            case ImGuiKey.DownArrow:
                                this.selectedFamilyIndex = (this.selectedFamilyIndex + 1) % families.Count;
                                changed = true;
                                break;
                            case ImGuiKey.UpArrow:
                                this.selectedFamilyIndex =
                                    (this.selectedFamilyIndex + families.Count - 1) % families.Count;
                                changed = true;
                                break;
                        }

                        if (changed)
                        {
                            SetTextFromCallback(
                                ref data,
                                this.ExtractName(families[this.selectedFamilyIndex]));
                        }
                    }
                    else
                    {
                        switch (data.EventKey)
                        {
                            case ImGuiKey.DownArrow:
                                this.selectedFamilyIndex = families.FindIndex(
                                    baseIndex + 1,
                                    x => this.TestName(x, this.familySearch));
                                if (this.selectedFamilyIndex < 0)
                                {
                                    this.selectedFamilyIndex = families.FindIndex(
                                        0,
                                        baseIndex + 1,
                                        x => this.TestName(x, this.familySearch));
                                }

                                changed = true;
                                break;
                            case ImGuiKey.UpArrow:
                                if (baseIndex > 0)
                                {
                                    this.selectedFamilyIndex = families.FindLastIndex(
                                        baseIndex - 1,
                                        x => this.TestName(x, this.familySearch));
                                }

                                if (this.selectedFamilyIndex < 0)
                                {
                                    if (baseIndex < 0)
                                    {
                                        baseIndex = 0;
                                    }

                                    this.selectedFamilyIndex = families.FindLastIndex(
                                        families.Count - 1,
                                        families.Count - baseIndex,
                                        x => this.TestName(x, this.familySearch));
                                }

                                changed = true;
                                break;
                        }
                    }

                    return 0;
                }))
        {
            if (!string.IsNullOrWhiteSpace(this.familySearch) && !changed)
            {
                this.selectedFamilyIndex = families.FindIndex(x => this.TestName(x, this.familySearch));
                changed = true;
            }
        }

        if (ImGui.BeginChild("##familyList"u8, ImGui.GetContentRegionAvail()))
        {
            var clipper = ImGui.ImGuiListClipper();
            var lineHeight = ImGui.GetTextLineHeightWithSpacing();

            if ((changed || this.firstDrawAfterRefresh) && this.selectedFamilyIndex != -1)
            {
                ImGui.SetScrollY(
                    (lineHeight * this.selectedFamilyIndex) -
                    ((ImGui.GetContentRegionAvail().Y - lineHeight) / 2));
            }

            clipper.Begin(families.Count, lineHeight);
            while (clipper.Step())
            {
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                {
                    if (i < 0)
                    {
                        ImGui.Text(" "u8);
                        continue;
                    }

                    var selected = this.selectedFamilyIndex == i;
                    if (ImGui.Selectable(
                            this.ExtractName(families[i]),
                            ref selected,
                            ImGuiSelectableFlags.DontClosePopups))
                    {
                        this.selectedFamilyIndex = families.IndexOf(families[i]);
                        this.familySearch = this.ExtractName(families[i]);
                        this.setFocusOn = 0;
                        changed = true;
                    }
                }
            }

            clipper.Destroy();
        }

        if (changed && this.selectedFamilyIndex >= 0)
        {
            var family = families[this.selectedFamilyIndex];
            this.selectedFontIndex = family.FindBestMatch(
                this.selectedFontWeight,
                this.selectedFontStretch,
                this.selectedFontStyle);
            this.selectedFont = this.selectedFont with { FontId = family.Fonts[this.selectedFontIndex] };
        }

        ImGui.EndChild();
        return changed;
    }

    private bool DrawFontListColumn(bool changed)
    {
        if (this.fontFamilies?.IsCompleted is not true)
        {
            ImGui.Text("Loading..."u8);
            return changed;
        }

        if (!this.fontFamilies.IsCompletedSuccessfully)
        {
            ImGui.Text("Error: " + this.fontFamilies.Exception);
            return changed;
        }

        var families = this.fontFamilies.Result;
        var family = this.selectedFamilyIndex >= 0
                     && this.selectedFamilyIndex < families.Count
                         ? families[this.selectedFamilyIndex]
                         : null;
        var fonts = family is not null ? family.Fonts.ToList() : EmptyIFontList;

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);

        if (this.setFocusOn == 1)
        {
            this.setFocusOn = -1;
            ImGui.SetKeyboardFocusHere();
        }

        if (ImGui.InputText(
                "##fontSearch"u8,
                ref this.fontSearch,
                255,
                ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.CallbackHistory,
                (ref ImGuiInputTextCallbackData data) =>
                {
                    if (fonts.Count == 0)
                    {
                        return 0;
                    }

                    var baseIndex = this.selectedFontIndex;
                    if (data.SelectionStart == 0 && data.SelectionEnd == data.BufTextLen)
                    {
                        switch (data.EventKey)
                        {
                            case ImGuiKey.DownArrow:
                                this.selectedFontIndex = (this.selectedFontIndex + 1) % fonts.Count;
                                changed = true;
                                break;
                            case ImGuiKey.UpArrow:
                                this.selectedFontIndex = (this.selectedFontIndex + fonts.Count - 1) % fonts.Count;
                                changed = true;
                                break;
                        }

                        if (changed)
                        {
                            SetTextFromCallback(
                                ref data,
                                this.ExtractName(fonts[this.selectedFontIndex]));
                        }
                    }
                    else
                    {
                        switch (data.EventKey)
                        {
                            case ImGuiKey.DownArrow:
                                this.selectedFontIndex = fonts.FindIndex(
                                    baseIndex + 1,
                                    x => this.TestName(x, this.fontSearch));
                                if (this.selectedFontIndex < 0)
                                {
                                    this.selectedFontIndex = fonts.FindIndex(
                                        0,
                                        baseIndex + 1,
                                        x => this.TestName(x, this.fontSearch));
                                }

                                changed = true;
                                break;
                            case ImGuiKey.UpArrow:
                                if (baseIndex > 0)
                                {
                                    this.selectedFontIndex = fonts.FindLastIndex(
                                        baseIndex - 1,
                                        x => this.TestName(x, this.fontSearch));
                                }

                                if (this.selectedFontIndex < 0)
                                {
                                    if (baseIndex < 0)
                                    {
                                        baseIndex = 0;
                                    }

                                    this.selectedFontIndex = fonts.FindLastIndex(
                                        fonts.Count - 1,
                                        fonts.Count - baseIndex,
                                        x => this.TestName(x, this.fontSearch));
                                }

                                changed = true;
                                break;
                        }
                    }

                    return 0;
                }))
        {
            if (!string.IsNullOrWhiteSpace(this.fontSearch) && !changed)
            {
                this.selectedFontIndex = fonts.FindIndex(x => this.TestName(x, this.fontSearch));
                changed = true;
            }
        }

        if (ImGui.BeginChild("##fontList"u8))
        {
            var clipper = ImGui.ImGuiListClipper();
            var lineHeight = ImGui.GetTextLineHeightWithSpacing();

            if ((changed || this.firstDrawAfterRefresh) && this.selectedFontIndex != -1)
            {
                ImGui.SetScrollY(
                    (lineHeight * this.selectedFontIndex) -
                    ((ImGui.GetContentRegionAvail().Y - lineHeight) / 2));
            }

            clipper.Begin(fonts.Count, lineHeight);
            while (clipper.Step())
            {
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                {
                    if (i < 0)
                    {
                        ImGui.Text(" "u8);
                        continue;
                    }

                    var selected = this.selectedFontIndex == i;
                    if (ImGui.Selectable(
                            this.ExtractName(fonts[i]),
                            ref selected,
                            ImGuiSelectableFlags.DontClosePopups))
                    {
                        this.selectedFontIndex = fonts.IndexOf(fonts[i]);
                        this.fontSearch = this.ExtractName(fonts[i]);
                        this.setFocusOn = 1;
                        changed = true;
                    }
                }
            }

            clipper.Destroy();
        }

        ImGui.EndChild();

        if (changed && family is not null && this.selectedFontIndex >= 0)
        {
            var font = family.Fonts[this.selectedFontIndex];
            this.selectedFontWeight = font.Weight;
            this.selectedFontStretch = font.Stretch;
            this.selectedFontStyle = font.Style;
            int fontNo = 0;
            if (family is DalamudAssetFontAndFamilyId
                {
                    Asset: DalamudAsset.NotoSansCjkRegular or DalamudAsset.NotoSansCjkMedium
                })
            {
                fontNo = this.config.EffectiveLanguage switch
                {
                    "ja" or "jp" => 0,
                    "tw" => 1,
                    "zh" => 2,
                    "ko" => 3,
                    _ => 0,
                };
            }

            this.selectedFont = this.selectedFont with { FontId = font, FontNo = fontNo };
        }

        return changed;
    }

    private bool DrawSizeListColumn()
    {
        var changed = false;
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);

        if (this.setFocusOn == 2)
        {
            this.setFocusOn = -1;
            ImGui.SetKeyboardFocusHere();
        }

        if (ImGui.InputText(
                "##fontSizeSearch"u8,
                ref this.fontSizeSearch,
                255,
                ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.CallbackHistory |
                ImGuiInputTextFlags.CharsDecimal,
                (ref ImGuiInputTextCallbackData data) =>
                {
                    switch (data.EventKey)
                    {
                        case ImGuiKey.DownArrow:
                            this.selectedFont = this.selectedFont with
                            {
                                SizePt = Math.Min(MaxFontSizePt, MathF.Floor(this.selectedFont.SizePt) + 1),
                            };
                            changed = true;
                            break;
                        case ImGuiKey.UpArrow:
                            this.selectedFont = this.selectedFont with
                            {
                                SizePt = Math.Max(MinFontSizePt, MathF.Ceiling(this.selectedFont.SizePt) - 1),
                            };
                            changed = true;
                            break;
                    }

                    if (changed)
                    {
                        SetTextFromCallback(ref data, $"{this.selectedFont.SizePt:0.##}");
                    }

                    return 0;
                }))
        {
            if (float.TryParse(this.fontSizeSearch, out var fontSizePt1))
            {
                this.selectedFont = this.selectedFont with { SizePt = fontSizePt1 };
                changed = true;
            }
        }

        if (ImGui.BeginChild("##fontSizeList"u8))
        {
            var clipper = ImGui.ImGuiListClipper();
            var lineHeight = ImGui.GetTextLineHeightWithSpacing();

            if (changed && this.selectedFontIndex != -1)
            {
                ImGui.SetScrollY(
                    (lineHeight * this.selectedFontIndex) -
                    ((ImGui.GetContentRegionAvail().Y - lineHeight) / 2));
            }

            clipper.Begin(FontSizeList.Length, lineHeight);
            while (clipper.Step())
            {
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                {
                    if (i < 0)
                    {
                        ImGui.Text(" "u8);
                        continue;
                    }

                    var selected = Equals(FontSizeList[i].Value, this.selectedFont.SizePt);
                    if (ImGui.Selectable(
                            FontSizeList[i].Name,
                            ref selected,
                            ImGuiSelectableFlags.DontClosePopups))
                    {
                        this.selectedFont = this.selectedFont with { SizePt = FontSizeList[i].Value };
                        this.setFocusOn = 2;
                        changed = true;
                    }
                }
            }

            clipper.Destroy();
        }

        ImGui.EndChild();

        if (this.selectedFont.SizePt < MinFontSizePt)
        {
            this.selectedFont = this.selectedFont with { SizePt = MinFontSizePt };
            changed = true;
        }

        if (this.selectedFont.SizePt > MaxFontSizePt)
        {
            this.selectedFont = this.selectedFont with { SizePt = MaxFontSizePt };
            changed = true;
        }

        if (changed)
        {
            this.fontSizeSearch = $"{this.selectedFont.SizePt:0.##}";
        }

        return changed;
    }

    private bool DrawAdvancedOptions()
    {
        var changed = false;

        if (!ImGui.BeginTable("##advancedOptions"u8, 4))
        {
            return false;
        }

        var labelWidth = ImGui.CalcTextSize("Letter Spacing:"u8).X;
        labelWidth = Math.Max(labelWidth, ImGui.CalcTextSize("Offset:"u8).X);
        labelWidth = Math.Max(labelWidth, ImGui.CalcTextSize("Line Height:"u8).X);
        labelWidth += ImGui.GetStyle().FramePadding.X;

        var inputWidth = ImGui.CalcTextSize("000.000"u8).X + (ImGui.GetStyle().FramePadding.X * 2);
        ImGui.TableSetupColumn(
            "##inputLabelColumn"u8,
            ImGuiTableColumnFlags.WidthFixed,
            labelWidth);
        ImGui.TableSetupColumn(
            "##input1Column"u8,
            ImGuiTableColumnFlags.WidthFixed,
            inputWidth);
        ImGui.TableSetupColumn(
            "##input2Column"u8,
            ImGuiTableColumnFlags.WidthFixed,
            inputWidth);
        ImGui.TableSetupColumn(
            "##fillerColumn"u8,
            ImGuiTableColumnFlags.WidthStretch,
            1f);

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Offset:"u8);

        ImGui.TableNextColumn();
        if (FloatInputText(
                "##glyphOffsetXInput",
                ref this.advUiState.OffsetXText,
                this.selectedFont.GlyphOffset.X) is { } newGlyphOffsetX)
        {
            changed = true;
            this.selectedFont = this.selectedFont with
            {
                GlyphOffset = this.selectedFont.GlyphOffset with { X = newGlyphOffsetX },
            };
        }

        ImGui.TableNextColumn();
        if (FloatInputText(
                "##glyphOffsetYInput",
                ref this.advUiState.OffsetYText,
                this.selectedFont.GlyphOffset.Y) is { } newGlyphOffsetY)
        {
            changed = true;
            this.selectedFont = this.selectedFont with
            {
                GlyphOffset = this.selectedFont.GlyphOffset with { Y = newGlyphOffsetY },
            };
        }

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Letter Spacing:"u8);

        ImGui.TableNextColumn();
        if (FloatInputText(
                "##letterSpacingXInput",
                ref this.advUiState.LetterSpacingText,
                this.selectedFont.LetterSpacing) is { } newLetterSpacing)
        {
            changed = true;
            this.selectedFont = this.selectedFont with { LetterSpacing = newLetterSpacing };
        }

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Line Height:"u8);

        ImGui.TableNextColumn();
        if (FloatInputText(
                "##lineHeightInput",
                ref this.advUiState.LineHeightText,
                this.selectedFont.LineHeight,
                0.05f,
                0.1f,
                3f) is { } newLineHeight)
        {
            changed = true;
            this.selectedFont = this.selectedFont with { LineHeight = newLineHeight };
        }

        ImGui.EndTable();
        return changed;

        static unsafe float? FloatInputText(
            string label,
            ref string buf,
            float value,
            float step = 1f,
            float min = -127,
            float max = 127)
        {
            var stylePushed = value < min || value > max || !float.TryParse(buf, out _);
            if (stylePushed)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.ErrorForeground);
            }

            var changed2 = false;
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            var changed1 = ImGui.InputText(
                label,
                ref buf,
                255,
                ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.CallbackHistory |
                ImGuiInputTextFlags.CharsDecimal,
                (ref ImGuiInputTextCallbackData data) =>
                {
                    switch (data.EventKey)
                    {
                        case ImGuiKey.DownArrow:
                            changed2 = true;
                            value = Math.Min(max, (MathF.Round(value / step) * step) + step);
                            SetTextFromCallback(ref data, $"{value:0.##}");
                            break;
                        case ImGuiKey.UpArrow:
                            changed2 = true;
                            value = Math.Max(min, (MathF.Round(value / step) * step) - step);
                            SetTextFromCallback(ref data, $"{value:0.##}");
                            break;
                    }

                    return 0;
                });

            if (stylePushed)
            {
                ImGui.PopStyleColor();
            }

            if (!changed1 && !changed2)
            {
                return null;
            }

            if (!float.TryParse(buf, out var parsed))
            {
                return null;
            }

            if (min > parsed || parsed > max)
            {
                return null;
            }

            return parsed;
        }
    }

    private void DrawActionButtons(Vector2 buttonSize)
    {
        if (this.fontHandle?.Available is not true
            || this.FontFamilyExcludeFilter?.Invoke(this.selectedFont.FontId.Family) is true)
        {
            ImGui.BeginDisabled();
            ImGui.Button("OK"u8, buttonSize);
            ImGui.EndDisabled();
        }
        else if (ImGui.Button("OK"u8, buttonSize))
        {
            this.tcs.TrySetResult(this.selectedFont);
        }

        if (ImGui.Button("Cancel"u8, buttonSize))
        {
            this.Cancel();
        }

        var doRefresh = false;
        var isFirst = false;
        if (this.fontFamilies?.IsCompleted is not true)
        {
            isFirst = doRefresh = this.fontFamilies is null;
            ImGui.BeginDisabled();
            ImGui.Button("Refresh"u8, buttonSize);
            ImGui.EndDisabled();
        }
        else if (ImGui.Button("Refresh"u8, buttonSize))
        {
            doRefresh = true;
        }

        if (doRefresh)
        {
            this.fontFamilies =
                this.fontFamilies?.ContinueWith(_ => RefreshBody())
                ?? Task.Run(RefreshBody);
            this.fontFamilies.ContinueWith(_ => this.firstDrawAfterRefresh = true);

            List<IFontFamilyId> RefreshBody()
            {
                var familyName = this.selectedFont.FontId.Family.ToString() ?? string.Empty;
                var fontName = this.selectedFont.FontId.ToString() ?? string.Empty;

                var newFonts = new List<IFontFamilyId> { DalamudDefaultFontAndFamilyId.Instance };
                newFonts.AddRange(IFontFamilyId.ListDalamudFonts());
                newFonts.AddRange(IFontFamilyId.ListGameFonts());
                var systemFonts = this.systemFontProvider.GetFamilies(!isFirst);
                systemFonts.Sort((a, b) => string.Compare(
                                     this.ExtractName(a),
                                     this.ExtractName(b),
                                     StringComparison.CurrentCultureIgnoreCase));
                newFonts.AddRange(systemFonts);
                if (this.FontFamilyExcludeFilter is not null)
                {
                    newFonts.RemoveAll(this.FontFamilyExcludeFilter);
                }

                this.UpdateSelectedFamilyAndFontIndices(newFonts, familyName, fontName);
                return newFonts;
            }
        }

        if (this.useAdvancedOptions)
        {
            if (ImGui.Button("Reset"u8, buttonSize))
            {
                this.selectedFont = this.selectedFont with
                {
                    LineHeight = 1f,
                    GlyphOffset = default,
                    LetterSpacing = default,
                };

                this.advUiState = new(this.selectedFont);
                this.fontHandle?.Dispose();
                this.fontHandle = null;
            }
        }
    }

    private void UpdateSelectedFamilyAndFontIndices(
        List<IFontFamilyId> fonts,
        string familyName,
        string fontName)
    {
        this.selectedFamilyIndex = fonts.FindIndex(x => x.ToString() == familyName);
        if (this.selectedFamilyIndex == -1)
        {
            this.selectedFontIndex = -1;
        }
        else
        {
            this.selectedFontIndex = -1;
            var family = fonts[this.selectedFamilyIndex];
            for (var i = 0; i < family.Fonts.Count; i++)
            {
                if (family.Fonts[i].ToString() == fontName)
                {
                    this.selectedFontIndex = i;
                    break;
                }
            }

            if (this.selectedFontIndex == -1)
            {
                this.selectedFontIndex = 0;
            }

            this.selectedFont = this.selectedFont with
            {
                FontId = fonts[this.selectedFamilyIndex].Fonts[this.selectedFontIndex],
            };
        }
    }

    /// <summary>
    /// Replaces the entire contents of an input-text callback buffer. Stand-in for Dalamud's
    /// <c>ImGuiHelpers.SetTextFromCallback</c>, which is not present in the referenced binding.
    /// </summary>
    private static unsafe void SetTextFromCallback(ref ImGuiInputTextCallbackData data, string text)
    {
        fixed (ImGuiInputTextCallbackData* p = &data)
        {
            ImGuiInputTextCallbackDataPtr ptr = p;
            ptr.DeleteChars(0, ptr.BufTextLen);
            ptr.InsertChars(0, text);
        }
    }

    private string ExtractName(IObjectWithLocalizableName what) =>
        what.GetLocalizedName(this.config.EffectiveLanguage);

    private bool TestName(IObjectWithLocalizableName what, string search) =>
        this.ExtractName(what).Contains(search, StringComparison.CurrentCultureIgnoreCase);

    private struct AdvancedOptionsUiState
    {
        public string OffsetXText;
        public string OffsetYText;
        public string LetterSpacingText;
        public string LineHeightText;

        public AdvancedOptionsUiState(SingleFontSpec spec)
        {
            this.OffsetXText = $"{spec.GlyphOffset.X:0.##}";
            this.OffsetYText = $"{spec.GlyphOffset.Y:0.##}";
            this.LetterSpacingText = $"{spec.LetterSpacing:0.##}";
            this.LineHeightText = $"{spec.LineHeight:0.##}";
        }
    }
}
