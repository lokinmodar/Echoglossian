namespace DalaMock.Core.Fonts.Atlas;

/// <summary>
/// Marshals work onto the ImGui render thread.
/// Drained by <see cref="ImGuiScene.Update"/> before each <see cref="ImGui.NewFrame"/>.
/// </summary>
internal sealed class MockFontAtlasUploadQueue
{
    private readonly ConcurrentQueue<SysAction> pending = new();

    public void Enqueue(SysAction action) => this.pending.Enqueue(action);

    public void Drain()
    {
        while (this.pending.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MockFontAtlasUploadQueue] queued action threw");
            }
        }
    }
}
