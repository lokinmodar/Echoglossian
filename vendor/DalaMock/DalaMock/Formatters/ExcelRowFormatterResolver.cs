namespace DalaMock.Core.Formatters;

public sealed class ExcelRowFormatterResolver
    : IExcelRowFormatterResolver
{
    private readonly IComponentContext context;

    private readonly ConcurrentDictionary<Type, object> cache
        = new();

    public ExcelRowFormatterResolver(IComponentContext context)
    {
        this.context = context;
    }

    public string Format<T>(T row)
        where T : struct, IExcelRow<T>
    {
        var formatter =
            (IExcelRowFormatter<T>)this.cache.GetOrAdd(
                typeof(T),
                static (t, self) => self.ResolveFormatter<T>(),
                this);

        return formatter.Format(row);
    }

    private IExcelRowFormatter<T> ResolveFormatter<T>()
        where T : struct, IExcelRow<T>
    {
        return this.context.Resolve<IExcelRowFormatter<T>>();
    }
}
