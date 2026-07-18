namespace DalaMock.Core.Imgui.Auto;

public class ImGuiString : ImGuiBaseElement
{
    public ImGuiString()
    {
    }

    public override void Draw(object? obj)
    {
        if (this.GetMethodInfo == null || this.SetMethodInfo == null)
        {
            return;
        }

        var textValue = this.GetStringValue(obj);

        if (ImGui.InputText($"{this.Name}##{this.Id}", ref textValue, 999))
        {
            if (textValue != this.GetStringValue(obj))
            {
                this.SetMethodInfo?.Invoke(obj, [textValue]);
            }
        }
    }

    public override Type GetBackingType()
    {
        return typeof(string);
    }

    private string GetStringValue(object? obj)
    {
        var value = this.GetMethodInfo?.Invoke(obj, null);
        string textValue = string.Empty;
        if (value is string stringValue)
        {
            textValue = stringValue;
        }

        return textValue;
    }
}
