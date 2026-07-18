namespace DalaMock.Core.Pickers;

public interface IExcelRowPickerFactory
{
    ExcelRowPicker Create(Type rowType);
}
