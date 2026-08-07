<!--
  Copyright (c) lokinmodar. All rights reserved.
  Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
-->

# Oversigt over understøttede oversættelsesflader

## Familier af oversættelsestilstande

| Tilstandsfamilie | Tilstande |
| --- | --- |
| Native-tooltip-familie | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |
| Overlay-familie | `Native UI Translation`; `Overlay Translation Only`; `Native UI Translation With Original Overlay` |
| Native-/distance-aware-hybridfamilie | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |
| Quest-/native-window-familie | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |

## Dialog- og overlayflader

| Flade | Konfig-toggle | Tilstande | Bemærkninger | Status i nuværende release |
| --- | --- | --- | --- | --- |
| Talk | `TranslateTalk` | Overlay-familie | Understøtter oversatte NPC-navne via `TranslateTalkNpcNames` | Aktiveret |
| BattleTalk | `TranslateBattleTalk` | Overlay-familie | Understøtter oversatte NPC-navne via `TranslateBattleTalkNpcNames` | Aktiveret |
| TalkSubtitle | `TranslateTalkSubtitle` | Overlay-familie | Uses titleless overlay presentation when overlay mode is active. | Aktiveret |
| MiniTalk | `TranslateMiniTalk` | Overlay-familie | Lille native flade; ordrige tekster kræver stadig omhyggelig native reflow | Aktiveret |
| CutSceneSelectString | `TranslateCutSceneSelectString` | Overlay-familie | Spørgsmålet bliver titel, og valgmulighederne bliver brødteksten i overlay-tilstand | Aktiveret |
| Yes/No dialog | `TranslateYesNoScreen` | Native-tooltip-familie | Findes i konfigurationsmodellen og tab-implementeringen, men er ikke eksponeret i det aktive Overlay-tab-flow | Aktiveret |
| SelectOk dialog | `TranslateSelectOk` | Native-tooltip-familie | Findes i konfigurationsmodellen og tab-implementeringen, men er ikke eksponeret i det aktive Overlay-tab-flow | Aktiveret |
| SelectString dialog | `TranslateSelectString` | Native-tooltip-familie | Uses structured plugin tooltips instead of overlay windows and supports native, tooltip-only, and swap presentation; prefers SelectString and falls back to SelectionDialogText. | Aktiveret |
| SelectIconString dialog | `TranslateSelectIconString` | Native-tooltip-familie | Keeps its own toggle and display mode and uses body-only structured tooltip presentation. | Aktiveret |

## Quest- og journalflader

| Flade | Konfig-toggle | Tilstande | Bemærkninger | Status i nuværende release |
| --- | --- | --- | --- | --- |
| Journal | `TranslateJournal` | Quest-/native-window-familie | Questliste | Aktiveret |
| JournalDetail | `TranslateJournalDetail` | Quest-/native-window-familie | Tæt indholdslayout; native tilstand kræver eksplicit block reflow | Aktiveret |
| ToDoList | `TranslateToDoList` | Quest-/native-window-familie | Quest-tracker / målliste | Aktiveret |
| ToDo | `TranslateToDo` | Quest-/native-window-familie | Instanced/FATE objective tracker. | Aktiveret |
| ScenarioTree | `TranslateScenarioTree` | Quest-/native-window-familie | Hovedscenarie-tracker | Aktiveret |
| JournalAccept | `TranslateJournalAccept` | Quest-/native-window-familie | Uses QuestPlate when a safe quest id is available and QuestPopupText fallback for live popup capture. | Aktiveret |
| JournalResult | `TranslateJournalResult` | Quest-/native-window-familie | Prefers QuestPlate canonical lookup and falls back to QuestPopupText while missing rows are translated live. | Aktiveret |
| RecommendList | `TranslateRecommendList` | Quest-/native-window-familie | Anbefalingsliste | Aktiveret |
| AreaMap | `TranslateAreaMap` | Quest-/native-window-familie | Quest text inside map-related quest UI; AreaMap and _NaviMap are string-array-backed. | Aktiveret |

## Toast-flader

| Flade | Konfig-toggle | Tilstande | Bemærkninger | Status i nuværende release |
| --- | --- | --- | --- | --- |
| WideText / Screen Info toast | `TranslateWideTextToast` | Overlay-familie | Stor informations-toast midt på skærmen | Aktiveret |
| Error toast | `TranslateErrorToast` | Overlay-familie | Fejl- og advarselsnotifikationer | Aktiveret |
| Area toast | `TranslateAreaToast` | Overlay-familie | Område- og lokationsnotifikationer | Aktiveret |
| Class / Job change toast | `TranslateClassChangeToast` | Overlay-familie | Meddelelse om class/job-skift | Aktiveret |
| Text gimmick hint | `TranslateTextGimmickHint` | Overlay-familie | Gimmick-/tutorial-hint | Aktiveret |
| Quest toast | `TranslateQuestToast` | Overlay-familie | Quest-relateret toast-notifikation | Aktiveret |

## Spilvinduesflader

| Flade | Konfig-toggle | Tilstande | Bemærkninger | Status i nuværende release |
| --- | --- | --- | --- | --- |
| Character window | `TranslateCharacterWindow` | Quest-/native-window-familie | DB-first game-window-runtime | Aktiveret |
| Main Command | `TranslateMainCommandWindow` | Quest-/native-window-familie | DB-first game-window-runtime | Aktiveret |
| Action Menu | `TranslateActionMenuWindow` | Quest-/native-window-familie | DB-first game-window-runtime | Aktiveret |
| HUD windows | `TranslateHudWindow` | Quest-/native-window-familie | DB-first game-window-runtime | Aktiveret |
| Operation Guide | `TranslateOperationGuideWindow` | Quest-/native-window-familie | DB-first game-window-runtime | Aktiveret |
| Addon Context Menu Title | `TranslateAddonContextMenuTitle` | Quest-/native-window-familie | DB-first game-window-runtime | Aktiveret |
| Context Menu | `TranslateContextMenu` | Native-tooltip-familie | Dedicated DB-first row-chain runtime with row-local hover targets. | Aktiveret |
| Tooltip addon | `TranslateTooltipAddon` | Native-tooltip-familie | Dedicated DB-first Tooltip addon runtime; tooltip translation and swap use an anchored overlay on the live game tooltip. | Aktiveret |
| Action / item detail tooltips | `TranslateTooltips` | Quest-/native-window-familie | Struktureret tooltip-oversættelse deaktiveres tvunget ved opstart, mens `ActionDetail` / `ItemDetail` stadig er ustabile | Aktiveret |

## Verdens- og NamePlate-flader

| Flade | Konfig-toggle | Tilstande | Bemærkninger | Status i nuværende release |
| --- | --- | --- | --- | --- |
| NamePlates | `TranslateNamePlates` | Native-/distance-aware-hybridfamilie | Standard languages keep the native backend; overlay-only languages keep the native nameplate original and render the translation through the distance-aware overlay backend. | Aktiveret |

## Skjulte eller midlertidigt begrænsede flader

| Flade | Konfig-toggle | Tilstande | Bemærkninger | Status i nuværende release |
| --- | --- | --- | --- | --- |
