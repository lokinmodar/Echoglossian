namespace DalaMock.Core.Plugin;

/// <summary>
/// The main service container that provides plugin loaders, loggers, and UI if required.
/// </summary>
public class MockContainer
{
    private readonly Dictionary<Type, Type>? serviceReplacements;
    private readonly MockDalamudConfiguration dalamudConfiguration;
    private readonly LoggingLevelSwitch levelSwitch;
    private readonly IContainer container;
    private readonly Logger seriLog;
    private readonly MockConfigurationManager configurationManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockContainer"/> class.
    /// Creates a new mock container with the given configuration.
    /// </summary>
    /// <param name="dalamudConfiguration">The configuration to use.</param>
    /// <param name="containerBuildHook">Allows you to alter the services registered by the container.</param>
    /// <param name="serviceReplacements">A dictionary of service replacements or additions. Provide the dalamud interface and the corresponding mock you want to supply. Will either replace the existing mock or add it to the service container. Mocks must implement IMockService</param>
    /// <param name="askPath">If set to true and no game path is found, will ask the user to select the ffxiv sqpack folder.</param>
    public MockContainer(MockDalamudConfiguration? dalamudConfiguration = null, Action<ContainerBuilder>? containerBuildHook = null, Dictionary<Type, Type>? serviceReplacements = null, bool askPath = true)
    {
        this.serviceReplacements = serviceReplacements;
        this.configurationManager = new MockConfigurationManager();
        this.dalamudConfiguration = dalamudConfiguration ?? this.configurationManager.LoadConfiguration();
        this.levelSwitch = new LoggingLevelSwitch
        {
            MinimumLevel = LogEventLevel.Verbose,
        };
        Log.Logger = this.seriLog = new LoggerConfiguration()
                                    .WriteTo.Console(standardErrorFromLevel: LogEventLevel.Verbose)
                                    .MinimumLevel.ControlledBy(this.levelSwitch)
                                    .CreateLogger();

        if (this.dalamudConfiguration.GamePathString is null)
        {
            var exdDataDir = Environment.GetEnvironmentVariable("EXD_DATA_DIR");
            if (exdDataDir is not null)
            {
                this.seriLog.Information("Attempting to use EXD_DATA_DIR environment variable.");
                if (Path.Exists(exdDataDir))
                {
                    this.seriLog.Information("EXD_DATA_DIR environment variable set to " + exdDataDir);
                    this.dalamudConfiguration.GamePathString = exdDataDir;
                }
            }
        }

        if (this.dalamudConfiguration.GamePathString is null)
        {
            var windowsGamePath = @"C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack";

            if (Path.Exists(windowsGamePath))
            {
                this.dalamudConfiguration.GamePathString = windowsGamePath;
            }
        }

        if (this.dalamudConfiguration.GamePathString is null)
        {
            var linuxGamePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (linuxGamePath != string.Empty)
            {
                linuxGamePath = Path.Join(linuxGamePath, ".xlcore", "ffxiv", "game", "sqpack");
                if (Path.Exists(linuxGamePath))
                {
                    this.dalamudConfiguration.GamePathString = linuxGamePath;
                }
            }
        }

        if (!this.dalamudConfiguration.GamePathValid && askPath)
        {
            this.seriLog.Information("Please select your ffxiv sqpack folder.");
            var dialogResult = Dialog.FolderPicker();
            if (dialogResult.IsOk)
            {
                this.dalamudConfiguration.GamePath = new DirectoryInfo(dialogResult.Path);
                this.configurationManager.SaveConfiguration(this.dalamudConfiguration);
            }
            else
            {
                this.seriLog.Error("You must provide your sqpack folder either manually or programmatically.");
                Environment.Exit(69);
            }
        }

        if (!this.dalamudConfiguration.GamePathValid)
        {
            this.seriLog.Error("The provided path " + this.dalamudConfiguration.GamePathString + " is invalid.");
            this.seriLog.Error("You must provide your sqpack folder either manually or programmatically.");
            Environment.Exit(69);
        }
        else
        {
            this.seriLog.Information("The provided path for game data " + this.dalamudConfiguration.GamePathString + " is valid.");
        }

        if (this.dalamudConfiguration.PluginSavePathString is null)
        {
            var dalamockSaveDir = Environment.GetEnvironmentVariable("DALAMOCK_SAVE_DIR");
            if (dalamockSaveDir is not null)
            {
                if (Path.Exists(dalamockSaveDir))
                {
                    this.dalamudConfiguration.PluginSavePathString = dalamockSaveDir;
                }
            }
        }

        if (this.dalamudConfiguration.PluginSavePathString is null)
        {
            var windowsPluginPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            if (windowsPluginPath != string.Empty)
            {
                windowsPluginPath = Path.Join(windowsPluginPath, "XIVLauncher");

                if (Path.Exists(windowsPluginPath))
                {
                    windowsPluginPath = Path.Join(windowsPluginPath, "DalaMock");
                    if (!Path.Exists(windowsPluginPath))
                    {
                        Directory.CreateDirectory(windowsPluginPath);
                    }

                    this.dalamudConfiguration.PluginSavePathString = windowsPluginPath;
                }
            }
        }

        if (this.dalamudConfiguration.PluginSavePathString is null)
        {
            var linuxPluginPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (linuxPluginPath != string.Empty)
            {
                linuxPluginPath = Path.Join(linuxPluginPath, ".xlcore");
                if (Path.Exists(linuxPluginPath))
                {
                    linuxPluginPath = Path.Join(linuxPluginPath, "DalaMock");
                    if (!Path.Exists(linuxPluginPath))
                    {
                        Directory.CreateDirectory(linuxPluginPath);
                    }

                    this.dalamudConfiguration.PluginSavePathString = linuxPluginPath;
                }
            }
        }

        if (!this.dalamudConfiguration.PluginSavePathValid && askPath)
        {
            this.seriLog.Information("Please select a plugin save folder.");
            var dialogResult = Dialog.FolderPicker();
            if (dialogResult.IsOk)
            {
                this.dalamudConfiguration.PluginSavePath = new DirectoryInfo(dialogResult.Path);
                this.configurationManager.SaveConfiguration(this.dalamudConfiguration);
            }
            else
            {
                this.seriLog.Error("You must provide your plugin save folder either manually or programmatically.");
                Environment.Exit(69);
            }
        }

        if (!this.dalamudConfiguration.PluginSavePathValid)
        {
            this.seriLog.Error("The provided path " + this.dalamudConfiguration.PluginSavePathValid + " is invalid.");
            this.seriLog.Error("You must provide your plugin save folder either manually or programmatically.");
            Environment.Exit(69);
        }

        var builder = new ContainerBuilder();
        this.RegisterMockServices(builder);
        containerBuildHook?.Invoke(builder);
        this.container = builder.Build();
    }

    private void ConfigureLogging(ILoggingBuilder log)
    {
        log.AddSerilog(this.seriLog);
        log.AddConsole();
    }

    private void RegisterMockServices(ContainerBuilder builder)
    {
        // Get the current assembly
        var assembly = Assembly.GetExecutingAssembly();

        // Find all types that implement IMockService and are not abstract
        var mockServiceTypes = assembly.GetTypes()
            .Where(t => !t.Name.Contains("Null") && typeof(IMockService).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract);

        var mockWindows = assembly.GetTypes()
            .Where(t => typeof(IMockWindow).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract);

        // ImGui.Auto
        builder.RegisterType<ImGuiElementGenerator>();
        var imGuiElements = assembly.GetTypes()
            .Where(t => typeof(ImGuiBaseElement).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract);

        foreach (var imGuiElement in imGuiElements)
        {
            builder.RegisterType(imGuiElement).AsSelf().As<IImGuiElement>();
        }

        builder.RegisterInstance(Options.Create(new LocalizationOptions { ResourcesPath = "Resources" }))
               .As<IOptions<LocalizationOptions>>()
               .SingleInstance();

        builder.RegisterType<ResourceManagerStringLocalizerFactory>()
               .As<IStringLocalizerFactory>()
               .SingleInstance();

        builder.RegisterGeneric(typeof(StringLocalizer<>))
               .As(typeof(IStringLocalizer<>))
               .SingleInstance();


        builder.Register(handler => LoggerFactory.Create(this.ConfigureLogging))
               .As<ILoggerFactory>()
               .SingleInstance()
               .AutoActivate();

        builder.RegisterGeneric(typeof(Logger<>))
               .As(typeof(ILogger<>))
               .SingleInstance();

        builder.RegisterInstance(this.seriLog).As<Serilog.ILogger>().SingleInstance();

        builder.RegisterInstance(this.levelSwitch);
        builder.RegisterInstance(this.configurationManager);
        builder.RegisterType<MockStyleManager>().AsSelf().SingleInstance();
        builder.Register<GameData>(
            c => new GameData(
                this.dalamudConfiguration.GamePath!.FullName,
                new LuminaOptions
                {
                    PanicOnSheetChecksumMismatch = false,
                })).SingleInstance();
        builder.RegisterInstance(this.dalamudConfiguration);
        builder.RegisterType<MockImGuiComponents>().AsSelf().As<IImGuiComponents>().SingleInstance();
        builder.RegisterType<MockDalamudUi>().SingleInstance();
        builder.RegisterType<MockMainMenuBar>().SingleInstance();
        builder.RegisterType<PluginLoader>().SingleInstance();
        builder.RegisterType<AssertHandler>().SingleInstance();
        builder.RegisterInstance(new MockDalamudVersionInfo(assembly.GetName().Version ?? new Version("Unknown"))).SingleInstance();
        builder.RegisterType<MockFileDialogManager>().As<IFileDialogManager>().SingleInstance();
        builder.RegisterInstance(this).SingleInstance();
        builder.RegisterType<MockWindowSystem>().WithParameter("imNamespace", "DalaMock").AsImplementedInterfaces().AsSelf().InstancePerDependency();
        builder.RegisterType<MockWindowHost>().AsSelf().InstancePerDependency();
        builder.RegisterType<MockWindowHostState>().AsSelf().InstancePerDependency();
        builder.RegisterType<MockDataShare>().AsSelf().SingleInstance();
        builder.RegisterType<SheetRedirectResolver>().AsSelf().SingleInstance();
        builder.RegisterType<NounProcessor>().AsSelf().SingleInstance();
        builder.RegisterType<MockMacroDecoder>().AsSelf().SingleInstance();
        builder.RegisterType<MockSystemFontProvider>().AsSelf().SingleInstance();
        builder.RegisterType<MockFontChooserFactory>().As<IFontChooserFactory>().AsSelf().SingleInstance();
        builder.RegisterType<MockReplacementContainer>().AsSelf().InstancePerDependency();
        builder.RegisterGeneric(typeof(DefaultExcelRowFormatter<>))
               .As(typeof(IExcelRowFormatter<>))
               .SingleInstance();
        builder.RegisterAssemblyTypes(assembly)
               .AsClosedTypesOf(typeof(IExcelRowFormatter<>))
               .SingleInstance();
        builder.RegisterType<ExcelRowFormatterResolver>()
               .As<IExcelRowFormatterResolver>()
               .SingleInstance();
        builder.RegisterGeneric(typeof(ExcelRowPicker<>))
               .AsSelf()
               .InstancePerLifetimeScope();
        builder.RegisterType<ExcelRowPickerFactory>()
               .As<IExcelRowPickerFactory>()
               .SingleInstance();

        if (this.dalamudConfiguration.CreateWindow)
        {
            builder.Register<ImGuiScene>(c =>
            {
                var assertHandler = c.Resolve<AssertHandler>();
                var gameData = c.Resolve<GameData>();
                return ImGuiScene.CreateWindow(assertHandler, gameData, this.dalamudConfiguration.GlobalUiScale);
            }).SingleInstance();

            builder.Register<GraphicsDevice>(c =>
            {
                var context = c.Resolve<ImGuiScene>();
                return context.GraphicsDevice;
            }).SingleInstance();

            builder.Register<CommandList>(c =>
            {
                var context = c.Resolve<ImGuiScene>();
                return context.CommandList;
            }).SingleInstance();

            builder.Register<ISdl2Window>(c =>
            {
                var context = c.Resolve<ImGuiScene>();
                return context.Window;
            }).SingleInstance();

            builder.RegisterSingletonsSelfAndInterfaces<IDataWindowWidget>(assembly);
            builder.RegisterType<MockDataWindow>().AsSelf().As<Window>().SingleInstance();
            builder.RegisterType<MockSettingsWindow>().AsSelf().As<Window>().SingleInstance();
            builder.RegisterType<MockPluginFaultWindow>().AsSelf().As<Window>().SingleInstance();
            builder.RegisterType<MockPluginWindow>().AsSelf().As<Window>().SingleInstance();
            builder.RegisterType<LocalPlayersWindow>().AsSelf().As<Window>().SingleInstance();
            builder.RegisterType<LocalPlayerEditWindow>().AsSelf().As<Window>().SingleInstance();
        }
        else
        {
            builder.RegisterType<NullSdl2Window>().AsSelf().AsImplementedInterfaces();
            var fixedList = mockServiceTypes.ToList();
            fixedList.Remove(typeof(MockTextureProvider));
            fixedList.Add(typeof(NullTextureProvider));
            fixedList.Remove(typeof(MockUiBuilder));
            fixedList.Add(typeof(NullUiBuilder));
            fixedList.Remove(typeof(MockKeyState));
            fixedList.Add(typeof(NullKeyState));
            mockServiceTypes = fixedList;
        }

        var replacedTypes = new Dictionary<Type, Type>();

        // Register each type as implementing IMockService or as the replacement type if specified
        foreach (var type in mockServiceTypes)
        {
            var backingInterface = type.GetInterfaces().FirstOrDefault(c => this.GetType() == typeof(IServiceType));
            if (backingInterface != null && this.serviceReplacements != null && this.serviceReplacements.TryGetValue(backingInterface, out var replacementType))
            {
                replacedTypes.Add(type, replacementType);
                builder.RegisterType(replacementType).AsSelf().As(replacementType.GetInterfaces()).SingleInstance();
            }
            else
            {
                builder.RegisterType(type).AsSelf().As(type.GetInterfaces()).SingleInstance();
            }
        }

        if (this.serviceReplacements != null)
        {
            foreach (var replacement in this.serviceReplacements)
            {
                if (!replacedTypes.ContainsKey(replacement.Key))
                {
                    builder.RegisterType(replacement.Value).AsSelf().As(replacement.Key).As<IMockService>().SingleInstance();
                }
            }
        }

        builder.RegisterType<MockDtrBarEntry>().AsSelf();
        builder.RegisterType<MockReadOnlyDtrEntryBar>().AsSelf();

        // Register each type as implementing IMockService
        foreach (var type in mockWindows)
        {
            builder.RegisterType(type).AsSelf().As<IMockWindow>().SingleInstance();
        }
    }

    /// <summary>
    /// Gets a mock UI, allowing you to render the imgui windows of plugins.
    /// </summary>
    /// <returns>A mock dalamud ui class.</returns>
    public MockDalamudUi GetMockUi()
    {
        return this.container.Resolve<MockDalamudUi>();
    }

    /// <summary>
    /// Returns a plugin loader, allows for multiple plugins to be loaded, then started and stopped.
    /// Each plugin will get it's own DI container.
    /// </summary>
    /// <returns>The plugin loader.</returns>
    public PluginLoader GetPluginLoader()
    {
        return this.container.Resolve<PluginLoader>();
    }

    /// <summary>
    /// Returns a list of all the mocked services that are available.
    /// </summary>
    /// <returns>An enumerable list of all mock services.</returns>
    public IEnumerable<IMockService> GetMockServices()
    {
        return this.container.Resolve<IEnumerable<IMockService>>().ToList();
    }

    /// <summary>
    /// Returns a list of all windows that are considered to be global to DalaMock.
    /// </summary>
    /// <returns>An enumerable list of all windows that are considered to be global to DalaMock.</returns>
    public IEnumerable<Window> GetWindows()
    {
        return this.container.Resolve<IEnumerable<Window>>().ToList();
    }

    /// <summary>
    /// Returns a mock window system.
    /// The mock window system extends the normal window system to get around the issue of not being able to initialize any dalamud services.
    /// </summary>
    /// <returns>A mock window system.</returns>
    public MockWindowSystem GetWindowSystem()
    {
        return this.container.Resolve<MockWindowSystem>();
    }

    /// <summary>
    /// Gets the service container, can be used to request services.
    /// </summary>
    /// <returns>The service container.</returns>
    public IContainer GetContainer()
    {
        return this.container;
    }

    /// <summary>
    /// Gets lumina's GameData object.
    /// </summary>
    /// <returns>A lumina GameData object.</returns>
    public GameData GetGameData()
    {
        return this.container.Resolve<GameData>();
    }
}
