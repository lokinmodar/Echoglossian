# DalaMock

DalaMock is a library designed to load Dalamud plugins outside the game environment. It can operate with or without a user interface.

Without UI: Ideal for running unit tests, enabling automated and isolated testing of plugin functionality.  
With UI: Facilitates rapid development and debugging of ImGui-based interfaces, significantly speeding up the design process.

### Sample

    private static void Main(string[] args)
    {
        var dalamudConfiguration = new MockDalamudConfiguration();
        var mockContainer = new MockContainer(dalamudConfiguration);
        var mockDalamudUi = mockContainer.GetMockUi();
        var pluginLoader = mockContainer.GetPluginLoader();
        var mockPlugin = pluginLoader.AddPlugin(typeof(DalamudPluginTest));
        mockDalamudUi.Run();
    }