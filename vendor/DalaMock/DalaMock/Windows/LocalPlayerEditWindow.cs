namespace DalaMock.Core.Windows;

public class LocalPlayerEditWindow : Window
{
    private readonly MockClientState mockClientState;
    private MockCharacter? mockCharacter;
    private readonly List<IGrouping<string, IImGuiElement>> characterElements;
    private bool isNew = false;
    private TaskCompletionSource<MockCharacter?> mockCharacterTcs;

    public LocalPlayerEditWindow(ImGuiElementGenerator elementGenerator, MockClientState mockClientState)
        : base("Local Player Edit")
    {
        this.mockClientState = mockClientState;
        this.mockCharacterTcs = new TaskCompletionSource<MockCharacter?>();
        this.characterElements = elementGenerator.GenerateElements(typeof(MockCharacter)).OrderBy(c => c.Name).GroupBy(c => c.Group).OrderBy(c => c.Key).ToList();
        this.Size = new Vector2(500, 500);
    }

    public void EditCharacter(MockCharacter mockCharacter)
    {
        this.mockCharacter = mockCharacter;
        this.isNew = false;
        this.IsOpen = true;
    }

    public Task<MockCharacter?> CreateCharacter()
    {
        this.mockCharacterTcs.TrySetCanceled();
        this.mockCharacterTcs = new();
        this.mockCharacter = new MockCharacter(this.mockClientState); // TODO: Use a factory
        this.isNew = false;
        this.IsOpen = true;
        return this.mockCharacterTcs.Task;
    }

    public override void Draw()
    {
        if (this.mockCharacter == null)
        {
            this.IsOpen = false;
            return;
        }

        ImGui.Text(this.isNew ? "New Character" : "Edit Character");
        foreach (var group in this.characterElements)
        {
            ImGui.Text(group.Key);
            foreach (var element in group)
            {
                element.Draw(this.mockCharacter);
            }
        }

        if (ImGui.Button("Save"))
        {
            this.mockCharacterTcs.TrySetResult(this.mockCharacter);
            this.IsOpen = false;
        }
    }
}
