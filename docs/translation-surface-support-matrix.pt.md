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
| Talk | `TranslateTalk` | Família overlay | Suporta nomes de NPC traduzidos através de `TranslateTalkNpcNames` | Ativado |
| BattleTalk | `TranslateBattleTalk` | Família overlay | Suporta nomes de NPC traduzidos através de `TranslateBattleTalkNpcNames` | Ativado |
| TalkSubtitle | `TranslateTalkSubtitle` | Família overlay | Uses titleless overlay presentation when overlay mode is active. | Ativado |
| MiniTalk | `TranslateMiniTalk` | Família overlay | Pequena superfície nativa; textos mais verbosos ainda exigem native reflow cuidadoso | Ativado |
| CutSceneSelectString | `TranslateCutSceneSelectString` | Família overlay | A pergunta torna-se o título e as opções tornam-se o corpo no modo overlay | Ativado |
| Yes/No dialog | `TranslateYesNoScreen` | Família native-tooltip | Presente no modelo de configuração e na implementação da tab, mas não está atualmente exposto no fluxo ativo da tab Overlay | Ativado |
| SelectOk dialog | `TranslateSelectOk` | Família native-tooltip | Presente no modelo de configuração e na implementação da tab, mas não está atualmente exposto no fluxo ativo da tab Overlay | Ativado |
| SelectString dialog | `TranslateSelectString` | Família native-tooltip | Uses structured plugin tooltips instead of overlay windows and supports native, tooltip-only, and swap presentation; prefers SelectString and falls back to SelectionDialogText. | Ativado |
| SelectIconString dialog | `TranslateSelectIconString` | Família native-tooltip | Keeps its own toggle and display mode and uses body-only structured tooltip presentation. | Ativado |

## Superfícies de quest e journal

| Superfície | Toggle de configuração | Modos | Notas | Status da release atual |
| --- | --- | --- | --- | --- |
| Journal | `TranslateJournal` | Família quest / native-window | Superfície de lista de quests | Ativado |
| JournalDetail | `TranslateJournalDetail` | Família quest / native-window | Layout de corpo denso; o modo nativo exige block reflow explícito | Ativado |
| ToDoList | `TranslateToDoList` | Família quest / native-window | Quest tracker / lista de objetivos | Ativado |
| ToDo | `TranslateToDo` | Família quest / native-window | Instanced/FATE objective tracker. | Ativado |
| ScenarioTree | `TranslateScenarioTree` | Família quest / native-window | Tracker do cenário principal | Ativado |
| JournalAccept | `TranslateJournalAccept` | Família quest / native-window | Uses QuestPlate when a safe quest id is available and QuestPopupText fallback for live popup capture. | Ativado |
| JournalResult | `TranslateJournalResult` | Família quest / native-window | Prefers QuestPlate canonical lookup and falls back to QuestPopupText while missing rows are translated live. | Ativado |
| RecommendList | `TranslateRecommendList` | Família quest / native-window | Lista de recomendações | Ativado |
| AreaMap | `TranslateAreaMap` | Família quest / native-window | Quest text inside map-related quest UI; AreaMap and _NaviMap are string-array-backed. | Ativado |

## Superfícies de toast

| Superfície | Toggle de configuração | Modos | Notas | Status da release atual |
| --- | --- | --- | --- | --- |
| WideText / Screen Info toast | `TranslateWideTextToast` | Família overlay | Grande toast informativo no centro do ecrã | Ativado |
| Error toast | `TranslateErrorToast` | Família overlay | Notificações de erro ou falha | Ativado |
| Area toast | `TranslateAreaToast` | Família overlay | Notificações de área e localização | Ativado |
| Class / Job change toast | `TranslateClassChangeToast` | Família overlay | Anúncio de mudança de class/job | Ativado |
| Text gimmick hint | `TranslateTextGimmickHint` | Família overlay | Superfície de pista de gimmick/tutorial | Ativado |
| Quest toast | `TranslateQuestToast` | Família overlay | Notificação toast relacionada com quest | Ativado |

## Superfícies de janelas do jogo

| Superfície | Toggle de configuração | Modos | Notas | Status da release atual |
| --- | --- | --- | --- | --- |
| Character window | `TranslateCharacterWindow` | Família quest / native-window | Runtime DB-first de janelas do jogo | Ativado |
| Main Command | `TranslateMainCommandWindow` | Família quest / native-window | Runtime DB-first de janelas do jogo | Ativado |
| Action Menu | `TranslateActionMenuWindow` | Família quest / native-window | Runtime DB-first de janelas do jogo | Ativado |
| HUD windows | `TranslateHudWindow` | Família quest / native-window | Runtime DB-first de janelas do jogo | Ativado |
| Operation Guide | `TranslateOperationGuideWindow` | Família quest / native-window | Runtime DB-first de janelas do jogo | Ativado |
| Addon Context Menu Title | `TranslateAddonContextMenuTitle` | Família quest / native-window | Runtime DB-first de janelas do jogo | Ativado |
| Context Menu | `TranslateContextMenu` | Família native-tooltip | Dedicated DB-first row-chain runtime with row-local hover targets. | Ativado |
| Tooltip addon | `TranslateTooltipAddon` | Família native-tooltip | Dedicated DB-first Tooltip addon runtime; tooltip translation and swap use an anchored overlay on the live game tooltip. | Ativado |
| Action / item detail tooltips | `TranslateTooltips` | Família quest / native-window | A tradução estruturada de tooltips é desativada à força no arranque enquanto `ActionDetail` / `ItemDetail` permanecerem instáveis | Ativado |

## Superfícies de mundo e NamePlate

| Superfície | Toggle de configuração | Modos | Notas | Status da release atual |
| --- | --- | --- | --- | --- |
| NamePlates | `TranslateNamePlates` | Família híbrida native / distance-aware | Standard languages keep the native backend; overlay-only languages keep the native nameplate original and render the translation through the distance-aware overlay backend. | Ativado |

## Superfícies ocultas ou temporariamente restritas

| Superfície | Toggle de configuração | Modos | Notas | Status da release atual |
| --- | --- | --- | --- | --- |
