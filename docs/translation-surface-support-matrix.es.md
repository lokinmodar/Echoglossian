<!--
  Copyright (c) lokinmodar. All rights reserved.
  Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
-->

# Matriz de compatibilidad de superficies de traducción

## Familias de modos de traducción

| Familia de modos | Modos |
| --- | --- |
| Familia native-tooltip | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |
| Familia overlay | `Native UI Translation`; `Overlay Translation Only`; `Native UI Translation With Original Overlay` |
| Familia híbrida native / distance-aware | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |
| Familia quest / native-window | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |

## Superficies de diálogo y overlay

| Superficie | Interruptor de configuración | Modos | Notas | Estado de la versión actual |
| --- | --- | --- | --- | --- |
| Talk | `TranslateTalk` | Familia overlay | Soporta nombres de NPC traducidos mediante `TranslateTalkNpcNames` | Activado |
| BattleTalk | `TranslateBattleTalk` | Familia overlay | Soporta nombres de NPC traducidos mediante `TranslateBattleTalkNpcNames` | Activado |
| TalkSubtitle | `TranslateTalkSubtitle` | Familia overlay | Uses titleless overlay presentation when overlay mode is active. | Activado |
| MiniTalk | `TranslateMiniTalk` | Familia overlay | Superficie nativa pequeña; el texto más verboso todavía requiere native reflow cuidadoso | Activado |
| CutSceneSelectString | `TranslateCutSceneSelectString` | Familia overlay | La pregunta se convierte en el título y las opciones en el cuerpo en modo overlay | Activado |
| Yes/No dialog | `TranslateYesNoScreen` | Familia native-tooltip | Presente en el modelo de configuración y en la implementación de la pestaña, pero no expuesto actualmente en el flujo activo de la pestaña Overlay | Activado |
| SelectOk dialog | `TranslateSelectOk` | Familia native-tooltip | Presente en el modelo de configuración y en la implementación de la pestaña, pero no expuesto actualmente en el flujo activo de la pestaña Overlay | Activado |
| SelectString dialog | `TranslateSelectString` | Familia native-tooltip | Uses structured plugin tooltips instead of overlay windows and supports native, tooltip-only, and swap presentation; prefers SelectString and falls back to SelectionDialogText. | Activado |
| SelectIconString dialog | `TranslateSelectIconString` | Familia native-tooltip | Keeps its own toggle and display mode and uses body-only structured tooltip presentation. | Activado |

## Superficies de quest y journal

| Superficie | Interruptor de configuración | Modos | Notas | Estado de la versión actual |
| --- | --- | --- | --- | --- |
| Journal | `TranslateJournal` | Familia quest / native-window | Superficie de lista de quests | Activado |
| JournalDetail | `TranslateJournalDetail` | Familia quest / native-window | Diseño de cuerpo denso; el modo nativo requiere block reflow explícito | Activado |
| ToDoList | `TranslateToDoList` | Familia quest / native-window | Seguimiento de quest / lista de objetivos | Activado |
| ToDo | `TranslateToDo` | Familia quest / native-window | Instanced/FATE objective tracker. | Activado |
| ScenarioTree | `TranslateScenarioTree` | Familia quest / native-window | Seguimiento del escenario principal | Activado |
| JournalAccept | `TranslateJournalAccept` | Familia quest / native-window | Uses QuestPlate when a safe quest id is available and QuestPopupText fallback for live popup capture. | Activado |
| JournalResult | `TranslateJournalResult` | Familia quest / native-window | Prefers QuestPlate canonical lookup and falls back to QuestPopupText while missing rows are translated live. | Activado |
| RecommendList | `TranslateRecommendList` | Familia quest / native-window | Lista de recomendaciones | Activado |
| AreaMap | `TranslateAreaMap` | Familia quest / native-window | Quest text inside map-related quest UI; AreaMap and _NaviMap are string-array-backed. | Activado |

## Superficies de toast

| Superficie | Interruptor de configuración | Modos | Notas | Estado de la versión actual |
| --- | --- | --- | --- | --- |
| WideText / Screen Info toast | `TranslateWideTextToast` | Familia overlay | Toast informativo grande en el centro de la pantalla | Activado |
| Error toast | `TranslateErrorToast` | Familia overlay | Notificaciones de error o fallo | Activado |
| Area toast | `TranslateAreaToast` | Familia overlay | Notificaciones de área y ubicación | Activado |
| Class / Job change toast | `TranslateClassChangeToast` | Familia overlay | Aviso de cambio de class/job | Activado |
| Text gimmick hint | `TranslateTextGimmickHint` | Familia overlay | Superficie de pista de gimmick/tutorial | Activado |
| Quest toast | `TranslateQuestToast` | Familia overlay | Notificación toast relacionada con quest | Activado |

## Superficies de ventanas del juego

| Superficie | Interruptor de configuración | Modos | Notas | Estado de la versión actual |
| --- | --- | --- | --- | --- |
| Character window | `TranslateCharacterWindow` | Familia quest / native-window | Runtime DB-first de ventanas del juego | Activado |
| Main Command | `TranslateMainCommandWindow` | Familia quest / native-window | Runtime DB-first de ventanas del juego | Activado |
| Action Menu | `TranslateActionMenuWindow` | Familia quest / native-window | Runtime DB-first de ventanas del juego | Activado |
| HUD windows | `TranslateHudWindow` | Familia quest / native-window | Runtime DB-first de ventanas del juego | Activado |
| Operation Guide | `TranslateOperationGuideWindow` | Familia quest / native-window | Runtime DB-first de ventanas del juego | Activado |
| Addon Context Menu Title | `TranslateAddonContextMenuTitle` | Familia quest / native-window | Runtime DB-first de ventanas del juego | Activado |
| Context Menu | `TranslateContextMenu` | Familia native-tooltip | Dedicated DB-first row-chain runtime with row-local hover targets. | Activado |
| Tooltip addon | `TranslateTooltipAddon` | Familia native-tooltip | Dedicated DB-first Tooltip addon runtime; tooltip translation and swap use an anchored overlay on the live game tooltip. | Activado |
| Action / item detail tooltips | `TranslateTooltips` | Familia quest / native-window | La traducción estructurada de tooltips se desactiva a la fuerza al iniciar mientras `ActionDetail` / `ItemDetail` sigan inestables | Activado |

## Superficies de mundo y NamePlate

| Superficie | Interruptor de configuración | Modos | Notas | Estado de la versión actual |
| --- | --- | --- | --- | --- |
| NamePlates | `TranslateNamePlates` | Familia híbrida native / distance-aware | Standard languages keep the native backend; overlay-only languages keep the native nameplate original and render the translation through the distance-aware overlay backend. | Activado |

## Superficies ocultas o temporalmente restringidas

| Superficie | Interruptor de configuración | Modos | Notas | Estado de la versión actual |
| --- | --- | --- | --- | --- |
