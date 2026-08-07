<!--
  Copyright (c) lokinmodar. All rights reserved.
  Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
-->

# Matriz de suporte das superfícies de tradução

## Famílias de modos de tradução

| Família de modos | Modos |
| --- | --- |
| Família native-tooltip | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |
| Família overlay | `Native UI Translation`; `Overlay Translation Only`; `Native UI Translation With Original Overlay` |
| Família híbrida native / distance-aware | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |
| Família quest / native-window | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |

## Superfícies de diálogo e overlay

| Superfície | Toggle de configuração | Modos | Notas | Status da release atual |
| --- | --- | --- | --- | --- |
| Talk | `TranslateTalk` | Família overlay | Suporta nomes de NPC traduzidos por `TranslateTalkNpcNames` | Ativado |
| BattleTalk | `TranslateBattleTalk` | Família overlay | Suporta nomes de NPC traduzidos por `TranslateBattleTalkNpcNames` | Ativado |
| TalkSubtitle | `TranslateTalkSubtitle` | Família overlay | Uses titleless overlay presentation when overlay mode is active. | Ativado |
| MiniTalk | `TranslateMiniTalk` | Família overlay | Superfície nativa pequena; textos mais verbosos ainda exigem native reflow cuidadoso | Ativado |
| CutSceneSelectString | `TranslateCutSceneSelectString` | Família overlay | A pergunta vira o título e as opções viram o corpo no modo overlay | Ativado |
| Yes/No dialog | `TranslateYesNoScreen` | Família native-tooltip | Usa tooltips estruturadas do plugin no lugar do overlay e suporta aplicação nativa, tooltip-only e swap | Ativado |
| SelectOk dialog | `TranslateSelectOk` | Família native-tooltip | Usa tooltips estruturadas do plugin no lugar do overlay e suporta aplicação nativa, tooltip-only e swap | Ativado |
| SelectString dialog | `TranslateSelectString` | Família native-tooltip | Uses structured plugin tooltips instead of overlay windows and supports native, tooltip-only, and swap presentation; prefers SelectString and falls back to SelectionDialogText. | Ativado |
| SelectIconString dialog | `TranslateSelectIconString` | Família native-tooltip | Tem toggle e display mode próprios; usa tooltip estruturada body-only | Ativado |

## Superfícies de quest e journal

| Superfície | Toggle de configuração | Modos | Notas | Status da release atual |
| --- | --- | --- | --- | --- |
| Journal | `TranslateJournal` | Família quest / native-window | Superfície de lista de quests | Ativado |
| JournalDetail | `TranslateJournalDetail` | Família quest / native-window | Layout de corpo denso; o modo nativo exige block reflow explícito | Ativado |
| ToDoList | `TranslateToDoList` | Família quest / native-window | Rastreador de quest / lista de objetivos | Ativado |
| ToDo | `TranslateToDo` | Família quest / native-window | Rastreador de objetivos de instância/FATE | Ativado |
| ScenarioTree | `TranslateScenarioTree` | Família quest / native-window | Rastreador do cenário principal | Ativado |
| JournalAccept | `TranslateJournalAccept` | Família quest / native-window | Uses QuestPlate when a safe quest id is available and QuestPopupText fallback for live popup capture. | Ativado |
| JournalResult | `TranslateJournalResult` | Família quest / native-window | Prefers QuestPlate canonical lookup and falls back to QuestPopupText while missing rows are translated live. | Ativado |
| RecommendList | `TranslateRecommendList` | Família quest / native-window | Lista de recomendações | Ativado |
| AreaMap | `TranslateAreaMap` | Família quest / native-window | Quest text inside map-related quest UI; AreaMap and _NaviMap are string-array-backed. | Ativado |

## Superfícies de toast

| Superfície | Toggle de configuração | Modos | Notas | Status da release atual |
| --- | --- | --- | --- | --- |
| WideText / Screen Info toast | `TranslateWideTextToast` | Família overlay | Toast informativo grande no centro da tela | Ativado |
| Error toast | `TranslateErrorToast` | Família overlay | Notificações de erro ou falha | Ativado |
| Area toast | `TranslateAreaToast` | Família overlay | Notificações de área e localização | Ativado |
| Class / Job change toast | `TranslateClassChangeToast` | Família overlay | Anúncio de troca de class/job | Ativado |
| Text gimmick hint | `TranslateTextGimmickHint` | Família overlay | Superfície de dica de gimmick/tutorial | Ativado |
| Quest toast | `TranslateQuestToast` | Família overlay | Toast de notificação relacionado a quest | Ativado |

## Superfícies de janelas do jogo

| Superfície | Toggle de configuração | Modos | Notas | Status da release atual |
| --- | --- | --- | --- | --- |
| Character window | `TranslateCharacterWindow` | Família quest / native-window | Runtime DB-first de janelas do jogo | Ativado |
| Main Command | `TranslateMainCommandWindow` | Família quest / native-window | Runtime DB-first de janelas do jogo | Ativado |
| Action Menu | `TranslateActionMenuWindow` | Família quest / native-window | Runtime DB-first de janelas do jogo | Ativado |
| HUD windows | `TranslateHudWindow` | Família quest / native-window | Runtime DB-first de janelas do jogo | Ativado |
| Operation Guide | `TranslateOperationGuideWindow` | Família quest / native-window | Runtime DB-first de janelas do jogo | Ativado |
| Addon Context Menu Title | `TranslateAddonContextMenuTitle` | Família quest / native-window | Runtime DB-first de janelas do jogo | Ativado |
| Context Menu | `TranslateContextMenu` | Família native-tooltip | Runtime DB-first dedicado de cadeia de linhas com alvos de hover por linha | Ativado |
| Tooltip addon | `TranslateTooltipAddon` | Família native-tooltip | Dedicated DB-first Tooltip addon runtime; tooltip translation and swap use an anchored overlay on the live game tooltip. | Ativado |
| Action / item detail tooltips | `TranslateTooltips` | Família quest / native-window | Runtime DB-first de tooltip estruturada; o padrão é modo Plugin Tooltip, enquanto gravação nativa é opt-in e limitada a nodes seguros com texto puro | Ativado |

## Superfícies de mundo e NamePlate

| Superfície | Toggle de configuração | Modos | Notas | Status da release atual |
| --- | --- | --- | --- | --- |
| NamePlates | `TranslateNamePlates` | Família híbrida native / distance-aware | Standard languages keep the native backend; overlay-only languages keep the native nameplate original and render the translation through the distance-aware overlay backend. | Ativado |

## Superfícies ocultas ou temporariamente restritas

| Superfície | Toggle de configuração | Modos | Notas | Status da release atual |
| --- | --- | --- | --- | --- |
