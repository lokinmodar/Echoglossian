namespace DalaMock.Core.Formatters;

public sealed class DefaultExcelRowFormatter<T> : IExcelRowFormatter<T>
    where T : struct, IExcelRow<T>
{
    public string Format(IExcelRow<T> row)
        => row.RowId.ToString();
}
