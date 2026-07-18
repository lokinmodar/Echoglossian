namespace DalaMock.Core.Imgui;

public struct ResourceSetInfo
{
    public readonly IntPtr ImGuiBinding;
    public readonly ResourceSet ResourceSet;

    public ResourceSetInfo(IntPtr imGuiBinding, ResourceSet resourceSet)
    {
        this.ImGuiBinding = imGuiBinding;
        this.ResourceSet = resourceSet;
    }
}
