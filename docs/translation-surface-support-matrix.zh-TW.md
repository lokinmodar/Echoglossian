<!--
  Copyright (c) lokinmodar. All rights reserved.
  Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
-->

# 翻譯介面支援矩陣

## 翻譯模式家族

| 模式家族 | 模式 |
| --- | --- |
| Native-tooltip 家族 | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |
| Overlay 家族 | `Native UI Translation`; `Overlay Translation Only`; `Native UI Translation With Original Overlay` |
| Native / distance-aware 混合家族 | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |
| Quest / native-window 家族 | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |

## 對話與 Overlay 介面

| 介面 | 設定開關 | 模式 | 說明 | 目前發行狀態 |
| --- | --- | --- | --- | --- |
| Talk | `TranslateTalk` | Overlay 家族 | 透過 `TranslateTalkNpcNames` 支援翻譯 NPC 名稱 | 已啟用 |
| BattleTalk | `TranslateBattleTalk` | Overlay 家族 | 透過 `TranslateBattleTalkNpcNames` 支援翻譯 NPC 名稱 | 已啟用 |
| TalkSubtitle | `TranslateTalkSubtitle` | Overlay 家族 | Uses titleless overlay presentation when overlay mode is active. | 已啟用 |
| MiniTalk | `TranslateMiniTalk` | Overlay 家族 | 小型原生介面；較長的翻譯文字仍需要謹慎的 native reflow | 已啟用 |
| CutSceneSelectString | `TranslateCutSceneSelectString` | Overlay 家族 | 在 overlay 模式下，問題文字成為標題，選項成為主體內容 | 已啟用 |
| Yes/No dialog | `TranslateYesNoScreen` | Native-tooltip 家族 | 已存在於設定模型與分頁實作中，但目前未在啟用中的 Overlay 分頁流程中顯示 | 已啟用 |
| SelectOk dialog | `TranslateSelectOk` | Native-tooltip 家族 | 已存在於設定模型與分頁實作中，但目前未在啟用中的 Overlay 分頁流程中顯示 | 已啟用 |
| SelectString dialog | `TranslateSelectString` | Native-tooltip 家族 | Uses structured plugin tooltips instead of overlay windows and supports native, tooltip-only, and swap presentation; prefers SelectString and falls back to SelectionDialogText. | 已啟用 |
| SelectIconString dialog | `TranslateSelectIconString` | Native-tooltip 家族 | Keeps its own toggle and display mode and uses body-only structured tooltip presentation. | 已啟用 |

## 任務與 Journal 介面

| 介面 | 設定開關 | 模式 | 說明 | 目前發行狀態 |
| --- | --- | --- | --- | --- |
| Journal | `TranslateJournal` | Quest / native-window 家族 | 任務列表介面 | 已啟用 |
| JournalDetail | `TranslateJournalDetail` | Quest / native-window 家族 | 主體版面密集；原生模式需要明確的 block reflow | 已啟用 |
| ToDoList | `TranslateToDoList` | Quest / native-window 家族 | 任務追蹤 / 目標清單 | 已啟用 |
| ToDo | `TranslateToDo` | Quest / native-window 家族 | Instanced/FATE objective tracker. | 已啟用 |
| ScenarioTree | `TranslateScenarioTree` | Quest / native-window 家族 | 主線劇情追蹤 | 已啟用 |
| JournalAccept | `TranslateJournalAccept` | Quest / native-window 家族 | Uses QuestPlate when a safe quest id is available and QuestPopupText fallback for live popup capture. | 已啟用 |
| JournalResult | `TranslateJournalResult` | Quest / native-window 家族 | Prefers QuestPlate canonical lookup and falls back to QuestPopupText while missing rows are translated live. | 已啟用 |
| RecommendList | `TranslateRecommendList` | Quest / native-window 家族 | 推薦清單 | 已啟用 |
| AreaMap | `TranslateAreaMap` | Quest / native-window 家族 | Quest text inside map-related quest UI; AreaMap and _NaviMap are string-array-backed. | 已啟用 |

## Toast 介面

| 介面 | 設定開關 | 模式 | 說明 | 目前發行狀態 |
| --- | --- | --- | --- | --- |
| WideText / Screen Info toast | `TranslateWideTextToast` | Overlay 家族 | 螢幕中央的大型資訊提示 | 已啟用 |
| Error toast | `TranslateErrorToast` | Overlay 家族 | 錯誤 / 失敗通知 | 已啟用 |
| Area toast | `TranslateAreaToast` | Overlay 家族 | 區域與地點通知 | 已啟用 |
| Class / Job change toast | `TranslateClassChangeToast` | Overlay 家族 | Class / Job 變更提示 | 已啟用 |
| Text gimmick hint | `TranslateTextGimmickHint` | Overlay 家族 | gimmick / 教學提示介面 | 已啟用 |
| Quest toast | `TranslateQuestToast` | Overlay 家族 | 與任務相關的 toast 通知 | 已啟用 |

## 遊戲視窗介面

| 介面 | 設定開關 | 模式 | 說明 | 目前發行狀態 |
| --- | --- | --- | --- | --- |
| Character window | `TranslateCharacterWindow` | Quest / native-window 家族 | DB-first 遊戲視窗執行階段 | 已啟用 |
| Main Command | `TranslateMainCommandWindow` | Quest / native-window 家族 | DB-first 遊戲視窗執行階段 | 已啟用 |
| Action Menu | `TranslateActionMenuWindow` | Quest / native-window 家族 | DB-first 遊戲視窗執行階段 | 已啟用 |
| HUD windows | `TranslateHudWindow` | Quest / native-window 家族 | DB-first 遊戲視窗執行階段 | 已啟用 |
| Operation Guide | `TranslateOperationGuideWindow` | Quest / native-window 家族 | DB-first 遊戲視窗執行階段 | 已啟用 |
| Addon Context Menu Title | `TranslateAddonContextMenuTitle` | Quest / native-window 家族 | DB-first 遊戲視窗執行階段 | 已啟用 |
| Context Menu | `TranslateContextMenu` | Native-tooltip 家族 | Dedicated DB-first row-chain runtime with row-local hover targets. | 已啟用 |
| Tooltip addon | `TranslateTooltipAddon` | Native-tooltip 家族 | Dedicated DB-first Tooltip addon runtime; tooltip translation and swap use an anchored overlay on the live game tooltip. | 已啟用 |
| Action / item detail tooltips | `TranslateTooltips` | Quest / native-window 家族 | 結構化 tooltip 翻譯會在啟動時被強制停用，直到 `ActionDetail` / `ItemDetail` 穩定為止 | 已啟用 |

## 世界與 NamePlate 介面

| 介面 | 設定開關 | 模式 | 說明 | 目前發行狀態 |
| --- | --- | --- | --- | --- |
| NamePlates | `TranslateNamePlates` | Native / distance-aware 混合家族 | Standard languages keep the native backend; overlay-only languages keep the native nameplate original and render the translation through the distance-aware overlay backend. | 已啟用 |

## 隱藏或暫時受限的介面

| 介面 | 設定開關 | 模式 | 說明 | 目前發行狀態 |
| --- | --- | --- | --- | --- |
