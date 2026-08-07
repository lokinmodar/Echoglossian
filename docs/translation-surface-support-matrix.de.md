<!--
  Copyright (c) lokinmodar. All rights reserved.
  Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
-->

# Matrix der unterstützten Übersetzungsoberflächen

## Übersetzungsmodus-Familien

| Modusfamilie | Modi |
| --- | --- |
| Native-Tooltip-Familie | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |
| Overlay-Familie | `Native UI Translation`; `Overlay Translation Only`; `Native UI Translation With Original Overlay` |
| Native-/Distance-Aware-Hybridfamilie | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |
| Quest-/Native-Window-Familie | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |

## Dialog- und Overlay-Oberflächen

| Oberfläche | Konfigurations-Toggle | Modi | Hinweise | Status der aktuellen Release |
| --- | --- | --- | --- | --- |
| Talk | `TranslateTalk` | Overlay-Familie | Unterstützt übersetzte NPC-Namen über `TranslateTalkNpcNames` | Aktiviert |
| BattleTalk | `TranslateBattleTalk` | Overlay-Familie | Unterstützt übersetzte NPC-Namen über `TranslateBattleTalkNpcNames` | Aktiviert |
| TalkSubtitle | `TranslateTalkSubtitle` | Overlay-Familie | Uses titleless overlay presentation when overlay mode is active. | Aktiviert |
| MiniTalk | `TranslateMiniTalk` | Overlay-Familie | Kleine native Oberfläche; ausführlichere Texte benötigen weiterhin sorgfältiges natives Reflow | Aktiviert |
| CutSceneSelectString | `TranslateCutSceneSelectString` | Overlay-Familie | Die Frage wird im Overlay-Modus zum Titel und die Optionen zum Haupttext | Aktiviert |
| Yes/No dialog | `TranslateYesNoScreen` | Native-Tooltip-Familie | Im Konfigurationsmodell und in der Tab-Implementierung vorhanden, aber derzeit nicht im aktiven Overlay-Tab-Flow sichtbar | Aktiviert |
| SelectOk dialog | `TranslateSelectOk` | Native-Tooltip-Familie | Im Konfigurationsmodell und in der Tab-Implementierung vorhanden, aber derzeit nicht im aktiven Overlay-Tab-Flow sichtbar | Aktiviert |
| SelectString dialog | `TranslateSelectString` | Native-Tooltip-Familie | Uses structured plugin tooltips instead of overlay windows and supports native, tooltip-only, and swap presentation; prefers SelectString and falls back to SelectionDialogText. | Aktiviert |
| SelectIconString dialog | `TranslateSelectIconString` | Native-Tooltip-Familie | Keeps its own toggle and display mode and uses body-only structured tooltip presentation. | Aktiviert |

## Quest- und Journal-Oberflächen

| Oberfläche | Konfigurations-Toggle | Modi | Hinweise | Status der aktuellen Release |
| --- | --- | --- | --- | --- |
| Journal | `TranslateJournal` | Quest-/Native-Window-Familie | Questlisten-Oberfläche | Aktiviert |
| JournalDetail | `TranslateJournalDetail` | Quest-/Native-Window-Familie | Dichtes Body-Layout; der native Modus erfordert explizites Block-Reflow | Aktiviert |
| ToDoList | `TranslateToDoList` | Quest-/Native-Window-Familie | Quest-Tracker / Zielliste | Aktiviert |
| ToDo | `TranslateToDo` | Quest-/Native-Window-Familie | Instanced/FATE objective tracker. | Aktiviert |
| ScenarioTree | `TranslateScenarioTree` | Quest-/Native-Window-Familie | Hauptszenario-Tracker | Aktiviert |
| JournalAccept | `TranslateJournalAccept` | Quest-/Native-Window-Familie | Uses QuestPlate when a safe quest id is available and QuestPopupText fallback for live popup capture. | Aktiviert |
| JournalResult | `TranslateJournalResult` | Quest-/Native-Window-Familie | Prefers QuestPlate canonical lookup and falls back to QuestPopupText while missing rows are translated live. | Aktiviert |
| RecommendList | `TranslateRecommendList` | Quest-/Native-Window-Familie | Empfehlungsliste | Aktiviert |
| AreaMap | `TranslateAreaMap` | Quest-/Native-Window-Familie | Quest text inside map-related quest UI; AreaMap and _NaviMap are string-array-backed. | Aktiviert |

## Toast-Oberflächen

| Oberfläche | Konfigurations-Toggle | Modi | Hinweise | Status der aktuellen Release |
| --- | --- | --- | --- | --- |
| WideText / Screen Info toast | `TranslateWideTextToast` | Overlay-Familie | Große Informations-Toast in der Bildschirmmitte | Aktiviert |
| Error toast | `TranslateErrorToast` | Overlay-Familie | Fehler- und Störungsmeldungen | Aktiviert |
| Area toast | `TranslateAreaToast` | Overlay-Familie | Gebiets- und Ortsbenachrichtigungen | Aktiviert |
| Class / Job change toast | `TranslateClassChangeToast` | Overlay-Familie | Ankündigung eines Klassen-/Jobwechsels | Aktiviert |
| Text gimmick hint | `TranslateTextGimmickHint` | Overlay-Familie | Gimmick-/Tutorial-Hinweis | Aktiviert |
| Quest toast | `TranslateQuestToast` | Overlay-Familie | Quest-bezogene Toast-Benachrichtigung | Aktiviert |

## Spiel-Fenster-Oberflächen

| Oberfläche | Konfigurations-Toggle | Modi | Hinweise | Status der aktuellen Release |
| --- | --- | --- | --- | --- |
| Character window | `TranslateCharacterWindow` | Quest-/Native-Window-Familie | DB-first-Game-Window-Runtime | Aktiviert |
| Main Command | `TranslateMainCommandWindow` | Quest-/Native-Window-Familie | DB-first-Game-Window-Runtime | Aktiviert |
| Action Menu | `TranslateActionMenuWindow` | Quest-/Native-Window-Familie | DB-first-Game-Window-Runtime | Aktiviert |
| HUD windows | `TranslateHudWindow` | Quest-/Native-Window-Familie | DB-first-Game-Window-Runtime | Aktiviert |
| Operation Guide | `TranslateOperationGuideWindow` | Quest-/Native-Window-Familie | DB-first-Game-Window-Runtime | Aktiviert |
| Addon Context Menu Title | `TranslateAddonContextMenuTitle` | Quest-/Native-Window-Familie | DB-first-Game-Window-Runtime | Aktiviert |
| Context Menu | `TranslateContextMenu` | Native-Tooltip-Familie | Dedicated DB-first row-chain runtime with row-local hover targets. | Aktiviert |
| Tooltip addon | `TranslateTooltipAddon` | Native-Tooltip-Familie | Dedicated DB-first Tooltip addon runtime; tooltip translation and swap use an anchored overlay on the live game tooltip. | Aktiviert |
| Action / item detail tooltips | `TranslateTooltips` | Quest-/Native-Window-Familie | Strukturierte Tooltip-Übersetzung wird beim Start zwangsweise deaktiviert, solange `ActionDetail` / `ItemDetail` instabil bleiben | Aktiviert |

## Welt- und NamePlate-Oberflächen

| Oberfläche | Konfigurations-Toggle | Modi | Hinweise | Status der aktuellen Release |
| --- | --- | --- | --- | --- |
| NamePlates | `TranslateNamePlates` | Native-/Distance-Aware-Hybridfamilie | Standard languages keep the native backend; overlay-only languages keep the native nameplate original and render the translation through the distance-aware overlay backend. | Aktiviert |

## Versteckte oder vorübergehend eingeschränkte Oberflächen

| Oberfläche | Konfigurations-Toggle | Modi | Hinweise | Status der aktuellen Release |
| --- | --- | --- | --- | --- |
