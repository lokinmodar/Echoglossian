namespace DalaMock.Core.Imgui;

/// <summary>
/// Provides a interface between a pointer to an existing resource and dalamud.
/// </summary>
/// <param name="handle">A pointer to the existing resource.</param>
/// <param name="width">Width of the texture.</param>
/// <param name="height">Height of the texture.</param>
public class RawTextureMap(nint handle, int width, int height) : IDalamudTextureWrap
{
    public void Dispose()
    {
    }

    public nint ImGuiHandle { get; } = handle;

    public ImTextureID Handle { get; set; } = new ImTextureID(handle);

    public int Width { get; } = width;

    public int Height { get; } = height;
}
