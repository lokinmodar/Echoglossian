namespace DalaMock.Core.Interface;

public class MockMainMenuBar
{
    private readonly MockSettingsWindow mockSettingsWindow;
    private readonly MockPluginWindow mockPluginWindow;
    private readonly MockDataWindow mockDataWindow;
    private bool showDemoWindow;

    public MockMainMenuBar(MockSettingsWindow mockSettingsWindow, MockPluginWindow mockPluginWindow, MockDataWindow mockDataWindow)
    {
        this.mockSettingsWindow = mockSettingsWindow;
        this.mockPluginWindow = mockPluginWindow;
        this.mockDataWindow = mockDataWindow;
    }

    public void Draw()
    {
        using var menuBar = ImRaii.MainMenuBar();
        if (!menuBar)
        {
            return;
        }

        if (ImGui.MenuItem("Configuration"))
        {
            this.mockSettingsWindow.Toggle();
        }

        if (ImGui.MenuItem("Plugin Loader"))
        {
            this.mockPluginWindow.Toggle();
        }

        if (ImGui.MenuItem("Data Window"))
        {
            this.mockDataWindow.Toggle();
        }

        ImGui.MenuItem("Demo Window", string.Empty, ref this.showDemoWindow);

        if (this.showDemoWindow)
        {
            ImGui.ShowDemoWindow(ref this.showDemoWindow);
        }
    }
}
