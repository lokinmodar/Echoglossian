namespace DalaMock.Core.Windows;

public class MockGameGuiWindow : MockWindow<MockGameGui>
{
    private readonly MockGameGui mockGameGui;

    private int hoveredItemId;
    private bool hoveredItemIsHq;

    public MockGameGuiWindow(
        MockGameGui mockGameGui,
        string name,
        ImGuiWindowFlags flags = ImGuiWindowFlags.None,
        bool forceMainWindow = false)
        : base(mockGameGui, name, flags, forceMainWindow)
    {
        this.mockGameGui = mockGameGui;
    }

    public MockGameGuiWindow(MockGameGui mockGameGui)
        : base(mockGameGui, "Mock Game Gui")
    {
        this.mockGameGui = mockGameGui;
    }

    public override void Draw()
    {
        var hoveredItemId = this.hoveredItemId;
        var hoveredItemIsHq = this.hoveredItemIsHq;
        ImGui.InputInt("Hovered Item ID: ", ref hoveredItemId);
        ImGui.Checkbox("Hovered Item Is HQ?: ", ref hoveredItemIsHq);
        if (hoveredItemId != this.hoveredItemId || hoveredItemIsHq != this.hoveredItemIsHq)
        {
            this.hoveredItemId = hoveredItemId;
            this.hoveredItemIsHq = hoveredItemIsHq;
            this.mockGameGui.SetHoveredItem(
                (uint)this.hoveredItemId,
                this.hoveredItemIsHq ? InventoryItem.ItemFlags.HighQuality : InventoryItem.ItemFlags.None);
        }
    }
}
