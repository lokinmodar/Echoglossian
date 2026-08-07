<!--
  Copyright (c) lokinmodar. All rights reserved.
  Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
-->

# Πίνακας υποστήριξης επιφανειών μετάφρασης

## Οικογένειες λειτουργιών μετάφρασης

| Οικογένεια λειτουργιών | Λειτουργίες |
| --- | --- |
| Οικογένεια native-tooltip | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |
| Οικογένεια overlay | `Native UI Translation`; `Overlay Translation Only`; `Native UI Translation With Original Overlay` |
| Υβριδική οικογένεια native / distance-aware | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |
| Οικογένεια quest / native-window | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |

## Επιφάνειες διαλόγων και overlay

| Επιφάνεια | Εναλλαγή ρύθμισης | Λειτουργίες | Σημειώσεις | Κατάσταση τρέχουσας έκδοσης |
| --- | --- | --- | --- | --- |
| Talk | `TranslateTalk` | Οικογένεια overlay | Υποστηρίζει μεταφρασμένα ονόματα NPC μέσω `TranslateTalkNpcNames` | Ενεργό |
| BattleTalk | `TranslateBattleTalk` | Οικογένεια overlay | Υποστηρίζει μεταφρασμένα ονόματα NPC μέσω `TranslateBattleTalkNpcNames` | Ενεργό |
| TalkSubtitle | `TranslateTalkSubtitle` | Οικογένεια overlay | Uses titleless overlay presentation when overlay mode is active. | Ενεργό |
| MiniTalk | `TranslateMiniTalk` | Οικογένεια overlay | Μικρή native επιφάνεια· τα πιο εκτενή κείμενα χρειάζονται ακόμη προσεκτικό native reflow | Ενεργό |
| CutSceneSelectString | `TranslateCutSceneSelectString` | Οικογένεια overlay | Η ερώτηση γίνεται τίτλος και οι επιλογές γίνονται το σώμα στο overlay mode | Ενεργό |
| Yes/No dialog | `TranslateYesNoScreen` | Οικογένεια native-tooltip | Υπάρχει στο μοντέλο ρυθμίσεων και στην υλοποίηση tab, αλλά δεν εκτίθεται σήμερα στο ενεργό overlay-tab flow | Ενεργό |
| SelectOk dialog | `TranslateSelectOk` | Οικογένεια native-tooltip | Υπάρχει στο μοντέλο ρυθμίσεων και στην υλοποίηση tab, αλλά δεν εκτίθεται σήμερα στο ενεργό overlay-tab flow | Ενεργό |
| SelectString dialog | `TranslateSelectString` | Οικογένεια native-tooltip | Uses structured plugin tooltips instead of overlay windows and supports native, tooltip-only, and swap presentation; prefers SelectString and falls back to SelectionDialogText. | Ενεργό |
| SelectIconString dialog | `TranslateSelectIconString` | Οικογένεια native-tooltip | Keeps its own toggle and display mode and uses body-only structured tooltip presentation. | Ενεργό |

## Επιφάνειες quest και journal

| Επιφάνεια | Εναλλαγή ρύθμισης | Λειτουργίες | Σημειώσεις | Κατάσταση τρέχουσας έκδοσης |
| --- | --- | --- | --- | --- |
| Journal | `TranslateJournal` | Οικογένεια quest / native-window | Επιφάνεια λίστας quest | Ενεργό |
| JournalDetail | `TranslateJournalDetail` | Οικογένεια quest / native-window | Πυκνή διάταξη σώματος· η native λειτουργία απαιτεί ρητό block reflow | Ενεργό |
| ToDoList | `TranslateToDoList` | Οικογένεια quest / native-window | Quest tracker / λίστα στόχων | Ενεργό |
| ToDo | `TranslateToDo` | Οικογένεια quest / native-window | Instanced/FATE objective tracker. | Ενεργό |
| ScenarioTree | `TranslateScenarioTree` | Οικογένεια quest / native-window | Tracker κύριου σεναρίου | Ενεργό |
| JournalAccept | `TranslateJournalAccept` | Οικογένεια quest / native-window | Uses QuestPlate when a safe quest id is available and QuestPopupText fallback for live popup capture. | Ενεργό |
| JournalResult | `TranslateJournalResult` | Οικογένεια quest / native-window | Prefers QuestPlate canonical lookup and falls back to QuestPopupText while missing rows are translated live. | Ενεργό |
| RecommendList | `TranslateRecommendList` | Οικογένεια quest / native-window | Λίστα προτάσεων | Ενεργό |
| AreaMap | `TranslateAreaMap` | Οικογένεια quest / native-window | Quest text inside map-related quest UI; AreaMap and _NaviMap are string-array-backed. | Ενεργό |

## Επιφάνειες toast

| Επιφάνεια | Εναλλαγή ρύθμισης | Λειτουργίες | Σημειώσεις | Κατάσταση τρέχουσας έκδοσης |
| --- | --- | --- | --- | --- |
| WideText / Screen Info toast | `TranslateWideTextToast` | Οικογένεια overlay | Μεγάλο informational toast στο κέντρο της οθόνης | Ενεργό |
| Error toast | `TranslateErrorToast` | Οικογένεια overlay | Ειδοποιήσεις σφάλματος / αποτυχίας | Ενεργό |
| Area toast | `TranslateAreaToast` | Οικογένεια overlay | Ειδοποιήσεις περιοχής και τοποθεσίας | Ενεργό |
| Class / Job change toast | `TranslateClassChangeToast` | Οικογένεια overlay | Ανακοίνωση αλλαγής class/job | Ενεργό |
| Text gimmick hint | `TranslateTextGimmickHint` | Οικογένεια overlay | Επιφάνεια hint για gimmick/tutorial | Ενεργό |
| Quest toast | `TranslateQuestToast` | Οικογένεια overlay | Toast ειδοποίηση σχετική με quest | Ενεργό |

## Επιφάνειες παραθύρων παιχνιδιού

| Επιφάνεια | Εναλλαγή ρύθμισης | Λειτουργίες | Σημειώσεις | Κατάσταση τρέχουσας έκδοσης |
| --- | --- | --- | --- | --- |
| Character window | `TranslateCharacterWindow` | Οικογένεια quest / native-window | DB-first runtime παραθύρων παιχνιδιού | Ενεργό |
| Main Command | `TranslateMainCommandWindow` | Οικογένεια quest / native-window | DB-first runtime παραθύρων παιχνιδιού | Ενεργό |
| Action Menu | `TranslateActionMenuWindow` | Οικογένεια quest / native-window | DB-first runtime παραθύρων παιχνιδιού | Ενεργό |
| HUD windows | `TranslateHudWindow` | Οικογένεια quest / native-window | DB-first runtime παραθύρων παιχνιδιού | Ενεργό |
| Operation Guide | `TranslateOperationGuideWindow` | Οικογένεια quest / native-window | DB-first runtime παραθύρων παιχνιδιού | Ενεργό |
| Addon Context Menu Title | `TranslateAddonContextMenuTitle` | Οικογένεια quest / native-window | DB-first runtime παραθύρων παιχνιδιού | Ενεργό |
| Context Menu | `TranslateContextMenu` | Οικογένεια native-tooltip | Dedicated DB-first row-chain runtime with row-local hover targets. | Ενεργό |
| Tooltip addon | `TranslateTooltipAddon` | Οικογένεια native-tooltip | Dedicated DB-first Tooltip addon runtime; tooltip translation and swap use an anchored overlay on the live game tooltip. | Ενεργό |
| Action / item detail tooltips | `TranslateTooltips` | Οικογένεια quest / native-window | Η δομημένη μετάφραση tooltip απενεργοποιείται υποχρεωτικά στην εκκίνηση όσο τα `ActionDetail` / `ItemDetail` παραμένουν ασταθή | Ενεργό |

## Επιφάνειες κόσμου και NamePlate

| Επιφάνεια | Εναλλαγή ρύθμισης | Λειτουργίες | Σημειώσεις | Κατάσταση τρέχουσας έκδοσης |
| --- | --- | --- | --- | --- |
| NamePlates | `TranslateNamePlates` | Υβριδική οικογένεια native / distance-aware | Standard languages keep the native backend; overlay-only languages keep the native nameplate original and render the translation through the distance-aware overlay backend. | Ενεργό |

## Κρυφές ή προσωρινά περιορισμένες επιφάνειες

| Επιφάνεια | Εναλλαγή ρύθμισης | Λειτουργίες | Σημειώσεις | Κατάσταση τρέχουσας έκδοσης |
| --- | --- | --- | --- | --- |
