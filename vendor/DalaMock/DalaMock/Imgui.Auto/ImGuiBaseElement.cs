namespace DalaMock.Core.Imgui.Auto;

public abstract class ImGuiBaseElement : IImGuiElement
{
    public virtual string Name { get; set; }

    public virtual string Id { get; set; }

    public virtual string Group { get; set; }

    public virtual MethodInfo? GetMethodInfo { get; set; }

    public virtual MethodInfo? SetMethodInfo { get; set; }

    public abstract void Draw(object? obj);

    public abstract Type GetBackingType();
}
