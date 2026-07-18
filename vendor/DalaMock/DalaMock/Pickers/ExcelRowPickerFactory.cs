namespace DalaMock.Core.Pickers;

public sealed class ExcelRowPickerFactory
    : IExcelRowPickerFactory
{
    private readonly ILifetimeScope scope;

    private readonly ConcurrentDictionary<Type, ExcelRowPicker>
        cache = new();

    public ExcelRowPickerFactory(
        ILifetimeScope scope)
    {
        this.scope = scope;
    }

    public ExcelRowPicker Create(Type rowType)
    {
        return this.cache.GetOrAdd(
            rowType,
            this.CreateInternal);
    }

    private ExcelRowPicker CreateInternal(Type rowType)
    {
        var pickerType =
            typeof(ExcelRowPicker<>)
                .MakeGenericType(rowType);

        return (ExcelRowPicker)this.scope.Resolve(pickerType);
    }
}
