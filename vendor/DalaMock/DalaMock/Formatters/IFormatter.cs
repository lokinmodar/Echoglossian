namespace DalaMock.Core.Formatters;

public interface IExcelRowFormatter<in T>
    where T : struct, IExcelRow<T>
{
    string Format(IExcelRow<T> row);
}
