namespace DalaMock.Core.Mocks.Textures;

public class MockTextureMap : IDalamudTextureWrap
{
    public MockTextureMap(nint handle, int width, int height, ImGuiScene imGuiScene)
    {
        this.ImGuiHandle = handle;
        this.Handle = new ImTextureID(handle);
        this.Width = width;
        this.Height = height;
        this.imGuiScene = imGuiScene;
    }

    public void Dispose()
    {
        this.imGuiScene.CleanupImGuiBinding(this.ImGuiHandle);
    }

    public nint ImGuiHandle { get; }

    public ImTextureID Handle { get; set; }

    public int Width { get; }

    public int Height { get; }

    private ImGuiScene imGuiScene;
}
