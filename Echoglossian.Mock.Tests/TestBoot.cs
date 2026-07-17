// <copyright file="TestBoot.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Autofac;
using DalaMock.Core.Configuration;
using DalaMock.Core.Plugin;
using Echoglossian.Mock.Hosting;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Events;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.ImGuiNotification.EventArgs;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Echoglossian.Mock.Tests;

/// <summary>
/// Builds a headless DalaMock container and starts the real Echoglossian plugin inside it.
/// </summary>
internal sealed class TestBoot
{
    private readonly Func<DirectoryInfo> runStateRootFactory;
    private readonly Func<Func<Task>, Task> startPluginRunner;
    private readonly Func<MockContainer, global::Echoglossian.Echoglossian, DirectoryInfo, DirectoryInfo, FileInfo, Task> postStartValidation;
    private readonly bool useHostedSessionFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestBoot"/> class.
    /// </summary>
    public TestBoot()
        : this(null, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestBoot"/> class with
    /// deterministic test seams for startup-failure coverage.
    /// </summary>
    /// <param name="runStateRootFactory">Creates the isolated local state root for the current run.</param>
    /// <param name="startPluginRunner">Runs or overrides the plugin-startup action.</param>
    /// <param name="postStartValidation">Runs additional validation before returning the started plugin rail.</param>
    internal TestBoot(
        Func<DirectoryInfo>? runStateRootFactory,
        Func<Func<Task>, Task>? startPluginRunner,
        Func<MockContainer, global::Echoglossian.Echoglossian, DirectoryInfo, DirectoryInfo, FileInfo, Task>? postStartValidation)
    {
        this.useHostedSessionFactory = runStateRootFactory is null &&
            startPluginRunner is null &&
            postStartValidation is null;
        this.runStateRootFactory = runStateRootFactory ?? this.CreateRunStateRoot;
        this.startPluginRunner = startPluginRunner ?? RunStartPluginAsync;
        this.postStartValidation = postStartValidation ?? NoOpPostStartValidationAsync;
    }

    /// <summary>
    /// Starts the real plugin under a headless DalaMock container.
    /// </summary>
    /// <returns>The started plugin and its owning mock container.</returns>
    public async Task<StartedPlugin> StartPluginAsync()
    {
        var stateRoot = this.runStateRootFactory();
        var pluginSavePath = this.CreateLocalPluginSavePath(stateRoot);
        var configPath = new FileInfo(Path.Combine(stateRoot.FullName, "test.json"));
        MockContainer? mockContainer = null;
        global::Echoglossian.Echoglossian? plugin = null;

        try
        {
            if (this.useHostedSessionFactory)
            {
                var session = await HostedPreviewPluginSessionFactory.StartAsync(
                    new HostedPreviewPluginOptions(
                        stateRoot,
                        pluginSavePath,
                        configPath,
                        DatabasePath: null,
                        CreateWindow: false));
                mockContainer = session.Container;
                plugin = session.Plugin;
                await this.postStartValidation(mockContainer, plugin, stateRoot, pluginSavePath, configPath);
                return new StartedPlugin(mockContainer, plugin, stateRoot, pluginSavePath, configPath);
            }

            mockContainer = new MockContainer(
                new MockDalamudConfiguration
                {
                    CreateWindow = false,
                    GamePath = this.ResolveSqpackDirectory(),
                    PluginSavePath = pluginSavePath,
                },
                builder => { },
                [],
                false);

            var pluginLoader = mockContainer.GetPluginLoader();
            var mockPlugin = pluginLoader.AddPlugin(typeof(EchoglossianAsyncPluginAdapter));
            var pluginLoadSettings = new PluginLoadSettings(
                stateRoot,
                configPath)
            {
                AssemblyLocation = typeof(global::Echoglossian.Echoglossian).Assembly.Location,
            };

            await this.startPluginRunner(() => pluginLoader.StartPlugin(mockPlugin, pluginLoadSettings));

            if (mockPlugin.DalamudPlugin is not EchoglossianAsyncPluginAdapter adapter ||
                adapter.Plugin is null)
            {
                throw new InvalidOperationException("DalaMock did not build Echoglossian.");
            }

            plugin = adapter.Plugin;
            await this.postStartValidation(mockContainer, plugin, stateRoot, pluginSavePath, configPath);
            return new StartedPlugin(mockContainer, plugin, stateRoot, pluginSavePath, configPath);
        }
        catch
        {
            this.TryCleanupFailedStartup(mockContainer, plugin, stateRoot);
            throw;
        }
    }

    /// <summary>
    /// Creates an isolated local state root for a single DalaMock startup run.
    /// </summary>
    /// <returns>The created local state root.</returns>
    private DirectoryInfo CreateRunStateRoot()
    {
        var pluginSavePath = new DirectoryInfo(
            Path.Combine(
                Path.GetTempPath(),
                "Echoglossian.Mock.Tests",
                Guid.NewGuid().ToString("N")));
        pluginSavePath.Create();
        return pluginSavePath;
    }

    /// <summary>
    /// Creates the plugin-save directory used by the DalaMock startup rail.
    /// </summary>
    /// <param name="stateRoot">The isolated local state root for the current run.</param>
    /// <returns>The created plugin-save directory.</returns>
    private DirectoryInfo CreateLocalPluginSavePath(DirectoryInfo stateRoot)
    {
        var pluginSavePath = new DirectoryInfo(Path.Combine(stateRoot.FullName, ".dalamock"));
        pluginSavePath.Create();
        return pluginSavePath;
    }

    /// <summary>
    /// Resolves the local FFXIV sqpack directory required by DalaMock.
    /// </summary>
    /// <returns>The local sqpack directory.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no valid local sqpack directory can be found.</exception>
    private DirectoryInfo ResolveSqpackDirectory()
    {
        foreach (var candidate in this.GetSqpackPathCandidates())
        {
            if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
            {
                return new DirectoryInfo(candidate);
            }
        }

        throw new InvalidOperationException("Unable to resolve a local FFXIV sqpack directory for DalaMock.");
    }

    /// <summary>
    /// Gets the local sqpack path candidates that should be checked for DalaMock.
    /// </summary>
    /// <returns>The sqpack path candidates.</returns>
    private IEnumerable<string?> GetSqpackPathCandidates()
    {
        yield return Environment.GetEnvironmentVariable("EXD_DATA_DIR");

        var launcherGamePath = this.TryReadLauncherGamePath();
        if (!string.IsNullOrWhiteSpace(launcherGamePath))
        {
            yield return Path.Combine(launcherGamePath, "game", "sqpack");
        }

        yield return @"C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack";
        yield return @"C:\Program Files\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack";
        yield return @"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY XIV Online\game\sqpack";
    }

    /// <summary>
    /// Reads the local XIVLauncher game path when it is available.
    /// </summary>
    /// <returns>The configured XIVLauncher game path, or <see langword="null"/> when it is unavailable.</returns>
    private string? TryReadLauncherGamePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var launcherConfigPath = Path.Combine(appDataPath, "XIVLauncher", "launcherConfigV3.json");
        return TryReadLauncherGamePath(launcherConfigPath);
    }

    /// <summary>
    /// Reads the local XIVLauncher game path from a specific config file path
    /// when it is available and readable.
    /// </summary>
    /// <param name="launcherConfigPath">The launcher config file path to inspect.</param>
    /// <returns>The configured XIVLauncher game path, or <see langword="null"/> when it is unavailable.</returns>
    internal static string? TryReadLauncherGamePath(string launcherConfigPath)
    {
        if (!File.Exists(launcherConfigPath))
        {
            return null;
        }

        try
        {
            using var launcherConfigStream = File.OpenRead(launcherConfigPath);
            using var launcherConfigDocument = JsonDocument.Parse(launcherConfigStream);
            if (!launcherConfigDocument.RootElement.TryGetProperty("GamePath", out var gamePathElement))
            {
                return null;
            }

            return gamePathElement.GetString();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Runs the default plugin-startup action.
    /// </summary>
    /// <param name="startPlugin">The plugin-startup action to run.</param>
    /// <returns>A task that completes once startup finishes.</returns>
    private static Task RunStartPluginAsync(Func<Task> startPlugin)
    {
        return startPlugin();
    }

    /// <summary>
    /// Performs no additional post-start validation.
    /// </summary>
    /// <param name="container">The owning mock container.</param>
    /// <param name="plugin">The started production plugin.</param>
    /// <param name="stateRoot">The isolated local state root for the run.</param>
    /// <param name="pluginSavePath">The rail-owned plugin save path for the run.</param>
    /// <param name="configPath">The rail-owned config path for the run.</param>
    /// <returns>A completed task.</returns>
    private static Task NoOpPostStartValidationAsync(
        MockContainer container,
        global::Echoglossian.Echoglossian plugin,
        DirectoryInfo stateRoot,
        DirectoryInfo pluginSavePath,
        FileInfo configPath)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Performs best-effort cleanup for startup failures that happen before the
    /// rail can return a <see cref="StartedPlugin"/> owner.
    /// </summary>
    /// <param name="mockContainer">The owning DalaMock container, when available.</param>
    /// <param name="plugin">The started production plugin, when startup reached construction.</param>
    /// <param name="stateRoot">The isolated local state root created for the failed run.</param>
    private void TryCleanupFailedStartup(
        MockContainer? mockContainer,
        global::Echoglossian.Echoglossian? plugin,
        DirectoryInfo stateRoot)
    {
        try
        {
            if (plugin is not null)
            {
                HeadlessPluginCleanup.PrepareForHeadlessDispose(plugin);
            }

            this.TryDisposeContainerAndPluginIfNeeded(mockContainer, plugin);
        }
        catch
        {
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            this.TryDeleteStateRoot(stateRoot);
        }
    }

    /// <summary>
    /// Disposes the owning mock container and falls back to direct plugin
    /// disposal when the container did not unload the plugin.
    /// </summary>
    /// <param name="mockContainer">The owning DalaMock container, when available.</param>
    /// <param name="plugin">The started production plugin, when available.</param>
    private void TryDisposeContainerAndPluginIfNeeded(
        MockContainer? mockContainer,
        global::Echoglossian.Echoglossian? plugin)
    {
        switch (mockContainer)
        {
            case IAsyncDisposable asyncDisposable:
                asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }

        if (plugin is not null &&
            !plugin.StartupAudit.CaptureSnapshot().HasStage(global::Echoglossian.PluginRuntime.Startup.PluginStartupStage.DisposeStarted))
        {
            plugin.Dispose();
        }
    }

    /// <summary>
    /// Deletes the isolated local state created for a failed startup run when
    /// the host has fully released its file handles.
    /// </summary>
    /// <param name="stateRoot">The isolated local state root for the failed run.</param>
    private void TryDeleteStateRoot(DirectoryInfo stateRoot)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                if (stateRoot.Exists)
                {
                    stateRoot.Delete(true);
                }

                return;
            }
            catch (IOException)
            {
                if (attempt == 9)
                {
                    return;
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(50));
                stateRoot.Refresh();
            }
            catch (UnauthorizedAccessException)
            {
                if (attempt == 9)
                {
                    return;
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(50));
                stateRoot.Refresh();
            }
        }
    }
}

/// <summary>
/// Adapts the synchronous production plugin to DalaMock's asynchronous plugin
/// startup contract without changing the production entrypoint.
/// </summary>
internal sealed class EchoglossianAsyncPluginAdapter : IAsyncDalamudPlugin
{
    private readonly IComponentContext componentContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="EchoglossianAsyncPluginAdapter"/> class.
    /// </summary>
    /// <param name="componentContext">The DalaMock Autofac component context.</param>
    public EchoglossianAsyncPluginAdapter(IComponentContext componentContext)
    {
        this.componentContext = componentContext;
    }

    /// <summary>
    /// Gets the real plugin instance after it has been loaded.
    /// </summary>
    public global::Echoglossian.Echoglossian? Plugin { get; private set; }

    /// <summary>
    /// Creates the real plugin after assigning the static Dalamud services it expects.
    /// </summary>
    /// <param name="cancellationToken">Cancels plugin creation.</param>
    /// <returns>A completed task once the production plugin has been constructed.</returns>
    public Task LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        global::Echoglossian.Echoglossian.DManager =
            this.componentContext.Resolve<IDataManager>();
        global::Echoglossian.Echoglossian.PluginInterface =
            this.componentContext.Resolve<IDalamudPluginInterface>();
        global::Echoglossian.Echoglossian.CommandManager =
            this.componentContext.Resolve<ICommandManager>();
        global::Echoglossian.Echoglossian.FrameworkInterface =
            this.componentContext.Resolve<IFramework>();
        global::Echoglossian.Echoglossian.GameGuiInterface =
            this.componentContext.Resolve<IGameGui>();
        global::Echoglossian.Echoglossian.ChatGuiInterface =
            this.componentContext.Resolve<IChatGui>();
        global::Echoglossian.Echoglossian.ClientStateInterface =
            this.componentContext.Resolve<IClientState>();
        global::Echoglossian.Echoglossian.SeStringEvaluator =
            this.componentContext.Resolve<ISeStringEvaluator>();
        global::Echoglossian.Echoglossian.ToastGuiInterface =
            this.componentContext.Resolve<IToastGui>();
        global::Echoglossian.Echoglossian.UnlockStateInterface =
            this.componentContext.Resolve<IUnlockState>();
        global::Echoglossian.Echoglossian.EventManager =
            this.ResolveOptionalService<IAddonEventManager>(
                static () => new MockAddonEventManager());
        global::Echoglossian.Echoglossian.AddonLifecycle =
            this.componentContext.Resolve<IAddonLifecycle>();
        global::Echoglossian.Echoglossian.PluginLog =
            this.componentContext.Resolve<IPluginLog>();
        global::Echoglossian.Echoglossian.NotificationManager =
            this.ResolveOptionalService<INotificationManager>(
                static () => new MockNotificationManager());
        global::Echoglossian.Echoglossian.TextureProvider =
            HeadlessTextureProviderProxy.Create(
                this.componentContext.Resolve<ITextureProvider>());

        this.Plugin = new global::Echoglossian.Echoglossian();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Disposes the real production plugin when DalaMock unloads the adapter.
    /// </summary>
    /// <returns>A completed value task after disposal finishes.</returns>
    public ValueTask DisposeAsync()
    {
        this.Plugin?.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Resolves a service from DalaMock, or falls back to a local test stub when absent.
    /// </summary>
    /// <typeparam name="TService">The service type to resolve.</typeparam>
    /// <param name="fallbackFactory">Creates the local fallback when the service is unavailable.</param>
    /// <returns>The resolved service or fallback stub.</returns>
    private TService ResolveOptionalService<TService>(Func<TService> fallbackFactory)
        where TService : class
    {
        return this.componentContext.TryResolve<TService>(out var service)
            ? service
            : fallbackFactory();
    }
}

/// <summary>
/// Wraps DalaMock's headless texture provider so constructor-time embedded image
/// loads can complete without a window-backed graphics device.
/// </summary>
internal class HeadlessTextureProviderProxy : DispatchProxy
{
    private ITextureProvider inner = null!;

    /// <summary>
    /// Creates a local texture-provider shim for headless startup tests.
    /// </summary>
    /// <param name="inner">The underlying DalaMock texture provider.</param>
    /// <returns>A proxy that intercepts constructor-time image loads.</returns>
    public static ITextureProvider Create(ITextureProvider inner)
    {
        var proxy = Create<ITextureProvider, HeadlessTextureProviderProxy>();
        ((HeadlessTextureProviderProxy)(object)proxy).inner = inner;
        return proxy;
    }

    /// <summary>
    /// Intercepts texture-provider calls and supplies a local stub texture for
    /// constructor-time image loads under headless DalaMock.
    /// </summary>
    /// <param name="targetMethod">The invoked texture-provider method.</param>
    /// <param name="args">The method arguments.</param>
    /// <returns>The delegated or locally synthesized method result.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the proxy receives an invocation without method metadata.</exception>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null)
        {
            throw new InvalidOperationException("The headless texture-provider shim received no target method.");
        }

        if (targetMethod.Name == nameof(ITextureProvider.CreateFromImageAsync))
        {
            var debugName = args is { Length: > 1 } ? args[1] as string : null;
            return Task.FromResult<IDalamudTextureWrap>(new HeadlessTextureWrap(debugName));
        }

        return targetMethod.Invoke(this.inner, args);
    }
}

/// <summary>
/// Represents a minimal disposable texture handle for headless startup tests.
/// </summary>
internal sealed class HeadlessTextureWrap : IDalamudTextureWrap
{
    private readonly string debugName;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeadlessTextureWrap"/> class.
    /// </summary>
    /// <param name="debugName">The optional debug name attached to the synthetic texture.</param>
    public HeadlessTextureWrap(string? debugName = null)
    {
        this.debugName = debugName ?? "headless";
    }

    /// <inheritdoc/>
    public ImTextureID Handle => default;

    /// <inheritdoc/>
    public int Width => 1;

    /// <inheritdoc/>
    public int Height => 1;

    /// <inheritdoc/>
    public Vector2 Size => new(this.Width, this.Height);

    /// <inheritdoc/>
    public IDalamudTextureWrap CreateWrapSharingLowLevelResource()
    {
        return new HeadlessTextureWrap(this.debugName);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return this.debugName;
    }
}

/// <summary>
/// Provides a no-op notification manager for local startup tests when DalaMock
/// does not supply one.
/// </summary>
internal sealed class MockNotificationManager : INotificationManager
{
    /// <summary>
    /// Adds a notification and returns a test-owned active notification wrapper.
    /// </summary>
    /// <param name="notification">The notification to track.</param>
    /// <returns>The tracked active notification.</returns>
    public IActiveNotification AddNotification(Notification notification)
    {
        return new MockActiveNotification(notification);
    }
}

/// <summary>
/// Represents a local active notification wrapper used by startup tests.
/// </summary>
internal sealed class MockActiveNotification : IActiveNotification
{
    private static long nextId = 1;
    private readonly Notification notification;
    private DateTime effectiveExpiry;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockActiveNotification"/> class.
    /// </summary>
    /// <param name="notification">The notification being tracked.</param>
    public MockActiveNotification(Notification notification)
    {
        this.notification = notification;
        this.Id = Interlocked.Increment(ref nextId);
        this.CreatedAt = DateTime.UtcNow;
        this.effectiveExpiry = notification.HardExpiry;
    }

    /// <inheritdoc/>
    public event Action<INotificationDismissArgs>? Dismiss;

    /// <inheritdoc/>
    public event Action<INotificationClickArgs>? Click;

    /// <inheritdoc/>
    public event Action<INotificationDrawArgs>? DrawActions;

    /// <inheritdoc/>
    public long Id { get; }

    /// <inheritdoc/>
    public DateTime CreatedAt { get; }

    /// <inheritdoc/>
    public DateTime EffectiveExpiry => this.effectiveExpiry;

    /// <inheritdoc/>
    public NotificationDismissReason? DismissReason { get; private set; }

    /// <inheritdoc/>
    public string Content
    {
        get => this.notification.Content ?? string.Empty;
        set => this.notification.Content = value;
    }

    /// <inheritdoc/>
    public string Title
    {
        get => this.notification.Title ?? string.Empty;
        set => this.notification.Title = value;
    }

    /// <inheritdoc/>
    public string MinimizedText
    {
        get => this.notification.MinimizedText ?? string.Empty;
        set => this.notification.MinimizedText = value;
    }

    /// <inheritdoc/>
    public NotificationType Type
    {
        get => this.notification.Type;
        set => this.notification.Type = value;
    }

    /// <inheritdoc/>
    public INotificationIcon? Icon
    {
        get => this.notification.Icon;
        set => this.notification.Icon = value;
    }

    /// <inheritdoc/>
    public ISharedImmediateTexture? IconTexture
    {
        get => this.notification.IconTexture;
        set => this.notification.IconTexture = value;
    }

    /// <inheritdoc/>
    public DateTime HardExpiry
    {
        get => this.notification.HardExpiry;
        set
        {
            this.notification.HardExpiry = value;
            this.effectiveExpiry = value;
        }
    }

    /// <inheritdoc/>
    public TimeSpan InitialDuration
    {
        get => this.notification.InitialDuration;
        set => this.notification.InitialDuration = value;
    }

    /// <inheritdoc/>
    public TimeSpan ExtensionDurationSinceLastInterest
    {
        get => this.notification.ExtensionDurationSinceLastInterest;
        set => this.notification.ExtensionDurationSinceLastInterest = value;
    }

    /// <inheritdoc/>
    public bool ShowIndeterminateIfNoExpiry
    {
        get => this.notification.ShowIndeterminateIfNoExpiry;
        set => this.notification.ShowIndeterminateIfNoExpiry = value;
    }

    /// <inheritdoc/>
    public bool RespectUiHidden
    {
        get => this.notification.RespectUiHidden;
        set => this.notification.RespectUiHidden = value;
    }

    /// <inheritdoc/>
    public bool Minimized
    {
        get => this.notification.Minimized;
        set => this.notification.Minimized = value;
    }

    /// <inheritdoc/>
    public bool UserDismissable
    {
        get => this.notification.UserDismissable;
        set => this.notification.UserDismissable = value;
    }

    /// <inheritdoc/>
    public float Progress
    {
        get => this.notification.Progress;
        set => this.notification.Progress = value;
    }

    /// <inheritdoc/>
    public void DismissNow()
    {
        this.DismissReason ??= NotificationDismissReason.Programmatical;
    }

    /// <inheritdoc/>
    public void ExtendBy(TimeSpan extension)
    {
        if (this.effectiveExpiry == DateTime.MaxValue)
        {
            return;
        }

        this.effectiveExpiry = this.effectiveExpiry.Add(extension);
    }
}

/// <summary>
/// Provides a no-op addon event manager for local startup tests when DalaMock
/// does not supply one.
/// </summary>
internal sealed class MockAddonEventManager : IAddonEventManager
{
    /// <inheritdoc/>
    public IAddonEventHandle? AddEvent(
        nint atkUnitBase,
        nint atkResNode,
        AddonEventType eventType,
        IAddonEventManager.AddonEventDelegate eventDelegate)
    {
        return null;
    }

    /// <inheritdoc/>
    public void RemoveEvent(IAddonEventHandle eventHandle)
    {
    }

    /// <inheritdoc/>
    public void SetCursor(AddonCursorType cursor)
    {
    }

    /// <inheritdoc/>
    public void ResetCursor()
    {
    }
}
