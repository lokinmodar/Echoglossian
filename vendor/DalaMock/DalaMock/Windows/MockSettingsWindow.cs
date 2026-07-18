namespace DalaMock.Core.Windows;

/// <summary>Represents the validity state of a configured path.</summary>
public enum PathState
{
    /// <summary>The path is configured and valid.</summary>
    Valid,

    /// <summary>The path does not exist on disk.</summary>
    DoesNotExist,

    /// <summary>The path exists but is not valid for its intended purpose.</summary>
    Invalid,
}

/// <summary>
/// Provides a tab-based settings window for configuring DalaMock.
/// </summary>
public class MockSettingsWindow : Window
{
    private readonly MockDalamudConfiguration dalamudConfiguration;
    private readonly IFileDialogManager fileDialogManager;
    private readonly MockConfigurationManager configurationManager;
    private readonly MockKeyState mockKeyState;
    private readonly ImGuiScene imGuiScene;
    private readonly MockStyleManager styleManager;
    private readonly MockUiBuilder mockUiBuilder;
    private readonly IFontChooserFactory fontChooserFactory;

    private IFontChooserDialog? fontChooser;

    private int clientLanguageIndex;
    private bool createWindow;
    private string languageOverride = string.Empty;
    private float globalScale = 1f;

    private int styleSel;
    private ImGuiColorEditFlags styleAlphaFlags = ImGuiColorEditFlags.AlphaPreviewHalf;
    private string styleRenameText = string.Empty;
    private bool styleRenameModalDrawing;

    private static readonly string[] LanguageNames = ["English", "Japanese", "German", "French"];
    private static readonly ClientLanguage[] LanguageValues =
    [
        ClientLanguage.English,
        ClientLanguage.Japanese,
        ClientLanguage.German,
        ClientLanguage.French,
    ];

    public MockSettingsWindow(
        MockDalamudConfiguration dalamudConfiguration,
        IFileDialogManager fileDialogManager,
        MockConfigurationManager configurationManager,
        MockKeyState mockKeyState,
        ImGuiScene imGuiScene,
        MockStyleManager styleManager,
        MockUiBuilder mockUiBuilder,
        IFontChooserFactory fontChooserFactory)
        : base("DalaMock Settings", ImGuiWindowFlags.None, false)
    {
        this.dalamudConfiguration = dalamudConfiguration;
        this.fileDialogManager = fileDialogManager;
        this.configurationManager = configurationManager;
        this.mockKeyState = mockKeyState;
        this.imGuiScene = imGuiScene;
        this.styleManager = styleManager;
        this.mockUiBuilder = mockUiBuilder;
        this.fontChooserFactory = fontChooserFactory;

        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(520, 350),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        if (this.GetGamePathState() != PathState.Valid || this.GetPluginPathState() != PathState.Valid)
        {
            this.IsOpen = true;
        }
    }

    public override void OnOpen()
    {
        this.clientLanguageIndex = System.Array.IndexOf(LanguageValues, this.dalamudConfiguration.ClientLanguage);
        if (this.clientLanguageIndex < 0)
        {
            this.clientLanguageIndex = 0;
        }

        this.createWindow = this.dalamudConfiguration.CreateWindow;
        this.languageOverride = this.dalamudConfiguration.LanguageOverride ?? string.Empty;
        this.globalScale = this.imGuiScene.GlobalFontScale;

        this.styleSel = this.styleManager.Styles.FindIndex(x => x.Name == this.styleManager.ChosenStyle);
        if (this.styleSel < 0)
        {
            this.styleSel = 0;
        }
    }

    public override void Draw()
    {
        using (var tabBar = ImRaii.TabBar("##SettingsTabs"))
        {
            if (tabBar)
            {
                using (var tab = ImRaii.TabItem("General"))
                {
                    if (tab)
                    {
                        this.DrawGeneralTab();
                    }
                }

                using (var tab = ImRaii.TabItem("UI"))
                {
                    if (tab)
                    {
                        this.DrawUiTab();
                    }
                }

                using (var tab = ImRaii.TabItem("Fonts"))
                {
                    if (tab)
                    {
                        this.DrawFontsTab();
                    }
                }

                using (var tab = ImRaii.TabItem("Style Editor"))
                {
                    if (tab)
                    {
                        this.DrawStyleEditorTab();
                    }
                }

                using (var tab = ImRaii.TabItem("Paths"))
                {
                    if (tab)
                    {
                        this.DrawPathsTab();
                    }
                }
            }
        }

        ImGui.Separator();
        this.DrawSaveButton();

        this.DrawFontChooser();
    }

    private void DrawFontsTab()
    {
        using var child = ImRaii.Child("##FontsTabContent", new Vector2(0, -ImGui.GetFrameHeightWithSpacing() - ImGui.GetStyle().ItemSpacing.Y), false);

        ImGui.TextUnformatted("Default Font");
        using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1f)))
        {
            ImGui.TextWrapped(
                "Choose the default font from the game fonts, dalamud-provided fonts and your installed system fonts. ");
        }

        ImGui.NewLine();

        var currentFont = this.mockUiBuilder.DefaultFontSpec is SingleFontSpec currentSingle
            ? currentSingle.ToString()
            : "Default (AXIS game font)";
        ImGui.TextUnformatted($"Current: {currentFont}");

        ImGui.NewLine();

        using (ImRaii.Disabled(this.fontChooser is not null))
        {
            if (ImGui.Button("Choose Default Font##chooseDefaultFont"))
            {
                var dialog = this.fontChooserFactory.Create();
                if (this.mockUiBuilder.DefaultFontSpec is SingleFontSpec currentSpec)
                {
                    dialog.SelectedFont = currentSpec;
                }

                dialog.SetPopupPositionAndSizeToCurrentWindowCenter();
                this.fontChooser = dialog;
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Reset Default Font##resetDefaultFont"))
        {
            this.mockUiBuilder.DefaultFontSpec = new SingleFontSpec
            {
                FontId = new GameFontAndFamilyId(GameFontFamily.Axis),
            };
            this.imGuiScene.RequestDefaultFont(null);
            this.dalamudConfiguration.DefaultFont = null;
            this.configurationManager.SaveConfiguration(this.dalamudConfiguration);
        }
    }

    private void DrawFontChooser()
    {
        if (this.fontChooser is null)
        {
            return;
        }

        this.fontChooser.Draw();

        if (!this.fontChooser.ResultTask.IsCompleted)
        {
            return;
        }

        if (this.fontChooser.ResultTask.IsCompletedSuccessfully)
        {
            var chosen = this.fontChooser.ResultTask.Result;
            this.mockUiBuilder.DefaultFontSpec = chosen;
            this.imGuiScene.RequestDefaultFont(chosen);
            this.dalamudConfiguration.DefaultFont = MockDefaultFontConfig.FromSpec(chosen);
            this.configurationManager.SaveConfiguration(this.dalamudConfiguration);
        }

        this.fontChooser.Dispose();
        this.fontChooser = null;
    }

    private void DrawGeneralTab()
    {
        using var child = ImRaii.Child("##GeneralTabContent", new Vector2(0, -ImGui.GetFrameHeightWithSpacing() - ImGui.GetStyle().ItemSpacing.Y), false);

        ImGui.SetNextItemWidth(200);
        ImGui.Combo("Client Language##clientLang", ref this.clientLanguageIndex, LanguageNames, LanguageNames.Length);

        ImGui.NewLine();

        ImGui.Checkbox("Create ImGui Window##createWindow", ref this.createWindow);
        using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1f)))
        {
            ImGui.TextUnformatted("Requires restart to take effect.");
        }

        ImGui.NewLine();

        ImGui.SetNextItemWidth(120);
        ImGui.InputTextWithHint("Language Override##langOverride", "e.g. de, fr, ja", ref this.languageOverride, 8);
        using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1f)))
        {
            ImGui.TextUnformatted("Leave empty to use system locale.");
        }
    }

    private void DrawUiTab()
    {
        using var child = ImRaii.Child("##UiTabContent", new Vector2(0, -ImGui.GetFrameHeightWithSpacing() - ImGui.GetStyle().ItemSpacing.Y), false);

        ImGui.TextUnformatted("Global Scale");
        using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1f)))
        {
            ImGui.TextUnformatted("Scales the whole UI imitating dalamud's global UI scale.");
        }

        ImGui.NewLine();

        ImGui.SetNextItemWidth(220);

        ImGui.SliderFloat("Scale##globalScale", ref this.globalScale, 0.5f, 3f);
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            this.ApplyGlobalScale(this.globalScale);
        }

        ImGui.SameLine();
        ImGui.TextDisabled($"(applied: {this.imGuiScene.GlobalFontScale:0.00})");

        if (ImGui.Button("Reset to 100%##resetScale"))
        {
            this.globalScale = 1f;
            this.ApplyGlobalScale(1f);
        }
    }

    private void ApplyGlobalScale(float scale)
    {
        this.imGuiScene.RequestGlobalFontScale(scale);
        this.dalamudConfiguration.GlobalUiScale = scale;
        this.configurationManager.SaveConfiguration(this.dalamudConfiguration);
    }

    private void DrawStyleEditorTab()
    {
        var styles = this.styleManager.Styles;
        if (styles.Count == 0)
        {
            return;
        }

        if (this.styleSel < 0 || this.styleSel >= styles.Count)
        {
            this.styleSel = 0;
        }

        var isBuiltinStyle = this.styleManager.IsBuiltIn(this.styleSel);

        var styleNames = styles.Select(x => x.Name).ToArray();
        ImGui.TextUnformatted("Choose Style:");
        ImGui.SetNextItemWidth(250);
        if (ImGui.Combo("##styleChooserCombo", ref this.styleSel, styleNames, styleNames.Length))
        {
            styles[this.styleSel].Apply();
        }

        ImGui.SameLine();
        if (ImGui.Button("Add New##styleAddNew"))
        {
            this.CaptureCurrentStyle();
            var newStyle = StyleModelV1.Get();
            newStyle.Name = this.GenerateStyleName();
            styles.Add(newStyle);
            this.styleSel = styles.Count - 1;
            newStyle.Apply();
        }

        ImGui.SameLine();
        if (ImGui.Button("Import##styleImport"))
        {
            this.CaptureCurrentStyle();
            try
            {
                var clipboardText = ImGui.GetClipboardText();
                var imported = StyleModel.Deserialize(clipboardText);
                if (imported != null)
                {
                    if (string.IsNullOrEmpty(imported.Name))
                    {
                        imported.Name = this.GenerateStyleName();
                    }
                    else if (styles.Any(x => x.Name == imported.Name))
                    {
                        imported.Name = $"{imported.Name} (Imported)";
                    }

                    styles.Add(imported);
                    this.styleSel = styles.Count - 1;
                    imported.Apply();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not import style from clipboard.");
            }
        }

        using (ImRaii.Disabled(isBuiltinStyle))
        {
            ImGui.SameLine();
            if (ImGui.Button("Rename##styleRename") && !isBuiltinStyle)
            {
                this.styleRenameText = styles[this.styleSel].Name;
                this.styleRenameModalDrawing = true;
                ImGui.OpenPopup("Rename Style");
            }

            ImGui.SameLine();
            if (ImGui.Button("Copy##styleCopy"))
            {
                this.CaptureCurrentStyle();
                ImGui.SetClipboardText(styles[this.styleSel].Serialize());
            }

            ImGui.SameLine();
            if (ImGui.Button("Delete##styleDelete") && !isBuiltinStyle)
            {
                styles.RemoveAt(this.styleSel);
                this.styleSel = Math.Clamp(this.styleSel - 1, 0, styles.Count - 1);
                styles[this.styleSel].Apply();
            }
        }

        this.DrawStyleRenameModal();

        ImGui.Separator();

        var workStyle = styles[this.styleSel];
        workStyle.BuiltInColors ??= StyleModelV1.DalamudStandard.BuiltInColors;
        isBuiltinStyle = this.styleManager.IsBuiltIn(this.styleSel);

        if (isBuiltinStyle)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudOrange))
            {
                ImGui.TextUnformatted("Built-in styles cannot be edited. Add a new style first.");
            }
        }

        using (var tabBar = ImRaii.TabBar("##StyleEditorInnerTabs"))
        {
            if (tabBar)
            {
                using (var tab = ImRaii.TabItem("Variables"))
                {
                    if (tab)
                    {
                        this.DrawStyleVariables(isBuiltinStyle);
                    }
                }

                using (var tab = ImRaii.TabItem("Colors"))
                {
                    if (tab)
                    {
                        this.DrawStyleColors(workStyle, isBuiltinStyle);
                    }
                }
            }
        }

        using (ImRaii.Disabled(isBuiltinStyle))
        {
            if (ImGui.Button("Save Style##styleSave"))
            {
                this.CaptureCurrentStyle();
                this.styleManager.ChosenStyle = styles[this.styleSel].Name;
                this.styleManager.Save();
            }
        }

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1f)))
        {
            ImGui.TextUnformatted("Saving sets this as the style loaded on startup.");
        }
    }

    private void DrawStyleVariables(bool isBuiltinStyle)
    {
        using var disabled = ImRaii.Disabled(isBuiltinStyle);
        using var child = ImRaii.Child("##StyleVariables", new Vector2(0, -ImGui.GetFrameHeightWithSpacing() * 2), true, ImGuiWindowFlags.HorizontalScrollbar);
        if (!child)
        {
            return;
        }

        var style = ImGui.GetStyle();

        ImGui.SliderFloat2("WindowPadding", ref style.WindowPadding, 0.0f, 20.0f, "%.0f");
        ImGui.SliderFloat2("FramePadding", ref style.FramePadding, 0.0f, 20.0f, "%.0f");
        ImGui.SliderFloat2("CellPadding", ref style.CellPadding, 0.0f, 20.0f, "%.0f");
        ImGui.SliderFloat2("ItemSpacing", ref style.ItemSpacing, 0.0f, 20.0f, "%.0f");
        ImGui.SliderFloat2("ItemInnerSpacing", ref style.ItemInnerSpacing, 0.0f, 20.0f, "%.0f");
        ImGui.SliderFloat2("TouchExtraPadding", ref style.TouchExtraPadding, 0.0f, 10.0f, "%.0f");
        ImGui.SliderFloat("IndentSpacing", ref style.IndentSpacing, 0.0f, 30.0f, "%.0f");
        ImGui.SliderFloat("ScrollbarSize", ref style.ScrollbarSize, 1.0f, 20.0f, "%.0f");
        ImGui.SliderFloat("GrabMinSize", ref style.GrabMinSize, 1.0f, 20.0f, "%.0f");

        ImGui.TextUnformatted("Borders");
        ImGui.SliderFloat("WindowBorderSize", ref style.WindowBorderSize, 0.0f, 1.0f, "%.0f");
        ImGui.SliderFloat("ChildBorderSize", ref style.ChildBorderSize, 0.0f, 1.0f, "%.0f");
        ImGui.SliderFloat("PopupBorderSize", ref style.PopupBorderSize, 0.0f, 1.0f, "%.0f");
        ImGui.SliderFloat("FrameBorderSize", ref style.FrameBorderSize, 0.0f, 1.0f, "%.0f");
        ImGui.SliderFloat("TabBorderSize", ref style.TabBorderSize, 0.0f, 1.0f, "%.0f");

        ImGui.TextUnformatted("Rounding");
        ImGui.SliderFloat("WindowRounding", ref style.WindowRounding, 0.0f, 12.0f, "%.0f");
        ImGui.SliderFloat("ChildRounding", ref style.ChildRounding, 0.0f, 12.0f, "%.0f");
        ImGui.SliderFloat("FrameRounding", ref style.FrameRounding, 0.0f, 12.0f, "%.0f");
        ImGui.SliderFloat("PopupRounding", ref style.PopupRounding, 0.0f, 12.0f, "%.0f");
        ImGui.SliderFloat("ScrollbarRounding", ref style.ScrollbarRounding, 0.0f, 12.0f, "%.0f");
        ImGui.SliderFloat("GrabRounding", ref style.GrabRounding, 0.0f, 12.0f, "%.0f");
        ImGui.SliderFloat("LogSliderDeadzone", ref style.LogSliderDeadzone, 0.0f, 12.0f, "%.0f");
        ImGui.SliderFloat("TabRounding", ref style.TabRounding, 0.0f, 12.0f, "%.0f");

        ImGui.TextUnformatted("Alignment");
        ImGui.SliderFloat2("WindowTitleAlign", ref style.WindowTitleAlign, 0.0f, 1.0f, "%.2f");
        var windowMenuButtonPosition = (int)style.WindowMenuButtonPosition + 1;
        if (ImGui.Combo("WindowMenuButtonPosition", ref windowMenuButtonPosition, ["None", "Left", "Right"], 3))
        {
            style.WindowMenuButtonPosition = (ImGuiDir)(windowMenuButtonPosition - 1);
        }

        ImGui.SliderFloat2("ButtonTextAlign", ref style.ButtonTextAlign, 0.0f, 1.0f, "%.2f");
        ImGui.SliderFloat2("SelectableTextAlign", ref style.SelectableTextAlign, 0.0f, 1.0f, "%.2f");
        ImGui.SliderFloat2("DisplaySafeAreaPadding", ref style.DisplaySafeAreaPadding, 0.0f, 30.0f, "%.0f");
    }

    private void DrawStyleColors(StyleModel workStyle, bool isBuiltinStyle)
    {
        using var disabled = ImRaii.Disabled(isBuiltinStyle);
        using var child = ImRaii.Child("##StyleColors", new Vector2(0, -ImGui.GetFrameHeightWithSpacing() * 2), true, ImGuiWindowFlags.HorizontalScrollbar);
        if (!child)
        {
            return;
        }

        if (ImGui.RadioButton("Opaque", this.styleAlphaFlags == ImGuiColorEditFlags.None))
        {
            this.styleAlphaFlags = ImGuiColorEditFlags.None;
        }

        ImGui.SameLine();
        if (ImGui.RadioButton("Alpha", this.styleAlphaFlags == ImGuiColorEditFlags.AlphaPreview))
        {
            this.styleAlphaFlags = ImGuiColorEditFlags.AlphaPreview;
        }

        ImGui.SameLine();
        if (ImGui.RadioButton("Both", this.styleAlphaFlags == ImGuiColorEditFlags.AlphaPreviewHalf))
        {
            this.styleAlphaFlags = ImGuiColorEditFlags.AlphaPreviewHalf;
        }

        var style = ImGui.GetStyle();
        foreach (var imGuiCol in Enum.GetValues<ImGuiCol>())
        {
            if (imGuiCol == ImGuiCol.Count)
            {
                continue;
            }

            ImGui.PushID(imGuiCol.ToString());
            ImGui.ColorEdit4("##color", ref style.Colors[(int)imGuiCol], ImGuiColorEditFlags.AlphaBar | this.styleAlphaFlags);
            ImGui.SameLine(0.0f, style.ItemInnerSpacing.X);
            ImGui.TextUnformatted(imGuiCol.ToString());
            ImGui.PopID();
        }

        ImGui.Separator();

        foreach (var property in typeof(DalamudColors).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            ImGui.PushID(property.Name);

            var colorVal = property.GetValue(workStyle.BuiltInColors);
            if (colorVal == null)
            {
                colorVal = property.GetValue(StyleModelV1.DalamudStandard.BuiltInColors);
                property.SetValue(workStyle.BuiltInColors, colorVal);
            }

            var color = (Vector4)colorVal!;
            if (ImGui.ColorEdit4("##color", ref color, ImGuiColorEditFlags.AlphaBar | this.styleAlphaFlags))
            {
                property.SetValue(workStyle.BuiltInColors, color);
                workStyle.BuiltInColors?.Apply();
            }

            ImGui.SameLine(0.0f, style.ItemInnerSpacing.X);
            ImGui.TextUnformatted(property.Name);
            ImGui.PopID();
        }
    }

    private void DrawStyleRenameModal()
    {
        if (!ImGui.BeginPopupModal("Rename Style", ref this.styleRenameModalDrawing, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar))
        {
            return;
        }

        ImGui.TextUnformatted("Please enter a new name for this style.");
        ImGui.Spacing();
        ImGui.InputText("##styleRenameInput", ref this.styleRenameText, 255);

        if (ImGui.Button("OK##styleRenameOk", new Vector2(120, 0)))
        {
            if (!string.IsNullOrWhiteSpace(this.styleRenameText) && !this.styleManager.IsBuiltIn(this.styleSel))
            {
                this.styleManager.Styles[this.styleSel].Name = this.styleRenameText;
            }

            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private void CaptureCurrentStyle()
    {
        if (this.styleManager.IsBuiltIn(this.styleSel))
        {
            return;
        }

        var styles = this.styleManager.Styles;
        var captured = StyleModelV1.Get();
        captured.Name = styles[this.styleSel].Name;
        styles[this.styleSel] = captured;
    }

    private string GenerateStyleName()
    {
        var styles = this.styleManager.Styles;
        var index = 1;
        string name;
        do
        {
            name = $"New Style {index++}";
        }
        while (styles.Any(x => x.Name == name));

        return name;
    }

    private void DrawPathsTab()
    {
        this.fileDialogManager.Draw();
        this.DrawGamePathSelector();
        this.DrawPluginPathSelector();
    }

    private void DrawSaveButton()
    {
        if (ImGui.Button("Save"))
        {
            this.dalamudConfiguration.ClientLanguage = LanguageValues[this.clientLanguageIndex];
            this.dalamudConfiguration.CreateWindow = this.createWindow;
            this.dalamudConfiguration.LanguageOverride = string.IsNullOrEmpty(this.languageOverride) ? null : this.languageOverride;
            this.configurationManager.SaveConfiguration(this.dalamudConfiguration);
            this.IsOpen = false;
        }
    }

    private void DrawGamePathSelector()
    {
        var gamePath = this.dalamudConfiguration.GamePath?.FullName ?? string.Empty;
        using var gamePathDisabled = ImRaii.Disabled(false);

        if (ImGui.InputTextWithHint("Game Path##gp", "Please enter your game path", ref gamePath, 999))
        {
            if (gamePath != (this.dalamudConfiguration.GamePath?.FullName ?? string.Empty))
            {
                this.dalamudConfiguration.GamePath = gamePath == string.Empty ? null : new DirectoryInfo(gamePath);
                this.configurationManager.SaveConfiguration(this.dalamudConfiguration);
            }
        }

        if (ImGui.Button("Select Folder##gamePathSelector"))
        {
            this.fileDialogManager.OpenFolderDialog(
                "Select Folder",
                (b, s) =>
                {
                    if (b)
                    {
                        if (s != (this.dalamudConfiguration.GamePath?.FullName ?? string.Empty))
                        {
                            this.dalamudConfiguration.GamePath = s == string.Empty ? null : new DirectoryInfo(s);
                            this.configurationManager.SaveConfiguration(this.dalamudConfiguration);
                        }
                    }
                });
        }

        var tooltip = "Must be the game/sqpack directory";
        if (tooltip.Length > 0 && ImGui.IsItemHovered(ImGuiHoveredFlags.None))
        {
            using var tt = ImRaii.Tooltip();
            ImGui.TextUnformatted(tooltip);
        }

        ImGui.TextUnformatted(this.GetGamePathStatus());

        ImGui.NewLine();
    }

    private PathState GetGamePathState()
    {
        var path = this.dalamudConfiguration.GamePath?.FullName ?? string.Empty;
        if (path != string.Empty && !Directory.Exists(path))
        {
            return PathState.DoesNotExist;
        }

        if (!this.dalamudConfiguration.GamePathValid)
        {
            return PathState.Invalid;
        }

        return PathState.Valid;
    }

    private PathState GetPluginPathState()
    {
        var path = this.dalamudConfiguration.PluginSavePath?.FullName ?? string.Empty;
        if (path != string.Empty && !Directory.Exists(path))
        {
            return PathState.DoesNotExist;
        }

        if (!this.dalamudConfiguration.PluginSavePathValid)
        {
            return PathState.Invalid;
        }

        return PathState.Valid;
    }

    private string GetGamePathStatus() => this.GetGamePathState() switch
    {
        PathState.DoesNotExist => "The configured path does not exist.",
        PathState.Invalid => "The configured path is not valid.",
        _ => "The configured path is valid.",
    };

    private string GetPluginPathStatus() => this.GetPluginPathState() switch
    {
        PathState.DoesNotExist => "The configured path does not exist.",
        PathState.Invalid => "The configured path is not valid.",
        _ => "The configured path is valid.",
    };

    private void DrawPluginPathSelector()
    {
        var pluginSavePath = this.dalamudConfiguration.PluginSavePath?.FullName ?? string.Empty;

        if (ImGui.InputTextWithHint("Plugin Save Path##psp", "Please enter the default plugin save path", ref pluginSavePath, 999))
        {
            if (pluginSavePath != (this.dalamudConfiguration.PluginSavePath?.FullName ?? string.Empty))
            {
                this.dalamudConfiguration.PluginSavePath = pluginSavePath == string.Empty ? null : new DirectoryInfo(pluginSavePath);
                this.configurationManager.SaveConfiguration(this.dalamudConfiguration);
            }
        }

        if (ImGui.Button("Select Folder##pluginSelectFolder"))
        {
            this.fileDialogManager.OpenFolderDialog(
                "Select Folder",
                (b, s) =>
                {
                    if (b)
                    {
                        if (s != (this.dalamudConfiguration.PluginSavePath?.FullName ?? string.Empty))
                        {
                            this.dalamudConfiguration.PluginSavePath = s == string.Empty ? null : new DirectoryInfo(s);
                            this.configurationManager.SaveConfiguration(this.dalamudConfiguration);
                        }
                    }
                });
        }

        ImGui.TextUnformatted(this.GetPluginPathStatus());

        ImGui.NewLine();
    }
}
