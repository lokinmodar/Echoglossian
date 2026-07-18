namespace DalaMock.Core.Imgui.Auto;

public class ImGuiUint : ImGuiBaseElement
{
    public ImGuiUint()
    {
    }

    public override void Draw(object? obj)
    {
        if (this.GetMethodInfo == null || this.SetMethodInfo == null)
        {
            return;
        }

        var value = this.GetConvertedValue(obj);

        if (ImGui.InputText($"{this.Name}##{this.Id}", ref value, 999))
        {
            if (value != this.GetConvertedValue(obj))
            {
                if (uint.TryParse(value, out var uintValue))
                {
                    this.SetMethodInfo?.Invoke(obj, [uintValue]);
                }
            }
        }
    }

    public override Type GetBackingType()
    {
        return typeof(uint);
    }

    private string GetConvertedValue(object? obj)
    {
        var value = this.GetMethodInfo?.Invoke(obj, null);
        if (value is uint convertedValue)
        {
            return convertedValue.ToString();
        }

        return string.Empty;
    }
}
