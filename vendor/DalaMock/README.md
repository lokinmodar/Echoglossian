# DalaMock

DalaMock.Core - [![NuGet](https://img.shields.io/nuget/v/DalaMock.Core.svg)](https://www.nuget.org/packages/DalaMock.Core)

DalaMock.Host - [![NuGet](https://img.shields.io/nuget/v/DalaMock.Host.svg)](https://www.nuget.org/packages/DalaMock.Host)

DalaMock.Shared - [![NuGet](https://img.shields.io/nuget/v/DalaMock.Shared.svg)](https://www.nuget.org/packages/DalaMock.Shared)

DalaMock.PluginTemplate - [![NuGet](https://img.shields.io/nuget/v/DalaMock.PluginTemplate.svg)](https://www.nuget.org/packages/DalaMock.PluginTemplate)

---

DalaMock is a framework for developing and testing [Dalamud](https://github.com/goatcorp/Dalamud) plugins outside of FFXIV. It provides a standalone ImGui host that loads your plugin against mock or real Dalamud service implementations, enabling UI iteration, unit testing, and service wiring without needing the game running.

The typical setup involves three projects alongside your main plugin: a `*.Mock` executable project that boots DalaMock with your plugin loaded, an optional test project that exercises your services in isolation, and your real plugin project which remains unchanged and deployable as a normal Dalamud plugin.

#### DalaMock.Core

The core mock runtime. Spins up a Veldrid-backed ImGui window that hosts your plugin's UI outside the game. Provides:

- `MockContainer` — entry point for configuring and launching the mock environment, with support for substituting individual Dalamud services (e.g. `IPluginLog`, `ISigScanner`) with custom mock implementations
- `PluginLoader` — loads and starts plugin instances within the mock host
- Mock windows for simulating Dalamud state (client state, local players, GameGui, settings)
- `MockDalamudUi` — the top-level mock UI loop; call `Run()` to block and display the mock window

#### DalaMock.Host

A `Microsoft.Extensions.Hosting`-based plugin hosting abstraction for structuring your plugin with full dependency injection. Provides:

- `HostedPlugin` — abstract base class implementing `IAsyncDalamudPlugin`. Override `ConfigureContainer`, `ConfigureServices`, and `ConfigureOptions` to wire up your plugin's Autofac container and `IHostedService` registrations. Handles async plugin lifecycle (`StartingAsync`, `StartedAsync`, `StoppingAsync`, `StoppedAsync`)
- `MediatorService` — an in-process pub/sub mediator for decoupling services within your plugin. Subscribe via `MediatorSubscriberBase` or `WindowMediatorSubscriberBase`
- `DalamudLoggingProvider` — bridges `Microsoft.Extensions.Logging` to Dalamud's `IPluginLog`
- `DalamudServiceRegistrationSource` — Autofac registration source that automatically resolves any Dalamud service from `IDalamudPluginInterface` without manual wiring
- `HostedEvents` — lifecycle event hooks (PluginBuilt, PluginStarted, PluginStopping, PluginStopped)

#### DalaMock.Shared

Shared interfaces and thin Dalamud-backed implementations used by both `DalaMock.Core` and `DalaMock.Host`. Consuming these interfaces in your plugin code allows mock replacements to be injected at runtime. Provides:

- `IFileDialogManager` / `DalamudFileDialogManager` — file dialog abstraction
- `IImGuiComponents` / `DalamudImGuiComponents` — ImGui component abstraction
- `IFont` / `DalamudFont` — font handle abstraction
- `IWindowSystemFactory` — factory interface for creating `WindowSystem` instances
- `IReplacementContainer` — interface for containers that register the above mock/real implementations into an Autofac `ContainerBuilder`
- `ContainerBuilderExtensions` — extension methods for registering DalaMock services into your Autofac container

#### DalaMock.PluginTemplate

A `dotnet new` template that scaffolds a new Dalamud plugin pre-wired for DalaMock. Install it via:

```
dotnet new install DalaMock.PluginTemplate
dotnet new dalamud-mock-plugin -n MyPlugin
```

The generated solution contains a ready-to-build plugin project and a companion `*.Mock` runner project with example service substitutions.

---

#### Sample Projects (not published to NuGet)

- **DalaMock.Sample** — a minimal Dalamud plugin demonstrating the `HostedPlugin` pattern with commands, configuration, and ImGui windows
- **DalaMock.Sample.Mock** — the standalone mock runner for `DalaMock.Sample`, showing how to substitute `IPluginLog` and `ISigScanner` and launch the mock UI
- **DalaMock.Sample.Tests** — xUnit tests for `DalaMock.Sample` that bootstrap the full DI host without the game, demonstrating how to write service-level unit tests against a real container