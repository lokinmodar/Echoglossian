<!--
  Copyright (c) lokinmodar. All rights reserved.
  Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
-->

# 翻译界面支持矩阵

## 翻译模式家族

| 模式家族 | 模式 |
| --- | --- |
| Native-tooltip 家族 | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |
| Overlay 家族 | `Native UI Translation`; `Overlay Translation Only`; `Native UI Translation With Original Overlay` |
| Native / distance-aware 混合家族 | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |
| Quest / native-window 家族 | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |

## 对话与 Overlay 界面

| 界面 | 配置开关 | 模式 | 说明 | 当前发布状态 |
| --- | --- | --- | --- | --- |
| Talk | `TranslateTalk` | Overlay 家族 | 通过 `TranslateTalkNpcNames` 支持翻译 NPC 名称 | 已启用 |
| BattleTalk | `TranslateBattleTalk` | Overlay 家族 | 通过 `TranslateBattleTalkNpcNames` 支持翻译 NPC 名称 | 已启用 |
| TalkSubtitle | `TranslateTalkSubtitle` | Overlay 家族 | Uses titleless overlay presentation when overlay mode is active. | 已启用 |
| MiniTalk | `TranslateMiniTalk` | Overlay 家族 | 原生小型界面；更长的翻译文本仍需要谨慎的 native reflow | 已启用 |
| CutSceneSelectString | `TranslateCutSceneSelectString` | Overlay 家族 | 在 overlay 模式下，问题文本作为标题，选项作为正文 | 已启用 |
| Yes/No dialog | `TranslateYesNoScreen` | Native-tooltip 家族 | 已存在于配置模型和标签页实现中，但当前未在活动的 Overlay 标签流中暴露 | 已启用 |
| SelectOk dialog | `TranslateSelectOk` | Native-tooltip 家族 | 已存在于配置模型和标签页实现中，但当前未在活动的 Overlay 标签流中暴露 | 已启用 |
| SelectString dialog | `TranslateSelectString` | Native-tooltip 家族 | Uses structured plugin tooltips instead of overlay windows and supports native, tooltip-only, and swap presentation; prefers SelectString and falls back to SelectionDialogText. | 已启用 |
| SelectIconString dialog | `TranslateSelectIconString` | Native-tooltip 家族 | Keeps its own toggle and display mode and uses body-only structured tooltip presentation. | 已启用 |

## 任务与 Journal 界面

| 界面 | 配置开关 | 模式 | 说明 | 当前发布状态 |
| --- | --- | --- | --- | --- |
| Journal | `TranslateJournal` | Quest / native-window 家族 | 任务列表界面 | 已启用 |
| JournalDetail | `TranslateJournalDetail` | Quest / native-window 家族 | 正文布局密集；原生模式需要显式 block reflow | 已启用 |
| ToDoList | `TranslateToDoList` | Quest / native-window 家族 | 任务追踪 / 目标列表 | 已启用 |
| ToDo | `TranslateToDo` | Quest / native-window 家族 | Instanced/FATE objective tracker. | 已启用 |
| ScenarioTree | `TranslateScenarioTree` | Quest / native-window 家族 | 主线剧情追踪 | 已启用 |
| JournalAccept | `TranslateJournalAccept` | Quest / native-window 家族 | Uses QuestPlate when a safe quest id is available and QuestPopupText fallback for live popup capture. | 已启用 |
| JournalResult | `TranslateJournalResult` | Quest / native-window 家族 | Prefers QuestPlate canonical lookup and falls back to QuestPopupText while missing rows are translated live. | 已启用 |
| RecommendList | `TranslateRecommendList` | Quest / native-window 家族 | 推荐列表 | 已启用 |
| AreaMap | `TranslateAreaMap` | Quest / native-window 家族 | Quest text inside map-related quest UI; AreaMap and _NaviMap are string-array-backed. | 已启用 |

## Toast 界面

| 界面 | 配置开关 | 模式 | 说明 | 当前发布状态 |
| --- | --- | --- | --- | --- |
| WideText / Screen Info toast | `TranslateWideTextToast` | Overlay 家族 | 屏幕中央的大型信息提示 | 已启用 |
| Error toast | `TranslateErrorToast` | Overlay 家族 | 错误 / 失败通知 | 已启用 |
| Area toast | `TranslateAreaToast` | Overlay 家族 | 区域和地点通知 | 已启用 |
| Class / Job change toast | `TranslateClassChangeToast` | Overlay 家族 | 职业 / Job 变更提示 | 已启用 |
| Text gimmick hint | `TranslateTextGimmickHint` | Overlay 家族 | gimmick / 教程提示界面 | 已启用 |
| Quest toast | `TranslateQuestToast` | Overlay 家族 | 与任务相关的 toast 通知 | 已启用 |

## 游戏窗口界面

| 界面 | 配置开关 | 模式 | 说明 | 当前发布状态 |
| --- | --- | --- | --- | --- |
| Character window | `TranslateCharacterWindow` | Quest / native-window 家族 | DB-first 游戏窗口运行时 | 已启用 |
| Main Command | `TranslateMainCommandWindow` | Quest / native-window 家族 | DB-first 游戏窗口运行时 | 已启用 |
| Action Menu | `TranslateActionMenuWindow` | Quest / native-window 家族 | DB-first 游戏窗口运行时 | 已启用 |
| HUD windows | `TranslateHudWindow` | Quest / native-window 家族 | DB-first 游戏窗口运行时 | 已启用 |
| Operation Guide | `TranslateOperationGuideWindow` | Quest / native-window 家族 | DB-first 游戏窗口运行时 | 已启用 |
| Addon Context Menu Title | `TranslateAddonContextMenuTitle` | Quest / native-window 家族 | DB-first 游戏窗口运行时 | 已启用 |
| Context Menu | `TranslateContextMenu` | Native-tooltip 家族 | Dedicated DB-first row-chain runtime with row-local hover targets. | 已启用 |
| Tooltip addon | `TranslateTooltipAddon` | Native-tooltip 家族 | Dedicated DB-first Tooltip addon runtime; tooltip translation and swap use an anchored overlay on the live game tooltip. | 已启用 |
| Action / item detail tooltips | `TranslateTooltips` | Quest / native-window 家族 | 结构化 tooltip 翻译会在启动时被强制禁用，直到 `ActionDetail` / `ItemDetail` 稳定为止 | 已启用 |

## 世界与 NamePlate 界面

| 界面 | 配置开关 | 模式 | 说明 | 当前发布状态 |
| --- | --- | --- | --- | --- |
| NamePlates | `TranslateNamePlates` | Native / distance-aware 混合家族 | Standard languages keep the native backend; overlay-only languages keep the native nameplate original and render the translation through the distance-aware overlay backend. | 已启用 |

## 隐藏或暂时受限的界面

| 界面 | 配置开关 | 模式 | 说明 | 当前发布状态 |
| --- | --- | --- | --- | --- |
