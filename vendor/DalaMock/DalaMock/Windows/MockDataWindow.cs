namespace DalaMock.Core.Windows;

/// <summary>
///     Class responsible for drawing the data/debug window.
/// </summary>
public class MockDataWindow : Window
{
    private readonly ILogger<MockDataWindow> logger;
    private readonly IImGuiComponents imGuiComponents;
    private readonly IOrderedEnumerable<IDataWindowWidget> orderedModules;

    private bool isExcept;

    private bool isLoaded;
    private bool selectionCollapsed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MockDataWindow" /> class.
    /// </summary>
    public MockDataWindow(IEnumerable<IDataWindowWidget> widgets, ILogger<MockDataWindow> logger, IImGuiComponents imGuiComponents)
        : base("Mock Data", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.logger = logger;
        this.imGuiComponents = imGuiComponents;
        this.Size = new Vector2(400, 300);
        this.SizeCondition = ImGuiCond.FirstUseEver;

        this.RespectCloseHotkey = false;
        this.orderedModules = widgets.OrderBy(module => module.DisplayName);
        this.CurrentWidget = this.orderedModules.FirstOrDefault();
    }

    /// <summary>Gets or sets the current widget.</summary>
    public IDataWindowWidget? CurrentWidget { get; set; }

    /// <summary>Gets the data window widget of the specified type.</summary>
    /// <typeparam name="T">Type of the data window widget to find.</typeparam>
    /// <returns>Found widget.</returns>
    public T GetWidget<T>() where T : IDataWindowWidget
    {
        foreach (var m in this.orderedModules)
        {
            if (m is T w)
            {
                return w;
            }
        }

        throw new ArgumentException($"No widget of type {typeof(T).FullName} found.");
    }

    /// <summary>
    ///     Set the DataKind dropdown menu.
    /// </summary>
    /// <param name="dataKind">Data kind name, can be lower and/or without spaces.</param>
    public void SetDataKind(string dataKind)
    {
        if (string.IsNullOrEmpty(dataKind))
        {
            return;
        }

        if (this.orderedModules.FirstOrDefault(module => module.IsWidgetCommand(dataKind)) is { } targetModule)
        {
            this.CurrentWidget = targetModule;
        }

        //Service<ChatGui>.Get().PrintError($"/xldata: Invalid data type {dataKind}");
    }

    public IMockService MockService { get; }

    public string Name => this.WindowName;

    /// <summary>
    ///     Draw the window via ImGui.
    /// </summary>
    public override void Draw()
    {
        this.Load();

        // Only draw the widget contents if the selection pane is collapsed.
        if (this.selectionCollapsed)
        {
            this.DrawContents();
            return;
        }

        using var table = ImRaii.Table("XlData_Table"u8, 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable);
        if (table)
        {
            ImGui.TableSetupColumn(
                "##SelectionColumn"u8,
                ImGuiTableColumnFlags.WidthFixed,
                200.0f * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("##ContentsColumn"u8, ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextColumn();
            this.DrawSelection();

            ImGui.TableNextColumn();
            this.DrawContents();
        }
    }

    private void DrawSelection()
    {
        using (var child = ImRaii.Child("XlData_SelectionPane"u8, ImGui.GetContentRegionAvail()))
        {
            if (child)
            {
                using(var listBox = ImRaii.ListBox("WidgetSelectionListbox"u8, ImGui.GetContentRegionAvail()))
                {
                    if (listBox)
                    {
                        foreach (var widget in this.orderedModules)
                        {
                            if (ImGui.Selectable(widget.DisplayName, this.CurrentWidget == widget))
                            {
                                this.CurrentWidget = widget;
                            }
                        }
                    }
                }
            }
        }
    }

    private void DrawContents()
    {
        using (var child = ImRaii.Child("XlData_ContentsPane"u8, ImGui.GetContentRegionAvail()))
        {
            if (child)
            {
                if (this.imGuiComponents.IconButton(
                        "collapse-expand",
                        this.selectionCollapsed ? FontAwesomeIcon.ArrowRight : FontAwesomeIcon.ArrowLeft))
                {
                    this.selectionCollapsed = !this.selectionCollapsed;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip($"{(this.selectionCollapsed ? "Expand" : "Collapse")} selection pane");
                }

                ImGui.SameLine();

                if (this.imGuiComponents.IconButton("forceReload", FontAwesomeIcon.Sync))
                {
                    this.isLoaded = false;
                    this.Load();
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Force Reload"u8);
                }

                ImGui.SameLine();

                var copy = this.imGuiComponents.IconButton("copyAll", FontAwesomeIcon.ClipboardList);

                ImGuiHelpers.ScaledDummy(10.0f);

                using (var innerChild = ImRaii.Child("XlData_WidgetContents"u8, ImGui.GetContentRegionAvail()))
                {
                    if (innerChild)
                    {
                        if (copy)
                        {
                            ImGui.LogToClipboard();
                        }

                        try
                        {
                            if (this.CurrentWidget is { Ready: true })
                            {
                                this.CurrentWidget.Draw();
                            }
                            else
                            {
                                ImGui.Text("Data not ready."u8);
                            }

                            this.isExcept = false;
                        }
                        catch (Exception ex)
                        {
                            if (!this.isExcept)
                            {
                                this.logger.LogError(ex, "Could not draw data");
                            }

                            this.isExcept = true;

                            ImGui.Text(ex.ToString());
                        }
                    }
                }
            }
        }
    }

    private void Load()
    {
        if (this.isLoaded)
        {
            return;
        }

        this.isLoaded = true;

        foreach (var widget in this.orderedModules)
        {
            widget.Load();
        }
    }
}
