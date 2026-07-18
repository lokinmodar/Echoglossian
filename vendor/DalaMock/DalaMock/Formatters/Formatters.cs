namespace DalaMock.Core.Formatters;

public class QuestFormatter : IExcelRowFormatter<Quest>
{
    public string Format(IExcelRow<Quest> row)
    {
        return row is Quest quest ? quest.Name.ToString() : row.RowId.ToString();
    }
}


public class CompanionFormatter : IExcelRowFormatter<Companion>
{
    public string Format(IExcelRow<Companion> row)
    {
        return row is Companion companion ? companion.Singular.ToString() : row.RowId.ToString();
    }
}
