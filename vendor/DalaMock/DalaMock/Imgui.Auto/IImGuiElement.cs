namespace DalaMock.Core.Imgui.Auto;

public interface IImGuiElement
{
    string Name { get; set; }

    string Id { get; set; }

    MethodInfo? GetMethodInfo { get; set; }

    MethodInfo? SetMethodInfo { get; set; }

    string Group { get; set; }

    void Draw(object? obj);

    Type GetBackingType();
}
