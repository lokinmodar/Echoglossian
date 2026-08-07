<!--
  Copyright (c) lokinmodar. All rights reserved.
  Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
-->

# Matrice de prise en charge des surfaces de traduction

## Familles de modes de traduction

| Famille de modes | Modes |
| --- | --- |
| Famille native-tooltip | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |
| Famille overlay | `Native UI Translation`; `Overlay Translation Only`; `Native UI Translation With Original Overlay` |
| Famille hybride native / distance-aware | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |
| Famille quête / native-window | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |

## Surfaces de dialogue et d’overlay

| Surface | Bascule de configuration | Modes | Notes | État de la version actuelle |
| --- | --- | --- | --- | --- |
| Talk | `TranslateTalk` | Famille overlay | Prend en charge les noms de PNJ traduits via `TranslateTalkNpcNames` | Activé |
| BattleTalk | `TranslateBattleTalk` | Famille overlay | Prend en charge les noms de PNJ traduits via `TranslateBattleTalkNpcNames` | Activé |
| TalkSubtitle | `TranslateTalkSubtitle` | Famille overlay | Uses titleless overlay presentation when overlay mode is active. | Activé |
| MiniTalk | `TranslateMiniTalk` | Famille overlay | Petite surface native ; les textes plus verbeux nécessitent encore un reflow natif soigné | Activé |
| CutSceneSelectString | `TranslateCutSceneSelectString` | Famille overlay | La question devient le titre et les options deviennent le corps en mode overlay | Activé |
| Yes/No dialog | `TranslateYesNoScreen` | Famille native-tooltip | Présent dans le modèle de configuration et l’implémentation de l’onglet, mais non exposé actuellement dans le flux actif de l’onglet Overlay | Activé |
| SelectOk dialog | `TranslateSelectOk` | Famille native-tooltip | Présent dans le modèle de configuration et l’implémentation de l’onglet, mais non exposé actuellement dans le flux actif de l’onglet Overlay | Activé |
| SelectString dialog | `TranslateSelectString` | Famille native-tooltip | Uses structured plugin tooltips instead of overlay windows and supports native, tooltip-only, and swap presentation; prefers SelectString and falls back to SelectionDialogText. | Activé |
| SelectIconString dialog | `TranslateSelectIconString` | Famille native-tooltip | Keeps its own toggle and display mode and uses body-only structured tooltip presentation. | Activé |

## Surfaces de quête et de journal

| Surface | Bascule de configuration | Modes | Notes | État de la version actuelle |
| --- | --- | --- | --- | --- |
| Journal | `TranslateJournal` | Famille quête / native-window | Surface de liste de quêtes | Activé |
| JournalDetail | `TranslateJournalDetail` | Famille quête / native-window | Mise en page de corps dense ; le mode natif nécessite un block reflow explicite | Activé |
| ToDoList | `TranslateToDoList` | Famille quête / native-window | Suivi de quête / liste d’objectifs | Activé |
| ToDo | `TranslateToDo` | Famille quête / native-window | Instanced/FATE objective tracker. | Activé |
| ScenarioTree | `TranslateScenarioTree` | Famille quête / native-window | Suivi du scénario principal | Activé |
| JournalAccept | `TranslateJournalAccept` | Famille quête / native-window | Uses QuestPlate when a safe quest id is available and QuestPopupText fallback for live popup capture. | Activé |
| JournalResult | `TranslateJournalResult` | Famille quête / native-window | Prefers QuestPlate canonical lookup and falls back to QuestPopupText while missing rows are translated live. | Activé |
| RecommendList | `TranslateRecommendList` | Famille quête / native-window | Liste de recommandations | Activé |
| AreaMap | `TranslateAreaMap` | Famille quête / native-window | Quest text inside map-related quest UI; AreaMap and _NaviMap are string-array-backed. | Activé |

## Surfaces de toast

| Surface | Bascule de configuration | Modes | Notes | État de la version actuelle |
| --- | --- | --- | --- | --- |
| WideText / Screen Info toast | `TranslateWideTextToast` | Famille overlay | Grand toast d’information au centre de l’écran | Activé |
| Error toast | `TranslateErrorToast` | Famille overlay | Notifications d’erreur ou d’échec | Activé |
| Area toast | `TranslateAreaToast` | Famille overlay | Notifications de zone et de localisation | Activé |
| Class / Job change toast | `TranslateClassChangeToast` | Famille overlay | Annonce de changement de class/job | Activé |
| Text gimmick hint | `TranslateTextGimmickHint` | Famille overlay | Surface d’indice de gimmick/tutorial | Activé |
| Quest toast | `TranslateQuestToast` | Famille overlay | Notification toast liée à une quête | Activé |

## Surfaces des fenêtres du jeu

| Surface | Bascule de configuration | Modes | Notes | État de la version actuelle |
| --- | --- | --- | --- | --- |
| Character window | `TranslateCharacterWindow` | Famille quête / native-window | Runtime DB-first des fenêtres du jeu | Activé |
| Main Command | `TranslateMainCommandWindow` | Famille quête / native-window | Runtime DB-first des fenêtres du jeu | Activé |
| Action Menu | `TranslateActionMenuWindow` | Famille quête / native-window | Runtime DB-first des fenêtres du jeu | Activé |
| HUD windows | `TranslateHudWindow` | Famille quête / native-window | Runtime DB-first des fenêtres du jeu | Activé |
| Operation Guide | `TranslateOperationGuideWindow` | Famille quête / native-window | Runtime DB-first des fenêtres du jeu | Activé |
| Addon Context Menu Title | `TranslateAddonContextMenuTitle` | Famille quête / native-window | Runtime DB-first des fenêtres du jeu | Activé |
| Context Menu | `TranslateContextMenu` | Famille native-tooltip | Dedicated DB-first row-chain runtime with row-local hover targets. | Activé |
| Tooltip addon | `TranslateTooltipAddon` | Famille native-tooltip | Dedicated DB-first Tooltip addon runtime; tooltip translation and swap use an anchored overlay on the live game tooltip. | Activé |
| Action / item detail tooltips | `TranslateTooltips` | Famille quête / native-window | La traduction structurée des tooltips est désactivée de force au démarrage tant que `ActionDetail` / `ItemDetail` restent instables | Activé |

## Surfaces du monde et NamePlate

| Surface | Bascule de configuration | Modes | Notes | État de la version actuelle |
| --- | --- | --- | --- | --- |
| NamePlates | `TranslateNamePlates` | Famille hybride native / distance-aware | Standard languages keep the native backend; overlay-only languages keep the native nameplate original and render the translation through the distance-aware overlay backend. | Activé |

## Surfaces cachées ou temporairement restreintes

| Surface | Bascule de configuration | Modes | Notes | État de la version actuelle |
| --- | --- | --- | --- | --- |
