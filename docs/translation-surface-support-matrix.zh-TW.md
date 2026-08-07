<!--
  Copyright (c) lokinmodar. All rights reserved.
  Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
-->

# 翻譯介面支援矩陣

## 翻譯模式家族

| 模式 | Modes |
| --- | --- |
| Native-tooltip family | Native UI Translation; Tooltip Translation Only; Native UI Translation With Original Tooltips |
| Overlay family | Native UI Translation; Overlay Translation Only; Native UI Translation With Original Overlay |
| Native / distance-aware hybrid family | Native UI Translation; Tooltip Translation Only; Native UI Translation With Original Tooltips |
| Quest / native-window family | Native UI Translation; Tooltip Translation Only; Native UI Translation With Original Tooltips |

## 對話與 Overlay 介面

| 介面 | 設定開關 | 模式 | 說明 | 目前發行狀態 |
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

## 任務與 Journal 介面

| 介面 | 設定開關 | 模式 | 說明 | 目前發行狀態 |
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

## Toast 介面

| 介面 | 設定開關 | 模式 | 說明 | 目前發行狀態 |
| --- | --- | --- | --- | --- |
| WideText / Screen Info toast | `TranslateWideTextToast` | Overlay family | Large center-screen information toast. | Enabled |
| Error toast | `TranslateErrorToast` | Overlay family | Error / failure notifications. | Enabled |
| Area toast | `TranslateAreaToast` | Overlay family | Area and location notifications. | Enabled |
| Class / Job change toast | `TranslateClassChangeToast` | Overlay family | Class/job change announcement. | Enabled |
| Text gimmick hint | `TranslateTextGimmickHint` | Overlay family | Gimmick/tutorial hint surface. | Enabled |
| Quest toast | `TranslateQuestToast` | Overlay family | Quest-related toast notification. | Enabled |

## 遊戲視窗介面

| 介面 | 設定開關 | 模式 | 說明 | 目前發行狀態 |
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

## 世界與 NamePlate 介面

| 介面 | 設定開關 | 模式 | 說明 | 目前發行狀態 |
| --- | --- | --- | --- | --- |
| NamePlates | `TranslateNamePlates` | Native / distance-aware hybrid family | Standard languages keep the native backend; overlay-only languages keep the native nameplate original and render the translation through the distance-aware overlay backend. | Enabled |

## 隱藏或暫時受限的介面

| 介面 | 設定開關 | 模式 | 說明 | 目前發行狀態 |
| --- | --- | --- | --- | --- |
