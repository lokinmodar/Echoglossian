namespace DalaMock.Core.Mocks.DalamudServices;

public class MockDataManager : IDataManager, IMockService
{
    private readonly GameData gameData;
    private readonly ClientLanguage mockLanguage;

    public MockDataManager(GameData gameData, MockDalamudConfiguration configuration)
    {
        this.gameData = gameData;
        this.mockLanguage = configuration.ClientLanguage;
    }

    public Type BackingType { get; }

    public ExcelSheet<T>? GetExcelSheet<T>()
        where T : struct, IExcelRow<T>
    {
        return this.gameData.GetExcelSheet<T>();
    }

    public ExcelSheet<T>? GetExcelSheet<T>(ClientLanguage language)
        where T : struct, IExcelRow<T>
    {
        return this.gameData.GetExcelSheet<T>(ClientLanguageExtensions.ToLumina(language));
    }

    public ExcelSheet<T> GetExcelSheet<T>(ClientLanguage? language = null, string? name = null)
        where T : struct, IExcelRow<T>
    {
        return this.gameData.GetExcelSheet<T>(language?.ToLumina(), name)!;
    }

    public SubrowExcelSheet<T> GetSubrowExcelSheet<T>(ClientLanguage? language = null, string? name = null)
        where T : struct, IExcelSubrow<T>
    {
        return this.gameData.GetSubrowExcelSheet<T>(language?.ToLumina(), name)!;
    }

    public FileResource? GetFile(string path)
    {
        return this.gameData.GetFile(path);
    }

    public T? GetFile<T>(string path)
        where T : FileResource
    {
        var filePath = GameData.ParseFilePath(path);
        if (filePath == null)
        {
            return default;
        }

        return this.GameData.Repositories.TryGetValue(filePath.Repository, out var repository) ? repository.GetFile<T>(filePath.Category, filePath) : default;
    }

    public Task<T> GetFileAsync<T>(string path, CancellationToken cancellationToken)
        where T : FileResource
        =>
        GameData.ParseFilePath(path) is { } filePath &&
            this.GameData.Repositories.TryGetValue(filePath.Repository, out var repository)
                ? Task.Run(
                    () => repository.GetFile<T>(filePath.Category, filePath) ?? throw new FileNotFoundException(
                              "Failed to load file, most likely because the file could not be found."),
                    cancellationToken)
                : Task.FromException<T>(new FileNotFoundException("The file could not be found."));

    public bool FileExists(string path)
    {
        return this.gameData.FileExists(path);
    }

    public ClientLanguage Language => this.mockLanguage;

    public GameData GameData => this.gameData;

    public ExcelModule Excel => this.gameData.Excel;

    public bool HasModifiedGameDataFiles => false;

    public string ServiceName => "Data Manager";
}
