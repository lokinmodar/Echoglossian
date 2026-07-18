namespace DalaMock.Core.Configuration;

public class ConfigurationManager
{
    private const string ConfigFileName = "DalaMockConfig.json";
    private readonly IConfigurationRoot configurationRoot;

    public ConfigurationManager()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(ConfigFileName, optional: true, reloadOnChange: true);

        this.configurationRoot = builder.Build();
    }

    public MockDalamudConfiguration LoadConfiguration()
    {
        var config = new MockDalamudConfiguration();
        this.configurationRoot.Bind(config);
        return config;
    }

    public void SaveConfiguration(MockDalamudConfiguration config)
    {
        var json = JsonConvert.SerializeObject(config, Formatting.Indented);
        File.WriteAllText(ConfigFileName, json);
    }
}
