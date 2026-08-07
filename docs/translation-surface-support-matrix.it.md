<!--
  Copyright (c) lokinmodar. All rights reserved.
  Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
-->

# Matrice di supporto delle superfici di traduzione

## Famiglie di modalità di traduzione

| Famiglia di modalità | Modalità |
| --- | --- |
| Famiglia native-tooltip | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |
| Famiglia overlay | `Native UI Translation`; `Overlay Translation Only`; `Native UI Translation With Original Overlay` |
| Famiglia ibrida native / distance-aware | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |
| Famiglia quest / native-window | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |

## Superfici di dialogo e overlay

| Superficie | Interruttore di configurazione | Modalità | Note | Stato della release corrente |
| --- | --- | --- | --- | --- |
| Talk | `TranslateTalk` | Famiglia overlay | Supporta nomi NPC tradotti tramite `TranslateTalkNpcNames` | Attivato |
| BattleTalk | `TranslateBattleTalk` | Famiglia overlay | Supporta nomi NPC tradotti tramite `TranslateBattleTalkNpcNames` | Attivato |
| TalkSubtitle | `TranslateTalkSubtitle` | Famiglia overlay | Uses titleless overlay presentation when overlay mode is active. | Attivato |
| MiniTalk | `TranslateMiniTalk` | Famiglia overlay | Piccola superficie nativa; testi più verbosi richiedono ancora un native reflow accurato | Attivato |
| CutSceneSelectString | `TranslateCutSceneSelectString` | Famiglia overlay | La domanda diventa il titolo e le opzioni diventano il corpo in modalità overlay | Attivato |
| Yes/No dialog | `TranslateYesNoScreen` | Famiglia native-tooltip | Presente nel modello di configurazione e nell’implementazione della scheda, ma non esposto attualmente nel flusso attivo della scheda Overlay | Attivato |
| SelectOk dialog | `TranslateSelectOk` | Famiglia native-tooltip | Presente nel modello di configurazione e nell’implementazione della scheda, ma non esposto attualmente nel flusso attivo della scheda Overlay | Attivato |
| SelectString dialog | `TranslateSelectString` | Famiglia native-tooltip | Uses structured plugin tooltips instead of overlay windows and supports native, tooltip-only, and swap presentation; prefers SelectString and falls back to SelectionDialogText. | Attivato |
| SelectIconString dialog | `TranslateSelectIconString` | Famiglia native-tooltip | Keeps its own toggle and display mode and uses body-only structured tooltip presentation. | Attivato |

## Superfici quest e journal

| Superficie | Interruttore di configurazione | Modalità | Note | Stato della release corrente |
| --- | --- | --- | --- | --- |
| Journal | `TranslateJournal` | Famiglia quest / native-window | Superficie lista quest | Attivato |
| JournalDetail | `TranslateJournalDetail` | Famiglia quest / native-window | Layout del corpo denso; la modalità nativa richiede block reflow esplicito | Attivato |
| ToDoList | `TranslateToDoList` | Famiglia quest / native-window | Tracker quest / lista obiettivi | Attivato |
| ToDo | `TranslateToDo` | Famiglia quest / native-window | Instanced/FATE objective tracker. | Attivato |
| ScenarioTree | `TranslateScenarioTree` | Famiglia quest / native-window | Tracker dello scenario principale | Attivato |
| JournalAccept | `TranslateJournalAccept` | Famiglia quest / native-window | Uses QuestPlate when a safe quest id is available and QuestPopupText fallback for live popup capture. | Attivato |
| JournalResult | `TranslateJournalResult` | Famiglia quest / native-window | Prefers QuestPlate canonical lookup and falls back to QuestPopupText while missing rows are translated live. | Attivato |
| RecommendList | `TranslateRecommendList` | Famiglia quest / native-window | Lista raccomandazioni | Attivato |
| AreaMap | `TranslateAreaMap` | Famiglia quest / native-window | Quest text inside map-related quest UI; AreaMap and _NaviMap are string-array-backed. | Attivato |

## Superfici toast

| Superficie | Interruttore di configurazione | Modalità | Note | Stato della release corrente |
| --- | --- | --- | --- | --- |
| WideText / Screen Info toast | `TranslateWideTextToast` | Famiglia overlay | Grande toast informativo al centro dello schermo | Attivato |
| Error toast | `TranslateErrorToast` | Famiglia overlay | Notifiche di errore o fallimento | Attivato |
| Area toast | `TranslateAreaToast` | Famiglia overlay | Notifiche di area e posizione | Attivato |
| Class / Job change toast | `TranslateClassChangeToast` | Famiglia overlay | Annuncio di cambio class/job | Attivato |
| Text gimmick hint | `TranslateTextGimmickHint` | Famiglia overlay | Superficie hint per gimmick/tutorial | Attivato |
| Quest toast | `TranslateQuestToast` | Famiglia overlay | Notifica toast relativa alle quest | Attivato |

## Superfici delle finestre di gioco

| Superficie | Interruttore di configurazione | Modalità | Note | Stato della release corrente |
| --- | --- | --- | --- | --- |
| Character window | `TranslateCharacterWindow` | Famiglia quest / native-window | Runtime DB-first delle finestre di gioco | Attivato |
| Main Command | `TranslateMainCommandWindow` | Famiglia quest / native-window | Runtime DB-first delle finestre di gioco | Attivato |
| Action Menu | `TranslateActionMenuWindow` | Famiglia quest / native-window | Runtime DB-first delle finestre di gioco | Attivato |
| HUD windows | `TranslateHudWindow` | Famiglia quest / native-window | Runtime DB-first delle finestre di gioco | Attivato |
| Operation Guide | `TranslateOperationGuideWindow` | Famiglia quest / native-window | Runtime DB-first delle finestre di gioco | Attivato |
| Addon Context Menu Title | `TranslateAddonContextMenuTitle` | Famiglia quest / native-window | Runtime DB-first delle finestre di gioco | Attivato |
| Context Menu | `TranslateContextMenu` | Famiglia native-tooltip | Dedicated DB-first row-chain runtime with row-local hover targets. | Attivato |
| Tooltip addon | `TranslateTooltipAddon` | Famiglia native-tooltip | Dedicated DB-first Tooltip addon runtime; tooltip translation and swap use an anchored overlay on the live game tooltip. | Attivato |
| Action / item detail tooltips | `TranslateTooltips` | Famiglia quest / native-window | La traduzione strutturata dei tooltip viene disattivata forzatamente all’avvio finché `ActionDetail` / `ItemDetail` restano instabili | Attivato |

## Superfici mondo e NamePlate

| Superficie | Interruttore di configurazione | Modalità | Note | Stato della release corrente |
| --- | --- | --- | --- | --- |
| NamePlates | `TranslateNamePlates` | Famiglia ibrida native / distance-aware | Standard languages keep the native backend; overlay-only languages keep the native nameplate original and render the translation through the distance-aware overlay backend. | Attivato |

## Superfici nascoste o temporaneamente limitate

| Superficie | Interruttore di configurazione | Modalità | Note | Stato della release corrente |
| --- | --- | --- | --- | --- |
