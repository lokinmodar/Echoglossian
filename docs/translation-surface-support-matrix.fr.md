<!--
  Copyright (c) lokinmodar. All rights reserved.
  Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
-->

# Matrice de prise en charge des surfaces de traduction

## Familles de modes de traduction

| Famille de modes | Modes |
| --- | --- |
| Native-tooltip family | Native UI Translation; Tooltip Translation Only; Native UI Translation With Original Tooltips |
| Overlay family | Native UI Translation; Overlay Translation Only; Native UI Translation With Original Overlay |
| Native / distance-aware hybrid family | Native UI Translation; Tooltip Translation Only; Native UI Translation With Original Tooltips |
| Quest / native-window family | Native UI Translation; Tooltip Translation Only; Native UI Translation With Original Tooltips |

## Surfaces de dialogue et d’overlay

| Surface | Bascule de configuration | Modes | Notes | État de la version actuelle |
| --- | --- | --- | --- | --- |
| Talk | `TranslateTalk` | Overlay family | Supports translated NPC names through TranslateTalkNpcNames. | Enabled |
| BattleTalk | `TranslateBattleTalk` | Overlay family | Supports translated NPC names through TranslateBattleTalkNpcNames. | Enabled |
| TalkSubtitle | `TranslateTalkSubtitle` | Overlay family | Uses titleless overlay presentation when overlay mode is active. | Enabled |
| MiniTalk | `TranslateMiniTalk` | Overlay family | Small native surface; verbose text still requires careful native reflow. | Enabled |
| CutSceneSelectString | `TranslateCutSceneSelectString` | Overlay family | Question becomes the title and options become the body in overlay mode. | Enabled |
| Yes/No dialog | `TranslateYesNoScreen` | Native-tooltip family | Uses structured plugin tooltips instead of overlay windows and supports native, tooltip-only, and swap presentation. | Enabled |
| SelectOk dialog | `TranslateSelectOk` | Native-tooltip family | Uses structured plugin tooltips instead of overlay windows and supports native, tooltip-only, and swap presentation. | Enabled |
| SelectString dialog | `TranslateSelectString` | Native-tooltip family | Uses structured plugin tooltips instead of overlay windows and supports native, tooltip-only, and swap presentation; prefers SelectString and falls back to SelectionDialogText. | Enabled |
| SelectIconString dialog | `TranslateSelectIconString` | Native-tooltip family | Keeps its own toggle and display mode and uses body-only structured tooltip presentation. | Enabled |

## Surfaces de quête et de journal

| Surface | Bascule de configuration | Modes | Notes | État de la version actuelle |
| --- | --- | --- | --- | --- |
| Journal | `TranslateJournal` | Quest / native-window family | Quest list surface. | Enabled |
| JournalDetail | `TranslateJournalDetail` | Quest / native-window family | Dense body layout; native mode requires explicit block reflow. | Enabled |
| ToDoList | `TranslateToDoList` | Quest / native-window family | Quest tracker / objective list. | Enabled |
| ToDo | `TranslateToDo` | Quest / native-window family | Instanced/FATE objective tracker. | Enabled |
| ScenarioTree | `TranslateScenarioTree` | Quest / native-window family | Main scenario tracker. | Enabled |
| JournalAccept | `TranslateJournalAccept` | Quest / native-window family | Uses QuestPlate when a safe quest id is available and QuestPopupText fallback for live popup capture. | Enabled |
| JournalResult | `TranslateJournalResult` | Quest / native-window family | Prefers QuestPlate canonical lookup and falls back to QuestPopupText while missing rows are translated live. | Enabled |
| RecommendList | `TranslateRecommendList` | Quest / native-window family | Recommendation list. | Enabled |
| AreaMap | `TranslateAreaMap` | Quest / native-window family | Quest text inside map-related quest UI; AreaMap and _NaviMap are string-array-backed. | Enabled |

## Surfaces de toast

| Surface | Bascule de configuration | Modes | Notes | État de la version actuelle |
| --- | --- | --- | --- | --- |
| WideText / Screen Info toast | `TranslateWideTextToast` | Overlay family | Large center-screen information toast. | Enabled |
| Error toast | `TranslateErrorToast` | Overlay family | Error / failure notifications. | Enabled |
| Area toast | `TranslateAreaToast` | Overlay family | Area and location notifications. | Enabled |
| Class / Job change toast | `TranslateClassChangeToast` | Overlay family | Class/job change announcement. | Enabled |
| Text gimmick hint | `TranslateTextGimmickHint` | Overlay family | Gimmick/tutorial hint surface. | Enabled |
| Quest toast | `TranslateQuestToast` | Overlay family | Quest-related toast notification. | Enabled |

## Surfaces des fenêtres du jeu

| Surface | Bascule de configuration | Modes | Notes | État de la version actuelle |
| --- | --- | --- | --- | --- |
| Character window | `TranslateCharacterWindow` | Quest / native-window family | DB-first game-window runtime. | Enabled |
| Main Command | `TranslateMainCommandWindow` | Quest / native-window family | DB-first game-window runtime. | Enabled |
| Action Menu | `TranslateActionMenuWindow` | Quest / native-window family | DB-first game-window runtime. | Enabled |
| HUD windows | `TranslateHudWindow` | Quest / native-window family | DB-first game-window runtime. | Enabled |
| Operation Guide | `TranslateOperationGuideWindow` | Quest / native-window family | DB-first game-window runtime. | Enabled |
| Addon Context Menu Title | `TranslateAddonContextMenuTitle` | Quest / native-window family | DB-first game-window runtime. | Enabled |
| Context Menu | `TranslateContextMenu` | Native-tooltip family | Dedicated DB-first row-chain runtime with row-local hover targets. | Enabled |
| Tooltip addon | `TranslateTooltipAddon` | Native-tooltip family | Dedicated DB-first Tooltip addon runtime; tooltip translation and swap use an anchored overlay on the live game tooltip. | Enabled |
| Action / item detail tooltips | `TranslateTooltips` | Quest / native-window family | DB-first structured tooltip runtime; defaults to Plugin Tooltip mode, while native writes are opt-in and guarded to plain-text-safe nodes. | Enabled |

## Surfaces du monde et NamePlate

| Surface | Bascule de configuration | Modes | Notes | État de la version actuelle |
| --- | --- | --- | --- | --- |
| NamePlates | `TranslateNamePlates` | Native / distance-aware hybrid family | Standard languages keep the native backend; overlay-only languages keep the native nameplate original and render the translation through the distance-aware overlay backend. | Enabled |

## Surfaces cachées ou temporairement restreintes

| Surface | Bascule de configuration | Modes | Notes | État de la version actuelle |
| --- | --- | --- | --- | --- |
