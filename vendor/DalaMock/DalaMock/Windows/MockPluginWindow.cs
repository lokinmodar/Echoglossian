namespace DalaMock.Core.Windows;

/// <summary>
/// Provides a window for managing and interacting with DalaMock plugins.
/// </summary>
public class MockPluginWindow : Window
{
    private readonly PluginLoader pluginLoader;
    private readonly MockDalamudConfiguration dalamudConfiguration;
    private readonly MockPluginFaultWindow faultWindow;

    public MockPluginWindow(PluginLoader pluginLoader, MockDalamudConfiguration dalamudConfiguration, MockPluginFaultWindow faultWindow)
        : base("DalaMock Plugins", ImGuiWindowFlags.None, false)
    {
        this.pluginLoader = pluginLoader;
        this.dalamudConfiguration = dalamudConfiguration;
        this.faultWindow = faultWindow;

        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(700, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        this.IsOpen = true;
    }

    public Type? GetPluginBaseType(Type type)
    {
        if (type.BaseType == null)
        {
            return null;
        }
        if (type.BaseType.GetInterface("IDalamudPlugin") != null || type.BaseType.GetInterface("IAsyncDalamudPlugin") != null)
        {
            return type.BaseType;
        }

        return null;
    }

    public override void Draw()
    {
        ImGui.TextUnformatted($"Plugins ({this.pluginLoader.LoadedPlugins.Count} registered)");
        ImGui.Separator();

        using var child = ImRaii.Child("##PluginList", Vector2.Zero, false);
        using var table = ImRaii.Table(
            "##PluginsTable",
            7,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp);

        if (!table)
        {
            return;
        }

        ImGui.TableSetupColumn("Plugin", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Base Plugin", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 90f);
        ImGui.TableSetupColumn("##load", ImGuiTableColumnFlags.WidthFixed, 80f);
        ImGui.TableSetupColumn("##settings", ImGuiTableColumnFlags.WidthFixed, 75f);
        ImGui.TableSetupColumn("##mainui", ImGuiTableColumnFlags.WidthFixed, 75f);
        ImGui.TableSetupColumn("##fault", ImGuiTableColumnFlags.WidthFixed, 80f);
        ImGui.TableHeadersRow();

        foreach (var (pluginType, plugin) in this.pluginLoader.LoadedPlugins)
        {
            ImGui.TableNextRow();
            using var id = ImRaii.PushId(pluginType.Name);

            ImGui.TableSetColumnIndex(0);
            ImGui.TextUnformatted(pluginType.Name);

            ImGui.TableSetColumnIndex(1);
            ImGui.TextUnformatted(this.GetPluginBaseType(pluginType)?.Name ?? "Unknown");

            ImGui.TableSetColumnIndex(2);
            this.DrawStatus(plugin);

            ImGui.TableSetColumnIndex(3);
            using (ImRaii.Disabled((!this.dalamudConfiguration.GamePathValid && !plugin.IsLoaded) || plugin.IsTransitioning))
            {
                if (ImGui.Button(plugin.IsLoaded ? "Unload##btn" : "Load##btn", new Vector2(-1, 0)))
                {
                    if (plugin.IsLoaded)
                    {
                        plugin.BeginTransition(PluginTransitionState.Unloading);
                        _ = this.pluginLoader.StopPlugin(plugin).ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                            {
                                var ex = t.Exception!.InnerException ?? t.Exception;
                                plugin.SetFault(ex);
                                this.faultWindow.ShowForPlugin(plugin);
                            }
                            else
                            {
                                plugin.EndTransition();
                            }
                        });
                    }
                    else
                    {
                        plugin.BeginTransition(PluginTransitionState.Loading);
                        _ = this.pluginLoader.StartPlugin(plugin).ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                            {
                                var ex = t.Exception!.InnerException ?? t.Exception;
                                plugin.SetFault(ex);
                                this.faultWindow.ShowForPlugin(plugin);
                            }
                            else
                            {
                                plugin.EndTransition();
                            }
                        });
                    }
                }
            }

            ImGui.TableSetColumnIndex(4);
            using (ImRaii.Disabled(!plugin.IsLoaded))
            {
                if (ImGui.Button("Settings##btn", new Vector2(-1, 0)) && plugin.Container != null)
                {
                    plugin.Container.Resolve<MockUiBuilder>().FireOpenConfigUiEvent();
                }
            }

            ImGui.TableSetColumnIndex(5);
            using (ImRaii.Disabled(!plugin.IsLoaded))
            {
                if (ImGui.Button("Main UI##btn", new Vector2(-1, 0)) && plugin.Container != null)
                {
                    plugin.Container.Resolve<MockUiBuilder>().FireOpenMainUiEvent();
                }
            }

            ImGui.TableSetColumnIndex(6);
            using (ImRaii.Disabled(!plugin.IsFaulted))
            {
                if (ImGui.Button("View Error##btn", new Vector2(-1, 0)))
                {
                    this.faultWindow.ShowForPlugin(plugin);
                }
            }
        }
    }

    private void DrawStatus(MockPlugin plugin)
    {
        if (plugin.TransitionState == PluginTransitionState.Loading)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudYellow))
            {
                ImGui.TextUnformatted("Loading...");
            }
        }
        else if (plugin.TransitionState == PluginTransitionState.Unloading)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudYellow))
            {
                ImGui.TextUnformatted("Unloading...");
            }
        }
        else if (plugin.IsFaulted)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed))
            {
                ImGui.TextUnformatted("Faulted");
            }
        }
        else if (plugin.IsLoaded)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.2f, 1f, 0.2f, 1f)))
            {
                ImGui.TextUnformatted("Loaded");
            }
        }
        else
        {
            using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1f)))
            {
                ImGui.TextUnformatted("Unloaded");
            }
        }
    }
}
