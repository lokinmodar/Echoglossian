namespace DalaMock.Core.Data.Widgets;

internal sealed class UnlockStateWidget : IDataWindowWidget
{
    private readonly MockUnlockState unlockState;
    private readonly IExcelRowPickerFactory rowPickerFactory;
    private readonly Dictionary<Type, IExcelRowPicker> pickers = new();
    private readonly Dictionary<string, Type?> resolvedTypes = new();

    private FieldInfo[] unlockFields = [];

    public UnlockStateWidget(MockUnlockState unlockState, IExcelRowPickerFactory rowPickerFactory)
    {
        this.unlockState = unlockState;
        this.rowPickerFactory = rowPickerFactory;
    }

    public string[]? CommandShortcuts { get; init; } = ["unlock"];

    public string DisplayName { get; init; } = "Unlock State";

    public bool Ready { get; set; } = true;

    public void Load()
    {
        this.unlockFields =
            typeof(MockUnlockState)
                .GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(f =>
                           f.FieldType == typeof(HashSet<uint>) && (f.Name.EndsWith("Unlocked", StringComparison.Ordinal) || f.Name.EndsWith("Completed", StringComparison.Ordinal))).ToArray();

        this.Ready = true;
    }

    public void Draw()
    {
        if (!this.Ready)
        {
            return;
        }

        foreach (var field in this.unlockFields)
        {
            this.DrawField(field);
        }
    }

    private void DrawField(FieldInfo field)
    {
        var name = field.Name.Replace("Unlocked", string.Empty).Replace("Completed", string.Empty);

        var set = (HashSet<uint>)field.GetValue(this.unlockState)!;

        var sheetType = this.ResolveSheetType(name);

        if (sheetType == null)
        {
            return;
        }

        var picker = this.GetPicker(sheetType);

        this.DrawSection(
            name,
            set,
            picker);
    }

    private void DrawSection(
        string label,
        HashSet<uint> set,
        IExcelRowPicker picker)
    {
        if (!ImGui.CollapsingHeader(label))
        {
            return;
        }


        if (ImGui.Button($"Add##{label}"))
        {
            picker.Open();
        }

        var id = picker.Draw();

        if (id != null)
        {
            set.Add(id.Value);
        }

        ImGui.Separator();

        using (var child = ImRaii.Child($"list##{label}", new Vector2(0, 200), true))
        {
            if (child)
            {
                uint? remove = null;

                foreach (var value in set)
                {
                    var text = picker.Format(value);

                    using (ImRaii.PushId((int)value))
                    {
                        ImGui.Text(text);
                        ImGui.SameLine();
                        if (ImGui.SmallButton("Remove"))
                        {
                            remove = value;
                        }
                    }
                }

                if (remove != null)
                {
                    set.Remove(remove.Value);
                }
            }
        }
    }

    private Type? ResolveSheetType(string name)
    {
        if (!this.resolvedTypes.TryGetValue(name, out var type))
        {
            type =
                AppDomain.CurrentDomain
                         .GetAssemblies()
                         .SelectMany(a => a.GetTypes())
                         .FirstOrDefault(t =>
                                             t.Name == name &&
                                             typeof(IExcelRow<>).IsAssignableFromGeneric(t));
            this.resolvedTypes.Add(name, type);
        }

        return type;
    }

    private IExcelRowPicker GetPicker(Type sheetType)
    {
        if (this.pickers.TryGetValue(sheetType, out var picker))
        {
            return picker;
        }

        picker = this.rowPickerFactory.Create(sheetType);

        this.pickers[sheetType] = picker;

        return picker;
    }
}

static class TypeExt
{
    public static bool IsAssignableFromGeneric(
        this Type generic,
        Type type)
    {
        if (!generic.IsGenericType)
        {
            return generic.IsAssignableFrom(type);
        }

        return type
               .GetInterfaces()
               .Any(i =>
                        i.IsGenericType &&
                        i.GetGenericTypeDefinition() == generic);
    }
}
