namespace DalaMock.Core.Imgui.Auto;

public class ImGuiUlong : ImGuiBaseElement
{
    public ImGuiUlong()
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
                if (ulong.TryParse(value, out var ulongValue))
                {
                    this.SetMethodInfo?.Invoke(obj, [ulongValue]);
                }
            }
        }
    }

    public override Type GetBackingType()
    {
        return typeof(ulong);
    }

    private string GetConvertedValue(object? obj)
    {
        var value = this.GetMethodInfo?.Invoke(obj, null);
        if (value is ulong convertedValue)
        {
            return convertedValue.ToString();
        }

        return string.Empty;
    }
}
