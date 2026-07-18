namespace DalaMock.Core.Imgui.Auto;

public class ImGuiSeString : ImGuiBaseElement
{
    public ImGuiSeString()
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
                var newSeString = new SeStringBuilder().AddText(textValue).BuiltString;
                this.SetMethodInfo?.Invoke(obj, [newSeString]);
            }
        }
    }

    /// <inheritdoc/>
    public override Type GetBackingType()
    {
        return typeof(SeString);
    }

    private string GetStringValue(object? obj)
    {
        var value = this.GetMethodInfo?.Invoke(obj, null);
        string textValue = string.Empty;
        if (value is SeString stringValue)
        {
            textValue = stringValue.TextValue;
        }

        return textValue;
    }
}
