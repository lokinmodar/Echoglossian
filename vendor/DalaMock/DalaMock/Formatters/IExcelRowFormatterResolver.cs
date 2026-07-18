namespace DalaMock.Core.Formatters;

public interface IExcelRowFormatterResolver
{
    string Format<T>(T row)
        where T : struct, IExcelRow<T>;
}
