namespace DalaMock.Core.Windows;

public class LocalPlayersWindow : Window
{
    private readonly LocalPlayerEditWindow editWindow;
    private List<MockCharacter> mockCharacters;
    private Task<MockCharacter?>? characterEditTask;

    public LocalPlayersWindow(LocalPlayerEditWindow editWindow)
        : base("Local Characters")
    {
        this.editWindow = editWindow;
        this.mockCharacters = new List<MockCharacter>();
        this.Size = new Vector2(500, 500);
    }

    public override void Draw()
    {
        if (this.characterEditTask is { IsCompleted: true, Result: not null })
        {
            this.mockCharacters.Add(this.characterEditTask.Result);
            this.characterEditTask = null;
        }

        if (ImGui.Button("Add Player"))
        {
            this.characterEditTask = this.editWindow.CreateCharacter();
        }

        using (var table = ImRaii.Table("Local Players", 2))
        {
            if (table)
            {
                ImGui.TableSetupColumn("Name");
                ImGui.TableSetupColumn("Edit", ImGuiTableColumnFlags.NoHeaderLabel);
                ImGui.TableHeadersRow();
                for (var index = 0; index < this.mockCharacters.Count; index++)
                {
                    var mockCharacter = this.mockCharacters[index];
                    ImRaii.PushId(index);
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text(mockCharacter.Name.TextValue);
                    ImGui.TableNextColumn();
                    if (ImGui.Button("Edit"))
                    {
                        this.editWindow.IsOpen = true;
                        this.editWindow.EditCharacter(mockCharacter);
                    }
                }
            }
        }
    }
}
