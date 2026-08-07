<!--
  Copyright (c) lokinmodar. All rights reserved.
  Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
-->

# Itzulpen-gainazalen euskarrien matrizea

## Itzulpen-moduen familiak

| Modu-familia | Moduak |
| --- | --- |
| Native-tooltip familia | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |
| Overlay familia | `Native UI Translation`; `Overlay Translation Only`; `Native UI Translation With Original Overlay` |
| Native / distance-aware familia hibridoa | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |
| Quest / native-window familia | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |

## Elkarrizketa eta overlay gainazalak

| Gainazala | Konfigurazio-txandakagailua | Moduak | Oharrak | Uneko bertsioaren egoera |
| --- | --- | --- | --- | --- |
| Talk | `TranslateTalk` | Overlay familia | NPC izen itzuliak onartzen ditu `TranslateTalkNpcNames` bidez | Aktibatuta |
| BattleTalk | `TranslateBattleTalk` | Overlay familia | NPC izen itzuliak onartzen ditu `TranslateBattleTalkNpcNames` bidez | Aktibatuta |
| TalkSubtitle | `TranslateTalkSubtitle` | Overlay familia | Uses titleless overlay presentation when overlay mode is active. | Aktibatuta |
| MiniTalk | `TranslateMiniTalk` | Overlay familia | Native gainazal txikia; testu luzeagoek native reflow zaindua behar dute oraindik | Aktibatuta |
| CutSceneSelectString | `TranslateCutSceneSelectString` | Overlay familia | Galdera izenburu bihurtzen da eta aukerak gorputz testu overlay moduan | Aktibatuta |
| Yes/No dialog | `TranslateYesNoScreen` | Native-tooltip familia | Konfigurazio ereduan eta tab inplementazioan dago, baina gaur egun ez dago ikusgai overlay tab aktiboaren fluxuan | Aktibatuta |
| SelectOk dialog | `TranslateSelectOk` | Native-tooltip familia | Konfigurazio ereduan eta tab inplementazioan dago, baina gaur egun ez dago ikusgai overlay tab aktiboaren fluxuan | Aktibatuta |
| SelectString dialog | `TranslateSelectString` | Native-tooltip familia | Uses structured plugin tooltips instead of overlay windows and supports native, tooltip-only, and swap presentation; prefers SelectString and falls back to SelectionDialogText. | Aktibatuta |
| SelectIconString dialog | `TranslateSelectIconString` | Native-tooltip familia | Keeps its own toggle and display mode and uses body-only structured tooltip presentation. | Aktibatuta |

## Quest eta journal gainazalak

| Gainazala | Konfigurazio-txandakagailua | Moduak | Oharrak | Uneko bertsioaren egoera |
| --- | --- | --- | --- | --- |
| Journal | `TranslateJournal` | Quest / native-window familia | Quest zerrendaren gainazala | Aktibatuta |
| JournalDetail | `TranslateJournalDetail` | Quest / native-window familia | Gorputz diseinu trinkoa; native moduak block reflow esplizitua behar du | Aktibatuta |
| ToDoList | `TranslateToDoList` | Quest / native-window familia | Quest jarraipena / helburuen zerrenda | Aktibatuta |
| ToDo | `TranslateToDo` | Quest / native-window familia | Instanced/FATE objective tracker. | Aktibatuta |
| ScenarioTree | `TranslateScenarioTree` | Quest / native-window familia | Eszenatoki nagusiaren jarraipena | Aktibatuta |
| JournalAccept | `TranslateJournalAccept` | Quest / native-window familia | Uses QuestPlate when a safe quest id is available and QuestPopupText fallback for live popup capture. | Aktibatuta |
| JournalResult | `TranslateJournalResult` | Quest / native-window familia | Prefers QuestPlate canonical lookup and falls back to QuestPopupText while missing rows are translated live. | Aktibatuta |
| RecommendList | `TranslateRecommendList` | Quest / native-window familia | Gomendioen zerrenda | Aktibatuta |
| AreaMap | `TranslateAreaMap` | Quest / native-window familia | Quest text inside map-related quest UI; AreaMap and _NaviMap are string-array-backed. | Aktibatuta |

## Toast gainazalak

| Gainazala | Konfigurazio-txandakagailua | Moduak | Oharrak | Uneko bertsioaren egoera |
| --- | --- | --- | --- | --- |
| WideText / Screen Info toast | `TranslateWideTextToast` | Overlay familia | Pantailaren erdiko informazio-toast handia | Aktibatuta |
| Error toast | `TranslateErrorToast` | Overlay familia | Errore edo hutsegite jakinarazpenak | Aktibatuta |
| Area toast | `TranslateAreaToast` | Overlay familia | Eremu eta kokapen jakinarazpenak | Aktibatuta |
| Class / Job change toast | `TranslateClassChangeToast` | Overlay familia | Class/job aldaketaren iragarkia | Aktibatuta |
| Text gimmick hint | `TranslateTextGimmickHint` | Overlay familia | Gimmick/tutorial pistaren gainazala | Aktibatuta |
| Quest toast | `TranslateQuestToast` | Overlay familia | Quest-ekin lotutako toast jakinarazpena | Aktibatuta |

## Joko-leihoen gainazalak

| Gainazala | Konfigurazio-txandakagailua | Moduak | Oharrak | Uneko bertsioaren egoera |
| --- | --- | --- | --- | --- |
| Character window | `TranslateCharacterWindow` | Quest / native-window familia | DB-first game-window runtime. | Aktibatuta |
| Main Command | `TranslateMainCommandWindow` | Quest / native-window familia | DB-first game-window runtime. | Aktibatuta |
| Action Menu | `TranslateActionMenuWindow` | Quest / native-window familia | DB-first game-window runtime. | Aktibatuta |
| HUD windows | `TranslateHudWindow` | Quest / native-window familia | DB-first game-window runtime. | Aktibatuta |
| Operation Guide | `TranslateOperationGuideWindow` | Quest / native-window familia | DB-first game-window runtime. | Aktibatuta |
| Addon Context Menu Title | `TranslateAddonContextMenuTitle` | Quest / native-window familia | DB-first game-window runtime. | Aktibatuta |
| Context Menu | `TranslateContextMenu` | Native-tooltip familia | Dedicated DB-first row-chain runtime with row-local hover targets. | Aktibatuta |
| Tooltip addon | `TranslateTooltipAddon` | Native-tooltip familia | Dedicated DB-first Tooltip addon runtime; tooltip translation and swap use an anchored overlay on the live game tooltip. | Aktibatuta |
| Action / item detail tooltips | `TranslateTooltips` | Quest / native-window familia | Egituratutako tooltip itzulpena indarrez desgaitzen da abioan, `ActionDetail` / `ItemDetail` oraindik ezegonkorrak direlako | Aktibatuta |

## Mundu eta NamePlate gainazalak

| Gainazala | Konfigurazio-txandakagailua | Moduak | Oharrak | Uneko bertsioaren egoera |
| --- | --- | --- | --- | --- |
| NamePlates | `TranslateNamePlates` | Native / distance-aware familia hibridoa | Standard languages keep the native backend; overlay-only languages keep the native nameplate original and render the translation through the distance-aware overlay backend. | Aktibatuta |

## Ezkutuko edo aldi baterako mugatutako gainazalak

| Gainazala | Konfigurazio-txandakagailua | Moduak | Oharrak | Uneko bertsioaren egoera |
| --- | --- | --- | --- | --- |
