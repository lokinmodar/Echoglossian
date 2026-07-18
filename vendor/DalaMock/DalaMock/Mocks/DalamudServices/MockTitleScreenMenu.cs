namespace DalaMock.Core.Mocks.DalamudServices;

public class MockTitleScreenMenu : ITitleScreenMenu, IMockService
{
    public string ServiceName => "Title Screen Menu";

    public void RemoveEntry(IReadOnlyTitleScreenMenuEntry entry)
    {
    }

    public IReadOnlyList<IReadOnlyTitleScreenMenuEntry> Entries { get; } = null!;

    public IReadOnlyTitleScreenMenuEntry AddEntry(string text, ISharedImmediateTexture texture, SysAction onTriggered)
    {
        return null!;
    }

    public IReadOnlyTitleScreenMenuEntry AddEntry(
        ulong priority,
        string text,
        ISharedImmediateTexture texture,
        SysAction onTriggered)
    {
        return null!;
    }
}
