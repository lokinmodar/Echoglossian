<img src="https://github.com/lokinmodar/Echoglossian/raw/APIv4/images/logo.png" align="right" width="260px"/>

# Echoglossian

[![Download count](https://img.shields.io/endpoint?url=https://qzysathwfhebdai6xgauhz4q7m0mzmrf.lambda-url.us-east-1.on.aws/Echoglossian)](https://github.com/lokinmodar/Echoglossian)
[![GitHub stars](https://badgen.net/github/stars/lokinmodar/Echoglossian)](https://GitHub.com/lokinmodar/Echoglossian/stargazers/)
[![GitHub release](https://img.shields.io/github/release/lokinmodar/Echoglossian.svg)](https://GitHub.com/lokinmodar/Echoglossian/releases/)
[![Crowdin](https://badges.crowdin.net/echoglossian/localized.svg)](https://crowdin.com)

## A realtime game text translator

## Installation

**Please install through [FFXIVQuickLauncher](https://github.com/goatcorp/FFXIVQuickLauncher)!**

## Chat Commands

***/eglo*** : Opens the Plugin Configuration Window
  
***/eglodbmanager*** : Opens the Plugin Database Manager Window


## Usage

Enable the desired translations and configure the various options through the Configuration Window.

For an up-to-date inventory of what can be translated, which mode family each
surface uses, and which surfaces are temporarily hidden or disabled in the
current release, see
[docs/translation-surface-support-matrix.md](docs/translation-surface-support-matrix.md).

For current translation-engine architecture and future LLM-specific
improvements such as surface-group engine routing and optional dialogue
session context, see:

- [docs/translation-engines-architecture-and-flows.md](docs/translation-engines-architecture-and-flows.md)
- [docs/llm-translation-improvements-plan.md](docs/llm-translation-improvements-plan.md)

For the current runtime flows of the dialogue-family and toast-family addon
handlers, including `Talk`, `BattleTalk`, `TalkSubtitle`, `MiniTalk`,
`TextGimmickHint`, and the addon-driven versus callback-driven toast paths, see:

- [docs/dialogue-and-toast-runtime-flows.md](docs/dialogue-and-toast-runtime-flows.md)

Localized versions of that document:

- [English](docs/translation-surface-support-matrix.md)
- [Brazilian Portuguese](docs/translation-surface-support-matrix.pt-BR.md)
- [Portuguese](docs/translation-surface-support-matrix.pt.md)
- [German](docs/translation-surface-support-matrix.de.md)
- [Danish](docs/translation-surface-support-matrix.da.md)
- [Greek](docs/translation-surface-support-matrix.el.md)
- [Spanish](docs/translation-surface-support-matrix.es.md)
- [Basque](docs/translation-surface-support-matrix.eu.md)
- [French](docs/translation-surface-support-matrix.fr.md)
- [Italian](docs/translation-surface-support-matrix.it.md)
- [Russian](docs/translation-surface-support-matrix.ru.md)
- [Vietnamese](docs/translation-surface-support-matrix.vi.md)
- [Simplified Chinese](docs/translation-surface-support-matrix.zh-CN.md)
- [Traditional Chinese (Taiwan)](docs/translation-surface-support-matrix.zh-TW.md)

## Official Repo AI Disclosure

For work intended for the official Dalamud plugin repository, AI usage beyond
basic autocomplete should be disclosed in the PR description, and AI-generated
assets should be disclosed as well. Repo-local guidance lives in
[docs/official-plugin-repo-ai-usage-disclosure.md](docs/official-plugin-repo-ai-usage-disclosure.md).

## Known Issues

- Refer to [This](https://github.com/lokinmodar/Echoglossian/issues/12)

## TODOs

- [Non-exhaustive list](https://github.com/users/lokinmodar/projects/2)

## Thanks

- annaclemens for XivCommon
- midorikami for AddonLifecycle
- goaats and the gang for dalamud and FFXIVQuickLauncher
- haplo for ChatTranslator plugin
- Eternita-S for their contribution
- Bluefissure for their contribution
- Soreepeong aka Kizer for the fontConfig fix
- samulopez for all the contributions
- pbzweihander for all the contributions
- Critical-Impact projects such as InventoryTools, AllaganMarket, CriticalCommonLib, LuminaSupplemental, DalaMock, and AllaganLib for practical Lumina/data-access patterns
- Era-FFXIV QuestShare.Plugin for quest progression and quest-sheet resolution patterns
- HaselDebug for addon inspection and probe patterns
- DelvUI and DelvCD for tooltip/hover UX patterns
- ChatBubbles for bubble-style addon handling references

## Contributors

<a href="https://github.com/lokinmodar/Echoglossian/graphs/contributors">
	<img src="https://contrib.rocks/image?repo=lokinmodar/Echoglossian" />
</a>


[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/J3J35HJVY)

Donate crypto: bc1qfg9l4zt02k7wjgfr6t322razhj7le8f9wcfwwe

You can also support the project via [![pix](https://github.com/lokinmodar/Echoglossian/raw/APIv4/images/pixlogo.png)](https://github.com/lokinmodar/Echoglossian/raw/APIv4/images/pix.png) (only valid in Brazil)

###### Final Fantasy XIV © 2010-2026 SQUARE ENIX CO., LTD. All Rights Reserved. I am not affiliated with SQUARE ENIX CO., LTD. in any way.
