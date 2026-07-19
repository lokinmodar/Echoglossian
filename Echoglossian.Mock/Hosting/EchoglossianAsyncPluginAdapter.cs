// <copyright file="EchoglossianAsyncPluginAdapter.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Autofac;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Events;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.ImGuiNotification.EventArgs;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Echoglossian.Mock.Hosting;

/// <summary>
/// Adapts the synchronous production plugin to DalaMock's asynchronous plugin
/// startup contract without changing the production entrypoint.
/// </summary>
public sealed class EchoglossianAsyncPluginAdapter : IAsyncDalamudPlugin
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
        global::Echoglossian.Echoglossian.NamePlateGuiInterface =
            this.componentContext.Resolve<INamePlateGui>();
        global::Echoglossian.Echoglossian.ObjectTableInterface =
            this.componentContext.Resolve<IObjectTable>();
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
    /// Resolves a service from DalaMock, or falls back to a local stub when absent.
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
    /// Creates a local texture-provider shim for headless startup flows.
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
/// Represents a minimal disposable texture handle for headless startup flows.
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
/// Provides a no-op notification manager for headless startup flows when
/// DalaMock does not supply one.
/// </summary>
internal sealed class MockNotificationManager : INotificationManager
{
    /// <summary>
    /// Adds a notification and returns a mock active notification wrapper.
    /// </summary>
    /// <param name="notification">The notification to track.</param>
    /// <returns>The tracked active notification.</returns>
    public IActiveNotification AddNotification(Notification notification)
    {
        return new MockActiveNotification(notification);
    }
}

/// <summary>
/// Represents a local active notification wrapper used by headless startup flows.
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
/// Provides a no-op addon event manager for headless startup flows when DalaMock
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
