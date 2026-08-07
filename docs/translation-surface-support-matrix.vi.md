<!--
  Copyright (c) lokinmodar. All rights reserved.
  Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
-->

# Ma trận hỗ trợ các bề mặt dịch

## Nhóm chế độ dịch

| Nhóm chế độ | Các chế độ |
| --- | --- |
| Nhóm native-tooltip | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |
| Nhóm overlay | `Native UI Translation`; `Overlay Translation Only`; `Native UI Translation With Original Overlay` |
| Nhóm lai native / distance-aware | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |
| Nhóm quest / native-window | `Native UI Translation`; `Tooltip Translation Only`; `Native UI Translation With Original Tooltips` |

## Bề mặt hội thoại và overlay

| Bề mặt | Công tắc cấu hình | Các chế độ | Ghi chú | Trạng thái phát hành hiện tại |
| --- | --- | --- | --- | --- |
| Talk | `TranslateTalk` | Nhóm overlay | Hỗ trợ dịch tên NPC qua `TranslateTalkNpcNames` | Đã bật |
| BattleTalk | `TranslateBattleTalk` | Nhóm overlay | Hỗ trợ dịch tên NPC qua `TranslateBattleTalkNpcNames` | Đã bật |
| TalkSubtitle | `TranslateTalkSubtitle` | Nhóm overlay | Uses titleless overlay presentation when overlay mode is active. | Đã bật |
| MiniTalk | `TranslateMiniTalk` | Nhóm overlay | Bề mặt native nhỏ; văn bản dài hơn vẫn cần native reflow cẩn thận | Đã bật |
| CutSceneSelectString | `TranslateCutSceneSelectString` | Nhóm overlay | Câu hỏi trở thành tiêu đề và các lựa chọn trở thành phần thân ở chế độ overlay | Đã bật |
| Yes/No dialog | `TranslateYesNoScreen` | Nhóm native-tooltip | Có trong mô hình cấu hình và phần cài đặt tab, nhưng hiện chưa được hiển thị trong luồng tab Overlay đang hoạt động | Đã bật |
| SelectOk dialog | `TranslateSelectOk` | Nhóm native-tooltip | Có trong mô hình cấu hình và phần cài đặt tab, nhưng hiện chưa được hiển thị trong luồng tab Overlay đang hoạt động | Đã bật |
| SelectString dialog | `TranslateSelectString` | Nhóm native-tooltip | Uses structured plugin tooltips instead of overlay windows and supports native, tooltip-only, and swap presentation; prefers SelectString and falls back to SelectionDialogText. | Đã bật |
| SelectIconString dialog | `TranslateSelectIconString` | Nhóm native-tooltip | Keeps its own toggle and display mode and uses body-only structured tooltip presentation. | Đã bật |

## Bề mặt quest và journal

| Bề mặt | Công tắc cấu hình | Các chế độ | Ghi chú | Trạng thái phát hành hiện tại |
| --- | --- | --- | --- | --- |
| Journal | `TranslateJournal` | Nhóm quest / native-window | Bề mặt danh sách quest | Đã bật |
| JournalDetail | `TranslateJournalDetail` | Nhóm quest / native-window | Bố cục phần thân dày đặc; chế độ native cần block reflow rõ ràng | Đã bật |
| ToDoList | `TranslateToDoList` | Nhóm quest / native-window | Trình theo dõi quest / danh sách mục tiêu | Đã bật |
| ToDo | `TranslateToDo` | Nhóm quest / native-window | Instanced/FATE objective tracker. | Đã bật |
| ScenarioTree | `TranslateScenarioTree` | Nhóm quest / native-window | Trình theo dõi kịch bản chính | Đã bật |
| JournalAccept | `TranslateJournalAccept` | Nhóm quest / native-window | Uses QuestPlate when a safe quest id is available and QuestPopupText fallback for live popup capture. | Đã bật |
| JournalResult | `TranslateJournalResult` | Nhóm quest / native-window | Prefers QuestPlate canonical lookup and falls back to QuestPopupText while missing rows are translated live. | Đã bật |
| RecommendList | `TranslateRecommendList` | Nhóm quest / native-window | Danh sách gợi ý | Đã bật |
| AreaMap | `TranslateAreaMap` | Nhóm quest / native-window | Quest text inside map-related quest UI; AreaMap and _NaviMap are string-array-backed. | Đã bật |

## Bề mặt toast

| Bề mặt | Công tắc cấu hình | Các chế độ | Ghi chú | Trạng thái phát hành hiện tại |
| --- | --- | --- | --- | --- |
| WideText / Screen Info toast | `TranslateWideTextToast` | Nhóm overlay | Toast thông tin lớn ở giữa màn hình | Đã bật |
| Error toast | `TranslateErrorToast` | Nhóm overlay | Thông báo lỗi / thất bại | Đã bật |
| Area toast | `TranslateAreaToast` | Nhóm overlay | Thông báo khu vực và vị trí | Đã bật |
| Class / Job change toast | `TranslateClassChangeToast` | Nhóm overlay | Thông báo đổi class/job | Đã bật |
| Text gimmick hint | `TranslateTextGimmickHint` | Nhóm overlay | Bề mặt gợi ý gimmick/tutorial | Đã bật |
| Quest toast | `TranslateQuestToast` | Nhóm overlay | Toast liên quan đến quest | Đã bật |

## Bề mặt cửa sổ game

| Bề mặt | Công tắc cấu hình | Các chế độ | Ghi chú | Trạng thái phát hành hiện tại |
| --- | --- | --- | --- | --- |
| Character window | `TranslateCharacterWindow` | Nhóm quest / native-window | Runtime cửa sổ game DB-first | Đã bật |
| Main Command | `TranslateMainCommandWindow` | Nhóm quest / native-window | Runtime cửa sổ game DB-first | Đã bật |
| Action Menu | `TranslateActionMenuWindow` | Nhóm quest / native-window | Runtime cửa sổ game DB-first | Đã bật |
| HUD windows | `TranslateHudWindow` | Nhóm quest / native-window | Runtime cửa sổ game DB-first | Đã bật |
| Operation Guide | `TranslateOperationGuideWindow` | Nhóm quest / native-window | Runtime cửa sổ game DB-first | Đã bật |
| Addon Context Menu Title | `TranslateAddonContextMenuTitle` | Nhóm quest / native-window | Runtime cửa sổ game DB-first | Đã bật |
| Context Menu | `TranslateContextMenu` | Nhóm native-tooltip | Dedicated DB-first row-chain runtime with row-local hover targets. | Đã bật |
| Tooltip addon | `TranslateTooltipAddon` | Nhóm native-tooltip | Dedicated DB-first Tooltip addon runtime; tooltip translation and swap use an anchored overlay on the live game tooltip. | Đã bật |
| Action / item detail tooltips | `TranslateTooltips` | Nhóm quest / native-window | Dịch tooltip có cấu trúc bị tắt cưỡng bức khi khởi động trong khi `ActionDetail` / `ItemDetail` vẫn chưa ổn định | Đã bật |

## Bề mặt thế giới và NamePlate

| Bề mặt | Công tắc cấu hình | Các chế độ | Ghi chú | Trạng thái phát hành hiện tại |
| --- | --- | --- | --- | --- |
| NamePlates | `TranslateNamePlates` | Nhóm lai native / distance-aware | Standard languages keep the native backend; overlay-only languages keep the native nameplate original and render the translation through the distance-aware overlay backend. | Đã bật |

## Bề mặt ẩn hoặc bị giới hạn tạm thời

| Bề mặt | Công tắc cấu hình | Các chế độ | Ghi chú | Trạng thái phát hành hiện tại |
| --- | --- | --- | --- | --- |
