# Translation Surface Runtime Map

## Runtime Families

| Family | Presentation Modes |
| --- | --- |
| Native-tooltip family (`nativeTooltip`) | Native UI Translation; Tooltip Translation Only; Native UI Translation With Original Tooltips |
| Overlay family (`overlay`) | Native UI Translation; Overlay Translation Only; Native UI Translation With Original Overlay |
| Native / distance-aware hybrid family (`nativeDistanceAwareHybrid`) | Native UI Translation; Tooltip Translation Only; Native UI Translation With Original Tooltips |
| Quest / native-window family (`questNativeWindow`) | Native UI Translation; Tooltip Translation Only; Native UI Translation With Original Tooltips |

## Surface Runtime Details

| Surface | Family | Translation Model | Cache | DB Owner | DB Read | DB Write | Supporting Docs |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Talk | overlay | Live addon capture and asynchronous translation published through the dialogue overlay. | Shared dialogue translation reuse. | TalkMessage | sync | async | docs/dialogue-and-toast-runtime-flows.md |
| BattleTalk | overlay | Live addon capture and asynchronous translation published through the dialogue overlay. | Shared dialogue translation reuse. | BattleTalkMessage | sync | async | docs/dialogue-and-toast-runtime-flows.md |
| TalkSubtitle | overlay | Live subtitle capture and asynchronous overlay publication. | Shared dialogue translation reuse. | TalkSubtitleMessage | sync | async | docs/dialogue-and-toast-runtime-flows.md |
| MiniTalk | overlay | Live capture, cache or database lookup, asynchronous translation, then overlay or native publication. | Dedicated MiniTalk cache and shared source-publication lifecycle reuse. | MiniTalkMessage | sync | async | docs/dialogue-and-toast-runtime-flows.md |
| CutSceneSelectString | overlay | Live selection capture and asynchronous overlay publication. | Selection text reuse by source content. | CutSceneSelectStringMessage | sync | async | docs/dialogue-and-toast-runtime-flows.md |
| Yes/No dialog | nativeTooltip | Structured dialog capture with tooltip or native presentation. | Structured dialog source reuse. | SelectionDialogText | sync | async | docs/selection-dialog-and-tooltip-runtime-flow.md |
| SelectOk dialog | nativeTooltip | Structured dialog capture with tooltip or native presentation. | Structured dialog source reuse. | SelectionDialogText | sync | async | docs/selection-dialog-and-tooltip-runtime-flow.md |
| SelectString dialog | nativeTooltip | Structured dialog capture with tooltip or native presentation. | Structured dialog source reuse. | SelectString; SelectionDialogText fallback | sync | async | docs/selection-dialog-and-tooltip-runtime-flow.md |
| SelectIconString dialog | nativeTooltip | Structured dialog capture with body-only tooltip or native presentation. | Structured dialog source reuse. | SelectionDialogText | sync | async | docs/selection-dialog-and-tooltip-runtime-flow.md |
| Journal | questNativeWindow | Canonical quest lookup with native-window publication. | Quest canonical-row reuse. | QuestPlate | sync | async | docs/quest-addon-translation-runtime-flow.md |
| JournalDetail | questNativeWindow | Canonical quest lookup with native-window publication and block reflow. | Quest canonical-row reuse. | QuestPlate | sync | async | docs/quest-addon-translation-runtime-flow.md |
| ToDoList | questNativeWindow | Canonical quest lookup with native-window publication. | Quest canonical-row reuse. | QuestPlate | sync | async | docs/quest-addon-translation-runtime-flow.md |
| ToDo | questNativeWindow | Live objective capture with native-window publication. | Objective source reuse. | ToDoText | sync | async | docs/quest-addon-translation-runtime-flow.md |
| ScenarioTree | questNativeWindow | Canonical quest lookup with native-window publication. | Quest canonical-row reuse. | QuestPlate | sync | async | docs/quest-addon-translation-runtime-flow.md |
| JournalAccept | questNativeWindow | Canonical quest lookup or live popup capture with native-window publication. | Quest and popup source reuse. | QuestPlate; QuestPopupText fallback | sync | async | docs/quest-addon-translation-runtime-flow.md |
| JournalResult | questNativeWindow | Canonical quest lookup with live popup fallback and native-window publication. | Quest and popup source reuse. | QuestPlate; QuestPopupText fallback | sync | async | docs/quest-addon-translation-runtime-flow.md |
| RecommendList | questNativeWindow | Canonical quest lookup with native-window publication. | Quest canonical-row reuse. | QuestPlate | sync | async | docs/quest-addon-translation-runtime-flow.md |
| AreaMap | questNativeWindow | String-array capture and native-window publication. | String-array source reuse. | StringArrayData | sync | async | docs/quest-addon-translation-runtime-flow.md |
| WideText / Screen Info toast | overlay | Toast capture and asynchronous overlay publication. | Toast source reuse. | ToastMessage | sync | async | docs/dialogue-and-toast-runtime-flows.md |
| Error toast | overlay | Toast capture and asynchronous overlay publication. | Toast source reuse. | ToastMessage | sync | async | docs/dialogue-and-toast-runtime-flows.md |
| Area toast | overlay | Toast capture and asynchronous overlay publication. | Toast source reuse. | ToastMessage | sync | async | docs/dialogue-and-toast-runtime-flows.md |
| Class / Job change toast | overlay | Toast capture and asynchronous overlay publication. | Toast source reuse. | ToastMessage | sync | async | docs/dialogue-and-toast-runtime-flows.md |
| Text gimmick hint | overlay | Addon capture and asynchronous overlay publication. | Toast source reuse. | TextGimmickHintMessage | sync | async | docs/dialogue-and-toast-runtime-flows.md |
| Quest toast | overlay | Toast capture and asynchronous overlay publication. | Toast source reuse. | ToastMessage | sync | async | docs/dialogue-and-toast-runtime-flows.md |
| Character window | questNativeWindow | DB-first game-window lookup with native publication. | Game-window row reuse. | GameWindow | sync | async | docs/action-domain-runtime-flow-index.md |
| Main Command | questNativeWindow | DB-first game-window lookup with native publication. | Game-window row reuse. | GameWindow | sync | async | docs/maincommand-addon-gamewindow-flow.md |
| Action Menu | questNativeWindow | DB-first game-window lookup with native publication. | Game-window row reuse. | GameWindow | sync | async | docs/actionmenu-runtime-flow.md |
| HUD windows | questNativeWindow | DB-first game-window lookup with native publication. | Game-window row reuse. | GameWindow | sync | async | docs/action-domain-runtime-flow-index.md |
| Operation Guide | questNativeWindow | DB-first game-window lookup with native publication. | Game-window row reuse. | GameWindow | sync | async | docs/action-domain-runtime-flow-index.md |
| Addon Context Menu Title | questNativeWindow | DB-first game-window lookup with native publication. | Game-window row reuse. | GameWindow | sync | async | docs/action-domain-runtime-flow-index.md |
| Context Menu | nativeTooltip | DB-first row-chain lookup with tooltip or native publication. | Context-menu row reuse. | ContextMenuText | sync | async | docs/selection-dialog-and-tooltip-runtime-flow.md |
| Tooltip addon | nativeTooltip | DB-first Tooltip addon lookup with anchored overlay or native publication. | Tooltip source reuse. | TooltipText | sync | async | docs/selection-dialog-and-tooltip-runtime-flow.md |
| Action / item detail tooltips | questNativeWindow | Sheet and DB-first structured tooltip lookup with plugin tooltip or guarded native publication. | Reference-text and tooltip reuse. | ActionTooltip, ItemTooltip, Trait | sync | async | docs/action-detail-sheet-flow.md |
| NamePlates | nativeDistanceAwareHybrid | Native presentation for standard languages and distance-aware overlay fallback for overlay-only languages. | Nameplate source reuse. | NamePlateMessage | sync | async | docs/translation-surface-support-matrix.md |
