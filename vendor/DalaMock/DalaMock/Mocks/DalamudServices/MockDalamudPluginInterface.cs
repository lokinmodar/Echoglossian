namespace DalaMock.Core.Mocks.DalamudServices;

public class MockDalamudPluginInterface : IDalamudPluginInterface, IDisposable
{
    private readonly IUiBuilder uiBuilder;
    private readonly IComponentContext componentContext;
    private readonly MockDalamudVersionInfo mockDalamudVersionInfo;
    private readonly MockDataShare dataShare;
    private readonly ILogger<MockDalamudPluginInterface> logger;

    // private readonly MockProgram _mockProgram;
    private readonly IPluginManifest pluginManifest;
    private PluginConfiguration pluginConfiguration;
    private PluginLoadReason pluginLoadReason;
    private readonly Guid pluginGuid;

    public MockDalamudPluginInterface(
        IUiBuilder uiBuilder,
        PluginLoadSettings pluginLoadSettings,
        IPluginManifest pluginManifest,
        IComponentContext componentContext,
        MockDalamudVersionInfo mockDalamudVersionInfo,
        MockDataShare dataShare,
        ILogger<MockDalamudPluginInterface> logger)
    {
        // _mockProgram = mockProgram;
        this.uiBuilder = uiBuilder;
        this.componentContext = componentContext;
        this.mockDalamudVersionInfo = mockDalamudVersionInfo;
        this.dataShare = dataShare;
        this.logger = logger;
        this.ConfigFile = pluginLoadSettings.ConfigFile;
        this.ConfigDirectory = pluginLoadSettings.ConfigDir;
        this.pluginLoadReason = pluginLoadSettings.PluginLoadReason;
        this.AssemblyLocation = new FileInfo((pluginLoadSettings.AssemblyLocation ?? Environment.ProcessPath)!);
        this.pluginConfiguration = new PluginConfiguration(pluginLoadSettings.ConfigDir.FullName);
        this.pluginManifest = pluginManifest;
        //TODO: Move this GUID into a LocalPluginManifest record
        this.pluginGuid = Guid.NewGuid();
    }

    /// <inheritdoc/>
    public event IDalamudPluginInterface.LanguageChangedDelegate? LanguageChanged;

    /// <inheritdoc/>
    public event IDalamudPluginInterface.ActivePluginsChangedDelegate? ActivePluginsChanged;

    /// <inheritdoc/>
    public Task InjectAsync(object instance, params object[] scopedObjects)
    {
        throw new NotImplementedException();
    }

    public Task<PluginUpdate?> CheckForUpdateAsync()
    {
        return Task.FromResult((PluginUpdate?)null);
    }

    /// <inheritdoc/>
    public PluginLoadReason Reason => this.pluginLoadReason;

    /// <inheritdoc/>
    public bool IsAutoUpdateComplete => true;

    /// <inheritdoc/>
    public string SourceRepository => "DevPlugin";

    /// <inheritdoc/>
    public string InternalName => this.pluginManifest.InternalName;

    /// <inheritdoc/>
    public IPluginManifest Manifest => this.pluginManifest;

    /// <inheritdoc/>
    public bool IsDev => true;

    /// <inheritdoc/>
    public bool IsTesting => false;

    public bool IsInProfile => false;

    /// <inheritdoc/>
    public DateTime LoadTime => DateTime.Now;

    /// <inheritdoc/>
    public DateTime LoadTimeUTC => DateTime.Now;

    /// <inheritdoc/>
    public TimeSpan LoadTimeDelta => TimeSpan.Zero;

    /// <inheritdoc/>
    public DirectoryInfo DalamudAssetDirectory => new(string.Empty);

    /// <inheritdoc/>
    public FileInfo ConfigFile { get; }

    /// <inheritdoc/>
    public IUiBuilder UiBuilder => this.uiBuilder;

    /// <inheritdoc/>
    public bool IsDevMenuOpen => true;

    /// <inheritdoc/>
    public bool IsDebugging => true;

    public bool AllowSeasonalEvents => true;

    /// <inheritdoc/>
    public string UiLanguage => "English";

    /// <inheritdoc/>
    public ISanitizer Sanitizer => new Sanitizer(ClientLanguage.English);

    /// <inheritdoc/>
    public XivChatType GeneralChatType => XivChatType.Debug;

    /// <inheritdoc/>
    public IEnumerable<IExposedPlugin> InstalledPlugins => new List<IExposedPlugin>();

    /// <inheritdoc/>
    public bool OpenPluginInstallerTo(
        PluginInstallerOpenKind openTo = PluginInstallerOpenKind.AllPlugins,
        string? searchText = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public bool OpenDalamudSettingsTo(SettingsOpenKind openTo = SettingsOpenKind.General, string? searchText = null)
    {
        return false;
    }

    /// <inheritdoc/>
    public bool OpenDeveloperMenu()
    {
        return false;
    }

    public IExposedPlugin? GetPlugin(Assembly assembly)
    {
        return null;
    }

    public IExposedPlugin? GetPlugin(AssemblyLoadContext context)
    {
        return null;
    }

    public IDalamudVersionInfo GetDalamudVersion()
    {
        return this.mockDalamudVersionInfo;
    }

    /// <inheritdoc/>
    public T GetOrCreateData<T>(string tag, Func<T> dataGenerator)
        where T : class
    {
        return this.dataShare.GetOrCreateData(tag, new DataCachePluginId(this.InternalName, this.pluginGuid), dataGenerator);
    }

    /// <inheritdoc/>
    public void RelinquishData(string tag)
    {

        this.dataShare.RelinquishData(tag, new DataCachePluginId(this.InternalName, this.pluginGuid));
    }

    /// <inheritdoc/>
    public bool TryGetData<T>(string tag, out T? data)
        where T : class
    {
        return this.dataShare.TryGetData(tag, new DataCachePluginId(this.InternalName, this.pluginGuid), out data);
    }

    /// <inheritdoc/>
    public T? GetData<T>(string tag)
        where T : class
    {
        return this.dataShare.GetData<T>(tag, new DataCachePluginId(this.InternalName, this.pluginGuid));
    }

    /// <inheritdoc/>
    public DirectoryInfo ConfigDirectory { get; }

    /// <inheritdoc/>
    public FileInfo AssemblyLocation { get; }

    /// <inheritdoc/>
    public ICallGateProvider<TRet> GetIpcProvider<TRet>(string name)
    {
        return new MockCallGateProvider<TRet>(name);
    }

    /// <inheritdoc/>
    public ICallGateProvider<T1, TRet> GetIpcProvider<T1, TRet>(string name)
    {
        return new MockCallGateProvider<T1, TRet>(name);
    }

    /// <inheritdoc/>
    public ICallGateProvider<T1, T2, TRet> GetIpcProvider<T1, T2, TRet>(string name)
    {
        return new MockCallGateProvider<T1, T2, TRet>(name);
    }

    /// <inheritdoc/>
    public ICallGateProvider<T1, T2, T3, TRet> GetIpcProvider<T1, T2, T3, TRet>(string name)
    {
        return new MockCallGateProvider<T1, T2, T3, TRet>(name);
    }

    /// <inheritdoc/>
    public ICallGateProvider<T1, T2, T3, T4, TRet> GetIpcProvider<T1, T2, T3, T4, TRet>(string name)
    {
        return new MockCallGateProvider<T1, T2, T3, T4, TRet>(name);
    }

    /// <inheritdoc/>
    public ICallGateProvider<T1, T2, T3, T4, T5, TRet> GetIpcProvider<T1, T2, T3, T4, T5, TRet>(string name)
    {
        return new MockCallGateProvider<T1, T2, T3, T4, T5, TRet>(name);
    }

    /// <inheritdoc/>
    public ICallGateProvider<T1, T2, T3, T4, T5, T6, TRet> GetIpcProvider<T1, T2, T3, T4, T5, T6, TRet>(string name)
    {
        return new MockCallGateProvider<T1, T2, T3, T4, T5, T6, TRet>(name);
    }

    /// <inheritdoc/>
    public ICallGateProvider<T1, T2, T3, T4, T5, T6, T7, TRet> GetIpcProvider<T1, T2, T3, T4, T5, T6, T7, TRet>(
        string name)
    {
        return new MockCallGateProvider<T1, T2, T3, T4, T5, T6, T7, TRet>(name);
    }

    /// <inheritdoc/>
    public ICallGateProvider<T1, T2, T3, T4, T5, T6, T7, T8, TRet> GetIpcProvider<T1, T2, T3, T4, T5, T6, T7, T8, TRet>(
        string name)
    {
        return new MockCallGateProvider<T1, T2, T3, T4, T5, T6, T7, T8, TRet>(name);
    }

    /// <inheritdoc/>
    public ICallGateSubscriber<TRet> GetIpcSubscriber<TRet>(string name)
    {
        return null!;
    }

    /// <inheritdoc/>
    public ICallGateSubscriber<T1, TRet> GetIpcSubscriber<T1, TRet>(string name)
    {
        return null!;
    }

    /// <inheritdoc/>
    public ICallGateSubscriber<T1, T2, TRet> GetIpcSubscriber<T1, T2, TRet>(string name)
    {
        return null!;
    }

    /// <inheritdoc/>
    public ICallGateSubscriber<T1, T2, T3, TRet> GetIpcSubscriber<T1, T2, T3, TRet>(string name)
    {
        return null!;
    }

    /// <inheritdoc/>
    public ICallGateSubscriber<T1, T2, T3, T4, TRet> GetIpcSubscriber<T1, T2, T3, T4, TRet>(string name)
    {
        return null!;
    }

    /// <inheritdoc/>
    public ICallGateSubscriber<T1, T2, T3, T4, T5, TRet> GetIpcSubscriber<T1, T2, T3, T4, T5, TRet>(string name)
    {
        return null!;
    }

    /// <inheritdoc/>
    public ICallGateSubscriber<T1, T2, T3, T4, T5, T6, TRet> GetIpcSubscriber<T1, T2, T3, T4, T5, T6, TRet>(string name)
    {
        return null!;
    }

    /// <inheritdoc/>
    public ICallGateSubscriber<T1, T2, T3, T4, T5, T6, T7, TRet> GetIpcSubscriber<T1, T2, T3, T4, T5, T6, T7, TRet>(
        string name)
    {
        return null!;
    }

    /// <inheritdoc/>
    public ICallGateSubscriber<T1, T2, T3, T4, T5, T6, T7, T8, TRet> GetIpcSubscriber<T1, T2, T3, T4, T5, T6, T7, T8,
        TRet>(string name)
    {
        return null!;
    }

    /// <inheritdoc/>
    public void SavePluginConfig(IPluginConfiguration? currentConfig)
    {
        var serializedConfig = SerializeConfig(currentConfig);
        File.WriteAllText(this.ConfigFile.FullName, serializedConfig);
    }

    /// <inheritdoc/>
    public IPluginConfiguration? GetPluginConfig()
    {
        foreach (var type in Assembly.GetCallingAssembly().GetTypes())
        {
            if (type.IsAssignableTo(typeof(IPluginConfiguration)))
            {
                var mi = this.pluginConfiguration.GetType().GetMethod("LoadForType");
                var fn = mi!.MakeGenericMethod(type);
                return (IPluginConfiguration)fn.Invoke(this.pluginConfiguration, new object[] { this.InternalName });
            }
        }

        return null!;
    }

    /// <inheritdoc/>
    public string GetPluginConfigDirectory()
    {
        var pluginConfigDirectory = Path.Combine(this.ConfigDirectory.FullName, this.InternalName);
        if (!Path.Exists(pluginConfigDirectory))
        {
            Directory.CreateDirectory(pluginConfigDirectory);
        }

        return pluginConfigDirectory;
    }

    /// <inheritdoc/>
    public string GetPluginLocDirectory()
    {
        return Path.Combine(Path.Combine(this.ConfigDirectory.FullName, this.InternalName), "loc");
    }

    /// <inheritdoc/>
    public DalamudLinkPayload AddChatLinkHandler(uint commandId, Action<uint, SeString> commandAction)
    {
        return null!;
    }

    /// <inheritdoc/>
    public void RemoveChatLinkHandler(uint commandId)
    {
    }

    /// <inheritdoc/>
    public void RemoveChatLinkHandler()
    {
    }

    /// <inheritdoc/>
    public T? Create<T>(params object[] scopedObjects)
        where T : class
    {
        var mockServices = this.componentContext.Resolve<IEnumerable<IMockService>>();
        Dictionary<Type, object> mockServicesDict = new();
        foreach (var mockService in mockServices)
        {
            foreach (var interfaceType in mockService.GetType().GetInterfaces())
            {
                mockServicesDict.TryAdd(interfaceType, mockService);
            }
        }

        foreach (var scopedObject in scopedObjects)
        {
            foreach (var interfaceType in scopedObject.GetType().GetInterfaces())
            {
                mockServicesDict.TryAdd(interfaceType, scopedObject);
            }
        }

        // Get the type of the class to instantiate
        var typeToCreate = typeof(T);

        // Get all constructors of the class
        var constructors = typeToCreate.GetConstructors();
        T? newInstance = null;

        // Try to find a suitable constructor
        foreach (var constructor in constructors)
        {
            var parameters = constructor.GetParameters();
            var parameterValues = new List<object>();

            foreach (var parameter in parameters)
            {
                // Try to resolve each parameter from the mock services
                if (mockServicesDict.TryGetValue(parameter.ParameterType, out var mockService))
                {
                    parameterValues.Add(mockService);
                }
                else
                {
                    parameterValues = null;
                    break;
                }
            }

            // If all parameters are resolved, create an instance and return it
            if (parameterValues != null)
            {
                newInstance = (T)constructor.Invoke(parameterValues.ToArray());
            }
        }

        // If no suitable constructor is found, try to use a parameterless constructor
        var parameterlessConstructor = constructors.FirstOrDefault(c => c.GetParameters().Length == 0);
        if (parameterlessConstructor != null)
        {
            newInstance = (T)parameterlessConstructor.Invoke(null);
        }

        if (newInstance == null)
        {
            // If no suitable constructor is found, return null
            this.logger.LogError($"No suitable constructor found for type '{typeToCreate.FullName}' that can be resolved with the provided mock services.");
            return null;
        }

        var properties = newInstance.GetType().GetProperties()
            .Where(prop => prop.PropertyType.IsPublic && Attribute.IsDefined(prop, typeof(PluginServiceAttribute)) && prop.CanWrite && prop.CanRead);

        foreach (var property in properties)
        {
            var propertyType = property.PropertyType;

            if (mockServicesDict.TryGetValue(propertyType, out var mockService))
            {
                property.SetValue(newInstance, mockService);
            }
            else
            {
                this.logger.LogError($"No matching mock service found for property '{property.Name}' of type '{propertyType.FullName}' in object of type '{newInstance.GetType().FullName}'.");
            }
        }

        return newInstance;
    }

    /// <inheritdoc/>
    public Task<T> CreateAsync<T>(params object[] scopedObjects)
        where T : class
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public bool Inject(object instance, params object[] scopedObjects)
    {
        var mockServices = this.componentContext.Resolve<IEnumerable<IMockService>>();
        Dictionary<Type, object> mockServicesDict = new();
        foreach (var mockService in mockServices)
        {
            foreach (var interfaceType in mockService.GetType().GetInterfaces())
            {
                mockServicesDict.TryAdd(interfaceType, mockService);
            }
        }

        foreach (var scopedObject in scopedObjects)
        {
            foreach (var interfaceType in scopedObject.GetType().GetInterfaces())
            {
                mockServicesDict.TryAdd(interfaceType, scopedObject);
            }
        }

        var properties = instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(prop => Attribute.IsDefined(prop, typeof(PluginServiceAttribute)) && prop.CanWrite && prop.CanRead);

        foreach (var property in properties)
        {
            var propertyType = property.PropertyType;

            if (mockServicesDict.TryGetValue(propertyType, out var mockService))
            {
                property.SetValue(instance, mockService);
            }
            else
            {
                this.logger.LogError($"No matching mock service found for property '{property.Name}' of type '{propertyType.FullName}' in object of type '{instance.GetType().FullName}'.");
            }
        }

        return true;
    }

    internal static string SerializeConfig(object? config)
    {
        return JsonConvert.SerializeObject(
            config,
            Formatting.Indented,
            new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects,
            });
    }

    public bool OpenPluginInstaller()
    {
        return false;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }

    public virtual void OnLanguageChanged(string langcode)
    {
        this.LanguageChanged?.Invoke(langcode);
    }

    public virtual void OnActivePluginsChanged(PluginListInvalidationKind kind, bool affectedthisplugin)
    {
        this.ActivePluginsChanged?.Invoke(new MockActivePluginsChangedEventArgs(kind, []));
    }

    public object? GetService(Type serviceType)
    {
        throw new NotImplementedException();
    }
}
