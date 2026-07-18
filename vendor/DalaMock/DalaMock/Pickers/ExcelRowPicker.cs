namespace DalaMock.Core.Pickers;

public abstract class ExcelRowPicker : IExcelRowPicker
{
    public abstract bool IsOpen { get; }

    public abstract void Open();

    public abstract uint? Draw();

    public abstract string Format(uint id);

    public abstract string GetPopupId();
}

public sealed class ExcelRowPicker<T> : ExcelRowPicker
    where T : struct, IExcelRow<T>
{
    private readonly IExcelRowFormatterResolver resolver;
    private readonly ExcelSheet<T> sheet;

    public delegate ExcelRowPicker<T> Factory(T type);

    public ExcelRowPicker(
        GameData gameData, IExcelRowFormatterResolver resolver)
    {
        this.resolver = resolver;
        this.sheet = gameData.GetExcelSheet<T>()!;
    }

    public override string Format(uint id)
    {
        var row = this.sheet.GetRowOrDefault(id);

        if (row == null)
        {
            return id.ToString();
        }

        return this.resolver.Format<T>(row.Value);
    }

    private bool open;
    private string filter = string.Empty;
    private uint? result;

    public override bool IsOpen => this.open;

    public override void Open()
    {
        this.open = true;
        this.result = null;
        this.filter = string.Empty;

        ImGui.OpenPopup(this.GetPopupId());
    }


    public override uint? Draw()
    {
        if (!this.open)
        {
            return null;
        }

        uint? selected = null;

        if (ImGui.BeginPopupModal(this.GetPopupId(), ref this.open))
        {
            ImGui.InputText("Filter", ref this.filter, 100);

            ImGui.Separator();

            ImGui.BeginChild("rows", new System.Numerics.Vector2(400, 300), true);

            foreach (var row in this.sheet)
            {
                var text = this.resolver.Format(row);

                if (!string.IsNullOrEmpty(this.filter) &&
                    !text.Contains(this.filter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (ImGui.Selectable($"{text}##{row.RowId}"))
                {
                    selected = row.RowId;
                    this.open = false;
                    ImGui.CloseCurrentPopup();
                }
            }

            ImGui.EndChild();

            if (ImGui.Button("Cancel"))
            {
                this.open = false;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }

        if (selected.HasValue)
        {
            this.result = selected;
        }

        return this.result;
    }

    public T? GetRow(uint id)
    {
        var row = this.sheet.GetRowOrDefault(id);
        return row;
    }

    public override string GetPopupId()
        => $"ExcelRowPicker<{typeof(T).Name}>";
}
