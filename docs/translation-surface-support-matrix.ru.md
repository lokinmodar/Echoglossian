<!--
  Copyright (c) lokinmodar. All rights reserved.
  Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
-->

# Матрица поддержки поверхностей перевода

## Семейства режимов перевода

| Семейство режимов | Режимы |
| --- | --- |
| Семейство native-tooltip | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |
| Семейство overlay | `Native UI Translation`; `Overlay Translation Only`; `Native UI Translation With Original Overlay` |
| Гибридное семейство native / distance-aware | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |
| Семейство quest / native-window | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |

## Диалоговые и overlay-поверхности

| Поверхность | Переключатель конфигурации | Режимы | Заметки | Статус текущего релиза |
| --- | --- | --- | --- | --- |
| Talk | `TranslateTalk` | Семейство overlay | Поддерживает перевод имён NPC через `TranslateTalkNpcNames` | Включено |
| BattleTalk | `TranslateBattleTalk` | Семейство overlay | Поддерживает перевод имён NPC через `TranslateBattleTalkNpcNames` | Включено |
| TalkSubtitle | `TranslateTalkSubtitle` | Семейство overlay | Uses titleless overlay presentation when overlay mode is active. | Включено |
| MiniTalk | `TranslateMiniTalk` | Семейство overlay | Небольшая native-поверхность; более многословный текст всё ещё требует аккуратного native reflow | Включено |
| CutSceneSelectString | `TranslateCutSceneSelectString` | Семейство overlay | В overlay-режиме вопрос становится заголовком, а варианты ответа становятся основным текстом | Включено |
| Yes/No dialog | `TranslateYesNoScreen` | Семейство native-tooltip | Присутствует в модели конфигурации и реализации вкладки, но сейчас не отображается в активном потоке вкладки Overlay | Включено |
| SelectOk dialog | `TranslateSelectOk` | Семейство native-tooltip | Присутствует в модели конфигурации и реализации вкладки, но сейчас не отображается в активном потоке вкладки Overlay | Включено |
| SelectString dialog | `TranslateSelectString` | Семейство native-tooltip | Uses structured plugin tooltips instead of overlay windows and supports native, tooltip-only, and swap presentation; prefers SelectString and falls back to SelectionDialogText. | Включено |
| SelectIconString dialog | `TranslateSelectIconString` | Семейство native-tooltip | Keeps its own toggle and display mode and uses body-only structured tooltip presentation. | Включено |

## Поверхности quest и journal

| Поверхность | Переключатель конфигурации | Режимы | Заметки | Статус текущего релиза |
| --- | --- | --- | --- | --- |
| Journal | `TranslateJournal` | Семейство quest / native-window | Поверхность списка quest | Включено |
| JournalDetail | `TranslateJournalDetail` | Семейство quest / native-window | Плотная компоновка основного блока; native-режим требует явного block reflow | Включено |
| ToDoList | `TranslateToDoList` | Семейство quest / native-window | Трекер quest / список целей | Включено |
| ToDo | `TranslateToDo` | Семейство quest / native-window | Instanced/FATE objective tracker. | Включено |
| ScenarioTree | `TranslateScenarioTree` | Семейство quest / native-window | Трекер основного сценария | Включено |
| JournalAccept | `TranslateJournalAccept` | Семейство quest / native-window | Uses QuestPlate when a safe quest id is available and QuestPopupText fallback for live popup capture. | Включено |
| JournalResult | `TranslateJournalResult` | Семейство quest / native-window | Prefers QuestPlate canonical lookup and falls back to QuestPopupText while missing rows are translated live. | Включено |
| RecommendList | `TranslateRecommendList` | Семейство quest / native-window | Список рекомендаций | Включено |
| AreaMap | `TranslateAreaMap` | Семейство quest / native-window | Quest text inside map-related quest UI; AreaMap and _NaviMap are string-array-backed. | Включено |

## Toast-поверхности

| Поверхность | Переключатель конфигурации | Режимы | Заметки | Статус текущего релиза |
| --- | --- | --- | --- | --- |
| WideText / Screen Info toast | `TranslateWideTextToast` | Семейство overlay | Большое информационное уведомление в центре экрана | Включено |
| Error toast | `TranslateErrorToast` | Семейство overlay | Уведомления об ошибках и сбоях | Включено |
| Area toast | `TranslateAreaToast` | Семейство overlay | Уведомления об области и местоположении | Включено |
| Class / Job change toast | `TranslateClassChangeToast` | Семейство overlay | Сообщение о смене class/job | Включено |
| Text gimmick hint | `TranslateTextGimmickHint` | Семейство overlay | Поверхность подсказок gimmick/tutorial | Включено |
| Quest toast | `TranslateQuestToast` | Семейство overlay | Toast-уведомление, связанное с quest | Включено |

## Поверхности игровых окон

| Поверхность | Переключатель конфигурации | Режимы | Заметки | Статус текущего релиза |
| --- | --- | --- | --- | --- |
| Character window | `TranslateCharacterWindow` | Семейство quest / native-window | DB-first runtime игровых окон | Включено |
| Main Command | `TranslateMainCommandWindow` | Семейство quest / native-window | DB-first runtime игровых окон | Включено |
| Action Menu | `TranslateActionMenuWindow` | Семейство quest / native-window | DB-first runtime игровых окон | Включено |
| HUD windows | `TranslateHudWindow` | Семейство quest / native-window | DB-first runtime игровых окон | Включено |
| Operation Guide | `TranslateOperationGuideWindow` | Семейство quest / native-window | DB-first runtime игровых окон | Включено |
| Addon Context Menu Title | `TranslateAddonContextMenuTitle` | Семейство quest / native-window | DB-first runtime игровых окон | Включено |
| Context Menu | `TranslateContextMenu` | Семейство native-tooltip | Dedicated DB-first row-chain runtime with row-local hover targets. | Включено |
| Tooltip addon | `TranslateTooltipAddon` | Семейство native-tooltip | Dedicated DB-first Tooltip addon runtime; tooltip translation and swap use an anchored overlay on the live game tooltip. | Включено |
| Action / item detail tooltips | `TranslateTooltips` | Семейство quest / native-window | Структурированный перевод tooltip принудительно отключается при запуске, пока `ActionDetail` / `ItemDetail` остаются нестабильными | Включено |

## Поверхности мира и NamePlate

| Поверхность | Переключатель конфигурации | Режимы | Заметки | Статус текущего релиза |
| --- | --- | --- | --- | --- |
| NamePlates | `TranslateNamePlates` | Гибридное семейство native / distance-aware | Standard languages keep the native backend; overlay-only languages keep the native nameplate original and render the translation through the distance-aware overlay backend. | Включено |

## Скрытые или временно ограниченные поверхности

| Поверхность | Переключатель конфигурации | Режимы | Заметки | Статус текущего релиза |
| --- | --- | --- | --- | --- |
