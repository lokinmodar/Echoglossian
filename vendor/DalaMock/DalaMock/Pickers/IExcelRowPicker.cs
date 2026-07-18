namespace DalaMock.Core.Pickers;

public interface IExcelRowPicker
{
    bool IsOpen { get; }

    void Open();

    uint? Draw();

    string Format(uint id);
}
