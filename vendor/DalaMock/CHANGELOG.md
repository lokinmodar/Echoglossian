# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

### Changed

### Removed

## [6.1.7] - 2026-06-07

### Fixed
- Added overload of IsUnlockLinkUnlocked to MockUnlockState

## [6.1.6] - 2026-05-27

### Fixed
- Added IsClassJobUnlocked to MockUnlockState

## [6.1.5] - 2026-05-27

### Changed
- Fix typo

## [6.1.4] - 2026-05-27

### Changed
- Added PreCreatingAsync virtual method for running tasks before the container creates
- PreBuild switched to PreBuildingAsync

## [6.1.3] - 2026-05-27

### Fixed
- Use newer cimgui DLLs

## [6.1.2] - 2026-05-24

### Fixed
- Fixed double cimgui reference

## [6.1.1] - 2026-05-24

### Changed
- Try to narrow down crash

## [6.1.0] - 2026-05-24

### Added
- Added custom font/style support to DalaMock.
- Added full support for game fonts + ttf fonts
- Added IFontChooserFactory and made it available in IReplacementContainer
- Added a font test window to the sample.
- Added ability to set the global ui scale, style and default font which are currently saved locally per dalamock instance.

## [6.0.1] - 2026-05-10

### Added
- Added MockNamePlateGui

## [6.0.0] - 2026-05-06

### Breaking Changes:
This version of DalaMock introduces several breaking changes with how non-dalamud services are provided to your plugin. 
 - Previously it was up to the developer to construct replacement services for services like IWindowSystemFactory, IImGuiComponents, IFileDialogManager
 - Now when constructing the mock version of your plugin you can request a MockReplacementContainer which will have the services provided by DalaMock that are not native to Dalamud. Override the MockReplacementContainer property on your mock plugin class and pass the MockReplacementContainer instance provided to it. The mock services will be injected automatically.
 - If you are using HostedPlugin IImGuiComponents, IWindowSystemFactory, IFont, IFileDialogManager will automatically be injected into your container.
 - If you are not using HostedPlugin, you will be responsible for handling service replacements.
 - HostedPlugin's now implement IAsyncDalamudPlugin. HostedPlugin provides StartingAsync, StoppingAsync, StartedAsync and StoppedAsync virtual methods that can be overridden if you need to perform tasks when these events occur. 

### Required Changes(HostBuilder plugin):
 - Request a `MockReplacementContainer mockReplacementContainer` in your Mock plugin's 
 - Override MockReplacementContainer `public override IReplacementContainer MockReplacementContainer => this.mockReplacementContainer;` inside your mock class.
 - If you were previously registering any of DalaMocks services in your container remove them
 - Remove these from your plugin

```
        containerBuilder.RegisterType<FileDialogManager>().SingleInstance();
        containerBuilder.RegisterType<DalamudFileDialogManager>().As<IFileDialogManager>().SingleInstance();
```
- Remove these from your mock plugin
```
        containerBuilder.RegisterType<MockWindowSystem>().AsSelf().As<IWindowSystem>().SingleInstance();
        containerBuilder.RegisterType<MockFileDialogManager>().AsSelf().As<IFileDialogManager>().SingleInstance();
        containerBuilder.RegisterType<MockFont>().AsSelf().As<IFont>().SingleInstance();
```

### Other Changes:
- Split the plugin loader window 
- Introduced IWindowSystemFactory and MockWindowSystemFactory
- ImGuiComponents now includes HelpMarker
- Switch DalaMock's WindowSystem to a similar system dalamud now uses.
- Implemented titlebar button support for plugin windows.
- Added a main menu allowing for dalamock provided windows to be hidden.
- Stopped publishing DalaMock.Sample
- Split DalaMock.Sample into DalaMock.Sample.Mock and added DalaMock.Sample.Tests

## [5.1.1] - 2026-05-03

- Bump core version

## [5.1.0] - 2026-05-02

- API15 support
- Switched to using Dalamud's IWindowSystem

## [5.0.2] - 2026-04-08

- Add FontIconFixedWidth to MockUiBuilder

## [5.0.1] - 2026-03-21

- Correctly provide both the implementation of MockServices and the underlying interface when running a mock project
- Fix issue that'd break test runs
- Add MockUnlockState and UnlockStateWidget


## [5.0.0] - 2026-03-12

- Breaking Change: Dalamud dependencies no longer need to be provided when using HostedPlugin, only a IDalamudPluginInterface is required for construction.
- Added a dalamud registration source if you aren't using HostedPlugin
- Implemented MockGameConfig for IGameConfig
- Implemented MockSeStringEvaluator for ISeStringEvaluator
- Implemented MockReliableFileStorage for IReliableFileStorage
- Implemented EffectiveLanguage and LanguageOverride in MockDalamudConfiguration
- Added missing properties to MockDalamudPluginInterface
- Update deps

## [4.1.1] - 2026-02-17

- Update DataShare to function like Dalamuds
- Update MockCharacter to include CustomizeData

## [4.1.0] - 2026-02-15

- MockContainer's serviceReplacements dictionary now expects a <InterfaceType, ImplementingType>. The interface should be the dalamud service you want to provide. The implementing type should implement the interface plus IMockService.
- If DalaMock already provides a mock for the dalamud service, it will be replaced. If DalaMock does not provide a mock, it will be added to the service container.

## [4.0.6] - 2026-02-09

- The 3 default fonts are now loaded as embedded resources and have the game glyphs merged
- The window size/position/state are saved in a dalamock_ui.json and DalaMock will attempt to restore these
- HostedPlugin now exposes HostedEvents and has a virtual Dispose
- The sample now includes examples of overriding a DalaMock service and subscribing to HostedEvents outside the regular host/DI workflow
- Fixed an issue with the wrong serilog being used for mocks

## [4.0.5] - 2026-02-05

- Add DataShare to MockDalamudPluginInterface
- Added DataShare widget and Mocks Window
- Added IImGuiComponents
- MockUiBuilder now provides the correct fonts

## [4.0.4] - 2026-01-30

- RegisterTransientsSelfAndInterfaces now returns the IRegistrationBuilder
- Implement MinimumWidth in MockDtrBar

## [4.0.3] - 2025-12-20

- Added LogMessage to MockChatGui

## [4.0.2] - 2025-12-20

### Fixed
- Don't use STG

## [4.0.1] - 2025-12-20

### Added
- Add stubbed MockPlayerState for IPlayerState


## [4.0.0] - 2025-12-20

### Added
- Initial support for API14

## [3.0.12] - 2025-11-29

### Added
- Add missing properties

## [3.0.11] - 2025-11-15

### Added
- Add missing properties/events

## [3.0.10] - 2025-11-15

### Added
- Add missing properties/events

## [3.0.9] - 2025-10-08

### Added
- Added ZoneInit to MockClientState

## [3.0.8] - 2025-10-01

### Fixed
- Fixed minor issues with the DalaMock.PluginTemplate


## [3.0.7] - 2025-10-01

### Changed
- Updated DalaMock.PluginTemplate
- Added missing methods/events

## [3.0.6] - 2025-08-28

### Added
- Added RegisterTransientSelf to ContainerBuilderExtensions

## [3.0.5] - 2025-08-26

### Fixed
- A very important typo

## [3.0.4] - 2025-08-26

### Changed
- The MockContainer now outputs more logging when EXD_DATA_DIR is specified along with outputting the game path when booting

### Fixed
- Correct BC7 handling as Lumina has now fixed the issue

## [3.0.3] - 2025-08-21

### Added
- HostedPlugin now supports IHostedService registration along with a facility to replace those services when mocking/unit testing

## [3.0.2] - 2025-08-13

### Added
- Added EXD_DATA_DIR environment variable, allowing for the exd path to be provided for CI
- Added DALAMOCK_SAVE_DIR environment variable
- DalaMock can be configured to not spawn a window and provide a null texture provider and ui builder allowing for use in CI

## [3.0.1] - 2025-08-09

### Changed
- Revert ChatLinkHandler updates
- Add DtrBar OnClick
- Handle BC/BC7/DXT pixel formats. This currently relies on an unreleased Lumina PR to support BC5/BC7.

## [3.0.0] - 2025-08-06

### Changed
- Initial support for API13

## [2.3.1] - 2025-06-17

### Added
- Added DalaMock.PluginTemplate allowing you to boostrap a hosted/di/mock driven plugin quickly

## [2.3.0] - 2025-06-16

### Added
- Improvements to HostedPlugin including
  - Will now emit events when built, starting, stopping and stopped
  - Can now be configured to register a MediatorService for you
  - HostingAwareService added which will provide automatic registration to plugin events

### Changed

- Updated dependencies
- The game path and plugin path will be automatically resolved if not provided
- MockDalamudPluginInterface will now return the real manifest if available
- DalaMock.Sample is now more opinionated

## [2.2.8] - 2025-05-29

### Changed

- DalaMock.Host now injects a dalamud ILogger provider
- Improvements to how DalaMock handles keyboard input from SDL2


## [2.2.7] - 2025-05-11

### Changed

- Add stub for ITextureProvider.CreateDrawListTexture and ITextureProvider.CreateFromClipboardAsync

## [2.2.6] - 2025-05-03

### Fixed

- MockWindowSystem now implements it's own draw logic instead of inheriting from DalamudWindowSystem allowing it to avoid any breakages occured by changes to that class.

## [2.2.5] - 2025-05-01

### Fixed

- Fixed slow dispose on MediatorService

## [2.2.4] - 2025-04-22

### Changed

- Updated all nuget packages
- Updated included cimgui so/dll files

## [2.2.3] - 2025-04-06

### Changed

- DalaMock.Host
    - Stop MediatorService from accepting empty message lists

## [2.2.2] - 2025-04-03

### Changed

- DalaMock.Host
  - Use semaphore for MediatorService to make message queue efficient

## [2.2.1] - 2025-03-29

### Added

- Bump for latest dalamud patch


## [2.2.0] - 2025-03-26

### Added

- Initial support for API12/7.2


## [2.1.7] - 2024-12-30

### Added

- ImGui asserts will be caught and logged

## [2.1.6] - 2024-11-23

### Fixed

- Use correct dalamud branch


## [2.1.5] - 2024-11-23

### Fixed

- Implement missing IChatGui methods

## [2.1.4] - 2024-11-18

### Fixed

- Use dalamud Serilog when possible


## [2.1.3] - 2024-11-18

### Fixed

- Use same nuget packages that dalamud does

## [2.1.2] - 2024-11-16

### Fixed

- Make GameData single instance

## [2.1.1] - 2024-11-13

### Fixed

- Add/update mocks for API11

## [2.1.0-alpha] - 2024-10-21

### Fixed

- Initial support for API11

## [2.0.28] - 2024-09-30

### Fixed

- Added missing ICallGateProvider property

## [2.0.27] - 2024-09-26

### Added

- Use MS logging for mocks

## [2.0.26] - 2024-08-20

### Added

- Added GetFileAsync to MockDataManager

## [2.0.25] - 2024-08-04

### Added

- Added IDtrBar mock service

### Fixed

- Use Microsoft.Extensions.Logging internally in combination with serilog
- Updated Serilog to latest stable release

## [2.0.24] - 2024-08-04

### Fixed

- Provide the correct directory for saving plugin configs

## [2.0.23] - 2024-08-01

### Fixed

- Provide the correct assembly name to the mock plugin interface


## [2.0.22] - 2024-07-28

### Fixed

- Support latest dalamud release
- Use staging zip for release

## [2.0.21] - 2024-07-28

### Fixed

- Support latest dalamud release

## [2.0.20] - 2024-07-28

### Added

- Have sample actually load/save a configuration

### Fixed

- Fix incorrect save path


## [2.0.19] - 2024-07-28

### Added

- DalaMock will ask you for a sqpack directory and plugin config directory if none are provided or can be loaded from the DalaMock configuration file

### Fixed
- Plugin config files/directories should match dalamuds layout

## [2.0.18] - 2024-07-28

### Added

- Added missing properties/methods to dalamud mock services
- Merged MockTextureProvider and MockTextureManager
- Assembly Location can be set via plugin load settings (Styr1x)
- Device and WindowHandlePtr access from UiBuilder (Styr1x)
- CreateFromRaw implementation for TextureProvider (Styr1x)
- Dispose for MockTextureMap (Styr1x)


## [2.0.17] - 2024-07-19

### Added

- Added missing method for MockObjectTable


## [2.0.16] - 2024-07-16

### Added

- Implemented most of MockChatGui

## [2.0.15] - 2024-07-15

### Added

- Add rudimentary font loading to support IFonts FontAwesome font

## [2.0.14] - 2024-07-14

### Added

- MockContainer can accept a list of replacement dalamud services

## [2.0.13] - 2024-07-14

### Added

- MockContainer can have its services overriden before being built

## [2.0.12] - 2024-07-14

### Added

- Dalamud configuration can be overridden

## [2.0.11] - 2024-07-13

### Added

- Sample plugin updated, showing how to inject a dalamud mock service

### Fixed

- Plugin startup failure will be logged.

## [2.0.10] - 2024-07-11

### Added

- Added IFileDialogManager, a wrapper for dalamuds file dialog manager, made to avoid font crashes
- DalaMock will now save and load a global game path and plugin config path

### Fixed

- Block plugin loading when paths are invalid and stop paths being edit if plugin is running

## [2.0.9] - 2024-07-11

### Added

### Fixed

### Changed

### Removed

## [2.0.8] - 2024-07-11

### Added

### Fixed

- Reworked parts of MockDalamudPluginInterface and allowed statics to be injected

### Changed

### Removed


## [2.0.7] - 2024-07-10

### Added

- Implemented Create and Inject for MockDalamudPluginInterface
- Added a changelog ;)

### Fixed

### Changed

### Removed