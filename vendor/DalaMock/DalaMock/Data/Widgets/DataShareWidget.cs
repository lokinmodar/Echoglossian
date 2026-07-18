namespace DalaMock.Core.Data.Widgets;

/// <summary>
/// Widget for displaying plugin data share modules.
/// </summary>
internal class DataShareWidget : IDataWindowWidget
{
    private readonly Lazy<MockDataShare> dataShare;
    private readonly ILogger<DataShareWidget> logger;
    private const ImGuiTabItemFlags NoCloseButton = (ImGuiTabItemFlags)ImGuiTabItemFlagsPrivate.NoCloseButton;

    private readonly List<(string Name, byte[]? Data)> dataView = [];
    private int nextTab = -1;

    /// <inheritdoc/>
    public string[]? CommandShortcuts { get; init; } = ["datashare"];

    /// <inheritdoc/>
    public string DisplayName { get; init; } = "Data Share & Call Gate";

    /// <inheritdoc/>
    public bool Ready { get; set; }

    /// <inheritdoc/>
    public void Load()
    {
        this.Ready = true;
    }

    public DataShareWidget(Lazy<MockDataShare> dataShare, ILogger<DataShareWidget> logger)
    {
        this.dataShare = dataShare;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public void Draw()
    {
        using var tabbar = ImRaii.TabBar("##tabbar"u8);
        if (!tabbar.Success)
        {
            return;
        }

        var d = true;
        using (var tabItem = ImRaii.TabItem("Data Share##tabbar-datashare"u8, ref d, NoCloseButton | (this.nextTab == 0 ? ImGuiTabItemFlags.SetSelected : 0)))
        {
            if (tabItem.Success)
            {
                this.DrawDataShare();
            }
        }

        for (var i = 0; i < this.dataView.Count; i++)
        {
            using var idpush = ImRaii.PushId($"##tabbar-data-{i}");

            var (name, data) = this.dataView[i];
            d = true;

            using var tabitem = ImRaii.TabItem(name, ref d, this.nextTab == 2 + i ? ImGuiTabItemFlags.SetSelected : 0);

            if (!d)
            {
                this.dataView.RemoveAt(i--);
            }

            if (!tabitem.Success)
            {
                continue;
            }

            if (ImGui.Button("Refresh"u8))
            {
                data = null;
            }

            if (data is null)
            {
                try
                {
                    var data2 = this.dataShare.Value.GetData<object>(name, new DataCachePluginId("DataShareWidget", Guid.Empty));
                    try
                    {
                        data = Encoding.UTF8.GetBytes(
                            JsonConvert.SerializeObject(
                                data2,
                                Formatting.Indented,
                                new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All }));
                    }
                    finally
                    {
                        this.dataShare.Value.RelinquishData(name, new DataCachePluginId("DataShareWidget", Guid.Empty));
                    }
                }
                catch (Exception e)
                {
                    data = Encoding.UTF8.GetBytes(e.ToString());
                }

                this.dataView[i] = (name, data);
            }

            ImGui.SameLine();
            if (ImGui.Button("Copy"u8))
            {
                ImGui.SetClipboardText(data);
            }

            ImGui.InputTextMultiline("text"u8, data, ImGui.GetContentRegionAvail(), ImGuiInputTextFlags.ReadOnly);
        }

        this.nextTab = -1;
    }

    private void DrawTextCell(string s, Func<string>? tooltip = null, bool framepad = false)
    {
        ImGui.TableNextColumn();
        var offset = ImGui.GetCursorScreenPos() + new Vector2(0, framepad ? ImGui.GetStyle().FramePadding.Y : 0);
        if (framepad)
        {
            ImGui.AlignTextToFramePadding();
        }

        ImGui.Text(s);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetNextWindowPos(offset - ImGui.GetStyle().WindowPadding);
            var vp = ImGui.GetWindowViewport();
            var wrx = (vp.WorkPos.X + vp.WorkSize.X) - offset.X;

            ImGui.SetNextWindowSizeConstraints(Vector2.One, new(wrx, float.MaxValue));
            using (ImRaii.Tooltip())
            {
                using var pushedWrap = ImRaii.TextWrapPos(wrx);
                ImGui.TextWrapped(tooltip?.Invoke() ?? s);
            }
        }

        if (ImGui.IsItemClicked())
        {
            ImGui.SetClipboardText(tooltip?.Invoke() ?? s);
            this.logger.LogInformation("Copied {TableName} to clipboard.", ImGui.TableGetColumnName());
        }
    }

    private void DrawDataShare()
    {
        using var table = ImRaii.Table("###DataShareTable"u8, 5, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg);
        if (!table.Success)
        {
            return;
        }

        ImGui.TableSetupColumn("Shared Tag"u8);
        ImGui.TableSetupColumn("Show"u8);
        ImGui.TableSetupColumn("Creator Assembly"u8);
        ImGui.TableSetupColumn("#"u8, ImGuiTableColumnFlags.WidthFixed, 30 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Consumers"u8);
        ImGui.TableHeadersRow();

        foreach (var share in this.dataShare.Value.GetAllShares())
        {
            ImGui.TableNextRow();
            this.DrawTextCell(share.Tag, null, true);

            ImGui.TableNextColumn();
            if (ImGui.Button($"Show##datasharetable-show-{share.Tag}"))
            {
                var index = 0;
                for (; index < this.dataView.Count; index++)
                {
                    if (this.dataView[index].Name == share.Tag)
                    {
                        break;
                    }
                }

                if (index == this.dataView.Count)
                {
                    this.dataView.Add((share.Tag, null));
                }
                else
                {
                    this.dataView[index] = (share.Tag, null);
                }

                this.nextTab = 2 + index;
            }

            this.DrawTextCell(share.CreatorPluginId.InternalName, () => share.CreatorPluginId.EffectiveWorkingId.ToString(), true);
            this.DrawTextCell(share.UserPluginIds.Length.ToString(), null, true);
            this.DrawTextCell(string.Join(", ", share.UserPluginIds.Select(c => c.InternalName)), () => string.Join("\n", share.UserPluginIds.Select(c => $"{c.InternalName} ({c.EffectiveWorkingId.ToString()}")), true);
        }
    }
}
