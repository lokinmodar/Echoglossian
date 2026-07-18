namespace DalaMock.Core.Windows;

/// <summary>
/// Displays exception details when a plugin load or unload operation faults.
/// </summary>
public class MockPluginFaultWindow : Window
{
    private MockPlugin? plugin;

    public MockPluginFaultWindow()
        : base("Plugin Fault##DalaMock", ImGuiWindowFlags.None, false)
    {
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(500, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void ShowForPlugin(MockPlugin target)
    {
        this.plugin = target;
        this.WindowName = $"Plugin Fault: {target.PluginType.Name}##DalaMock";
        this.IsOpen = true;
    }

    public override void Draw()
    {
        if (this.plugin == null)
        {
            return;
        }

        ImGui.TextUnformatted($"Plugin: {this.plugin.PluginType.Name}");
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4);

        if (this.plugin.LastException == null)
        {
            ImGui.TextUnformatted("No exception details available.");
            return;
        }

        using var child = ImRaii.Child("##FaultDetails", new Vector2(0, -ImGui.GetFrameHeightWithSpacing()), true);
        using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudGrey))
        {
            ImGui.TextWrapped(this.plugin.LastException.ToString());
        }

        ImGuiHelpers.ScaledDummy(4);
        if (ImGui.Button("Copy##fault"))
        {
            ImGui.SetClipboardText(this.plugin.LastException.ToString());
        }

        ImGui.SameLine();
        if (ImGui.Button("Clear Fault##fault"))
        {
            this.plugin.ClearFault();
            this.IsOpen = false;
        }
    }
}
