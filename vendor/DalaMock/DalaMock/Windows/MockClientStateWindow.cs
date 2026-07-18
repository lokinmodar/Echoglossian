namespace DalaMock.Core.Windows;

public class MockClientStateWindow : MockWindow<MockClientState>
{
    private readonly MockClientState mockClientState;
    private readonly LocalPlayersWindow localPlayersWindow;
    private readonly ExcelSheet<TerritoryType> territoryTypeSheet;
    private Dictionary<uint, string>? territoryTypes;
    private MockCharacter character;

    public MockClientStateWindow(MockClientState mockClientState, IDataManager dataManager, LocalPlayersWindow localPlayersWindow)
        : base(
        mockClientState,
        "Mock Client State")
    {
        this.mockClientState = mockClientState;
        this.localPlayersWindow = localPlayersWindow;
        this.territoryTypeSheet = dataManager.GetExcelSheet<TerritoryType>()!;
        this.character = new MockCharacter(mockClientState); // make factory later
    }

    public Dictionary<uint, string> TerritoryTypes => this.territoryTypes ??= this.territoryTypeSheet
        .Where(c => c.TerritoryIntendedUse.RowId is 0 or 1 or 13 or 14)
        .ToDictionary(c => c.RowId, c => c.Name.ToString());

    public override void Draw()
    {
        ImGui.Text("Client State");
        ImGui.Separator();

        // ClientLanguage
        ImGui.Text("Client Language:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        using (var combo = ImRaii.Combo("##ClientLanguage", this.mockClientState.ClientLanguage.ToString()))
        {
            if (combo)
            {
                if (ImGui.Selectable(ClientLanguage.English.ToString()))
                {
                    this.mockClientState.ClientLanguage = ClientLanguage.English;
                }

                if (ImGui.Selectable(ClientLanguage.Japanese.ToString()))
                {
                    this.mockClientState.ClientLanguage = ClientLanguage.Japanese;
                }

                if (ImGui.Selectable(ClientLanguage.French.ToString()))
                {
                    this.mockClientState.ClientLanguage = ClientLanguage.French;
                }

                if (ImGui.Selectable(ClientLanguage.German.ToString()))
                {
                    this.mockClientState.ClientLanguage = ClientLanguage.German;
                }
            }
        }

        // TerritoryType
        uint territoryType = this.mockClientState.TerritoryType;
        ImGui.Text("Territory Type: ");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        using (var combo = ImRaii.Combo(
                   "##TerritoryType",
                   this.TerritoryTypes.ContainsKey(this.mockClientState.TerritoryType)
                       ? this.TerritoryTypes[this.mockClientState.TerritoryType]
                       : "None Selected"))
        {
            if (combo)
            {
                foreach (var territoryTypeName in this.TerritoryTypes)
                {
                    if (ImGui.Selectable(territoryTypeName.Value))
                    {
                        this.mockClientState.TerritoryType = (ushort)territoryTypeName.Key;
                    }
                }
            }
        }

        // MapId
        var mapId = (int)this.mockClientState.MapId;
        ImGui.Text($"Map ID: {mapId}");
        ImGui.SameLine();
        if (ImGui.InputInt("##MapId", ref mapId))
        {
            this.mockClientState.MapId = (uint)mapId;
        }

        // LocalPlayer
        ImGui.Text($"Local Player: {this.mockClientState.LocalPlayer?.Name ?? "None"}");
        ImGui.SameLine();
        if (ImGui.Button("Manage##LocalPlayer"))
        {
            this.localPlayersWindow.IsOpen = true;
        }

        // LocalContentId
        var localContentId = this.mockClientState.LocalContentId.ToString();
        ImGui.Text($"Local Content ID: {localContentId}");
        ImGui.SameLine();
        if (ImGui.InputText("##LocalContentId", ref localContentId, 100))
        {
            if (ulong.TryParse(localContentId, out var result))
            {
                this.mockClientState.LocalContentId = result;
            }
        }

        // IsLoggedIn
        var isLoggedIn = this.mockClientState.IsLoggedIn;
        ImGui.Text($"Is Logged In: {isLoggedIn}");
        ImGui.SameLine();
        if (ImGui.Checkbox("##IsLoggedIn", ref isLoggedIn))
        {
            this.mockClientState.IsLoggedIn = isLoggedIn;
        }

        // IsPvP
        var isPvP = this.mockClientState.IsPvP;
        ImGui.Text($"Is PvP: {isPvP}");
        ImGui.SameLine();
        if (ImGui.Checkbox("##IsPvP", ref isPvP))
        {
            this.mockClientState.IsPvP = isPvP;
        }

        // IsPvPExcludingDen
        var isPvPExcludingDen = this.mockClientState.IsPvPExcludingDen;
        ImGui.Text($"Is PvP Excluding Den: {isPvPExcludingDen}");
        ImGui.SameLine();
        if (ImGui.Checkbox("##IsPvPExcludingDen", ref isPvPExcludingDen))
        {
            this.mockClientState.IsPvPExcludingDen = isPvPExcludingDen;
        }

        // IsGPosing
        var isGPosing = this.mockClientState.IsGPosing;
        ImGui.Text($"Is GPosing: {isGPosing}");
        ImGui.SameLine();
        if (ImGui.Checkbox("##IsGPosing", ref isGPosing))
        {
            this.mockClientState.IsGPosing = isGPosing;
        }
    }
}
