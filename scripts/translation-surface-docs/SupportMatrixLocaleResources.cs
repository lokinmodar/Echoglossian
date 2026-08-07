// <copyright file="SupportMatrixLocaleResources.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace TranslationSurfaceDocs;

/// <summary>
/// Supplies deterministic matrix labels for every catalog locale.
/// </summary>
internal static class SupportMatrixLocaleResources
{
    private static readonly IReadOnlyDictionary<string, string> CanonicalModeLabels =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Native UI Translation"] = "Native UI Translation",
            ["Tooltip Translation Only"] = "Tooltip Translation Only",
            ["Native UI Translation With Original Tooltips"] =
                "Native UI Translation With Original Tooltips",
            ["Overlay Translation Only"] = "Overlay Translation Only",
            ["Native UI Translation With Original Overlay"] =
                "Native UI Translation With Original Overlay",
        };

    private static readonly IReadOnlyDictionary<string, SupportMatrixLocaleResourceSet> Resources =
        new Dictionary<string, SupportMatrixLocaleResourceSet>(StringComparer.Ordinal)
        {
            ["en"] = Create(
                "Translation Surface Support Matrix",
                "Translation Mode Families",
                "Mode Family",
                "Surface",
                "Config Toggle",
                "Modes",
                "Notes",
                "Current Release Status",
                CreateModeFamilyNames(
                    "Native-tooltip family",
                    "Overlay family",
                    "Native / distance-aware hybrid family",
                    "Quest / native-window family"),
                CreateReleaseStatuses("Enabled"),
                ["Dialog and Overlay Surfaces", "Quest and Journal Surfaces", "Toast Surfaces", "Game Window Surfaces", "World-space And NamePlate Surfaces", "Hidden or Temporarily Restricted Surfaces"]),
            ["pt-BR"] = Create(
                "Matriz de suporte das superfícies de tradução",
                "Famílias de modos de tradução",
                "Família de modos",
                "Superfície",
                "Toggle de configuração",
                "Modos",
                "Notas",
                "Status da release atual",
                CreateModeFamilyNames(
                    "Família native-tooltip",
                    "Família overlay",
                    "Família híbrida native / distance-aware",
                    "Família quest / native-window"),
                CreateReleaseStatuses("Ativado"),
                ["Superfícies de diálogo e overlay", "Superfícies de quest e journal", "Superfícies de toast", "Superfícies de janelas do jogo", "Superfícies de mundo e NamePlate", "Superfícies ocultas ou temporariamente restritas"]),
            ["pt"] = Create(
                "Matriz de suporte das superfícies de tradução",
                "Famílias de modos de tradução",
                "Família de modos",
                "Superfície",
                "Toggle de configuração",
                "Modos",
                "Notas",
                "Status da release atual",
                CreateModeFamilyNames(
                    "Família native-tooltip",
                    "Família overlay",
                    "Família híbrida native / distance-aware",
                    "Família quest / native-window"),
                CreateReleaseStatuses("Ativado"),
                ["Superfícies de diálogo e overlay", "Superfícies de quest e journal", "Superfícies de toast", "Superfícies de janelas do jogo", "Superfícies de mundo e NamePlate", "Superfícies ocultas ou temporariamente restritas"]),
            ["de"] = Create(
                "Matrix der unterstützten Übersetzungsoberflächen",
                "Übersetzungsmodus-Familien",
                "Modusfamilie",
                "Oberfläche",
                "Konfigurations-Toggle",
                "Modi",
                "Hinweise",
                "Status der aktuellen Release",
                CreateModeFamilyNames(
                    "Native-Tooltip-Familie",
                    "Overlay-Familie",
                    "Native-/Distance-Aware-Hybridfamilie",
                    "Quest-/Native-Window-Familie"),
                CreateReleaseStatuses("Aktiviert"),
                ["Dialog- und Overlay-Oberflächen", "Quest- und Journal-Oberflächen", "Toast-Oberflächen", "Spiel-Fenster-Oberflächen", "Welt- und NamePlate-Oberflächen", "Versteckte oder vorübergehend eingeschränkte Oberflächen"]),
            ["da"] = Create(
                "Oversigt over understøttede oversættelsesflader",
                "Familier af oversættelsestilstande",
                "Tilstandsfamilie",
                "Flade",
                "Konfig-toggle",
                "Tilstande",
                "Bemærkninger",
                "Status i nuværende release",
                CreateModeFamilyNames(
                    "Native-tooltip-familie",
                    "Overlay-familie",
                    "Native-/distance-aware-hybridfamilie",
                    "Quest-/native-window-familie"),
                CreateReleaseStatuses("Aktiveret"),
                ["Dialog- og overlayflader", "Quest- og journalflader", "Toast-flader", "Spilvinduesflader", "Verdens- og NamePlate-flader", "Skjulte eller midlertidigt begrænsede flader"]),
            ["el"] = Create(
                "Πίνακας υποστήριξης επιφανειών μετάφρασης",
                "Οικογένειες λειτουργιών μετάφρασης",
                "Οικογένεια λειτουργιών",
                "Επιφάνεια",
                "Εναλλαγή ρύθμισης",
                "Λειτουργίες",
                "Σημειώσεις",
                "Κατάσταση τρέχουσας έκδοσης",
                CreateModeFamilyNames(
                    "Οικογένεια native-tooltip",
                    "Οικογένεια overlay",
                    "Υβριδική οικογένεια native / distance-aware",
                    "Οικογένεια quest / native-window"),
                CreateReleaseStatuses("Ενεργό"),
                ["Επιφάνειες διαλόγων και overlay", "Επιφάνειες quest και journal", "Επιφάνειες toast", "Επιφάνειες παραθύρων παιχνιδιού", "Επιφάνειες κόσμου και NamePlate", "Κρυφές ή προσωρινά περιορισμένες επιφάνειες"]),
            ["es"] = Create(
                "Matriz de compatibilidad de superficies de traducción",
                "Familias de modos de traducción",
                "Familia de modos",
                "Superficie",
                "Interruptor de configuración",
                "Modos",
                "Notas",
                "Estado de la versión actual",
                CreateModeFamilyNames(
                    "Familia native-tooltip",
                    "Familia overlay",
                    "Familia híbrida native / distance-aware",
                    "Familia quest / native-window"),
                CreateReleaseStatuses("Activado"),
                ["Superficies de diálogo y overlay", "Superficies de quest y journal", "Superficies de toast", "Superficies de ventanas del juego", "Superficies de mundo y NamePlate", "Superficies ocultas o temporalmente restringidas"]),
            ["eu"] = Create(
                "Itzulpen-gainazalen euskarrien matrizea",
                "Itzulpen-moduen familiak",
                "Modu-familia",
                "Gainazala",
                "Konfigurazio-txandakagailua",
                "Moduak",
                "Oharrak",
                "Uneko bertsioaren egoera",
                CreateModeFamilyNames(
                    "Native-tooltip familia",
                    "Overlay familia",
                    "Native / distance-aware familia hibridoa",
                    "Quest / native-window familia"),
                CreateReleaseStatuses("Aktibatuta"),
                ["Elkarrizketa eta overlay gainazalak", "Quest eta journal gainazalak", "Toast gainazalak", "Joko-leihoen gainazalak", "Mundu eta NamePlate gainazalak", "Ezkutuko edo aldi baterako mugatutako gainazalak"]),
            ["fr"] = Create(
                "Matrice de prise en charge des surfaces de traduction",
                "Familles de modes de traduction",
                "Famille de modes",
                "Surface",
                "Bascule de configuration",
                "Modes",
                "Notes",
                "État de la version actuelle",
                CreateModeFamilyNames(
                    "Famille native-tooltip",
                    "Famille overlay",
                    "Famille hybride native / distance-aware",
                    "Famille quête / native-window"),
                CreateReleaseStatuses("Activé"),
                ["Surfaces de dialogue et d’overlay", "Surfaces de quête et de journal", "Surfaces de toast", "Surfaces des fenêtres du jeu", "Surfaces du monde et NamePlate", "Surfaces cachées ou temporairement restreintes"]),
            ["it"] = Create(
                "Matrice di supporto delle superfici di traduzione",
                "Famiglie di modalità di traduzione",
                "Famiglia di modalità",
                "Superficie",
                "Interruttore di configurazione",
                "Modalità",
                "Note",
                "Stato della release corrente",
                CreateModeFamilyNames(
                    "Famiglia native-tooltip",
                    "Famiglia overlay",
                    "Famiglia ibrida native / distance-aware",
                    "Famiglia quest / native-window"),
                CreateReleaseStatuses("Attivato"),
                ["Superfici di dialogo e overlay", "Superfici quest e journal", "Superfici toast", "Superfici delle finestre di gioco", "Superfici mondo e NamePlate", "Superfici nascoste o temporaneamente limitate"]),
            ["ru"] = Create(
                "Матрица поддержки поверхностей перевода",
                "Семейства режимов перевода",
                "Семейство режимов",
                "Поверхность",
                "Переключатель конфигурации",
                "Режимы",
                "Заметки",
                "Статус текущего релиза",
                CreateModeFamilyNames(
                    "Семейство native-tooltip",
                    "Семейство overlay",
                    "Гибридное семейство native / distance-aware",
                    "Семейство quest / native-window"),
                CreateReleaseStatuses("Включено"),
                ["Диалоговые и overlay-поверхности", "Поверхности quest и journal", "Toast-поверхности", "Поверхности игровых окон", "Поверхности мира и NamePlate", "Скрытые или временно ограниченные поверхности"]),
            ["vi"] = Create(
                "Ma trận hỗ trợ các bề mặt dịch",
                "Nhóm chế độ dịch",
                "Nhóm chế độ",
                "Bề mặt",
                "Công tắc cấu hình",
                "Các chế độ",
                "Ghi chú",
                "Trạng thái phát hành hiện tại",
                CreateModeFamilyNames(
                    "Nhóm native-tooltip",
                    "Nhóm overlay",
                    "Nhóm lai native / distance-aware",
                    "Nhóm quest / native-window"),
                CreateReleaseStatuses("Đã bật"),
                ["Bề mặt hội thoại và overlay", "Bề mặt quest và journal", "Bề mặt toast", "Bề mặt cửa sổ game", "Bề mặt thế giới và NamePlate", "Bề mặt ẩn hoặc bị giới hạn tạm thời"]),
            ["zh-CN"] = Create(
                "翻译界面支持矩阵",
                "翻译模式家族",
                "模式家族",
                "界面",
                "配置开关",
                "模式",
                "说明",
                "当前发布状态",
                CreateModeFamilyNames(
                    "Native-tooltip 家族",
                    "Overlay 家族",
                    "Native / distance-aware 混合家族",
                    "Quest / native-window 家族"),
                CreateReleaseStatuses("已启用"),
                ["对话与 Overlay 界面", "任务与 Journal 界面", "Toast 界面", "游戏窗口界面", "世界与 NamePlate 界面", "隐藏或暂时受限的界面"]),
            ["zh-TW"] = Create(
                "翻譯介面支援矩陣",
                "翻譯模式家族",
                "模式家族",
                "介面",
                "設定開關",
                "模式",
                "說明",
                "目前發行狀態",
                CreateModeFamilyNames(
                    "Native-tooltip 家族",
                    "Overlay 家族",
                    "Native / distance-aware 混合家族",
                    "Quest / native-window 家族"),
                CreateReleaseStatuses("已啟用"),
                ["對話與 Overlay 介面", "任務與 Journal 介面", "Toast 介面", "遊戲視窗介面", "世界與 NamePlate 介面", "隱藏或暫時受限的介面"]),
        };

    /// <summary>
    /// Gets the labels for a declared matrix locale.
    /// </summary>
    /// <param name="locale">The catalog locale identifier.</param>
    /// <returns>The immutable localized labels.</returns>
    public static SupportMatrixLocaleResourceSet ForLocale(string locale) =>
        Resources.TryGetValue(locale, out SupportMatrixLocaleResourceSet? resources)
            ? resources
            : throw new InvalidOperationException($"Unsupported locale '{locale}'.");

    /// <summary>
    /// Gets whether a locale has declared support-matrix label resources.
    /// </summary>
    /// <param name="locale">The catalog locale identifier.</param>
    /// <returns><see langword="true" /> when the locale is supported; otherwise, <see langword="false" />.</returns>
    public static bool IsSupportedLocale(string locale) =>
        Resources.ContainsKey(locale);

    private static SupportMatrixLocaleResourceSet Create(
        string title,
        string modeFamiliesHeading,
        string modeFamilyHeader,
        string surfaceHeader,
        string configToggleHeader,
        string modesHeader,
        string notesHeader,
        string releaseStatusHeader,
        IReadOnlyDictionary<string, string> modeFamilyNames,
        IReadOnlyDictionary<string, string> releaseStatuses,
        IReadOnlyList<string> sectionHeadings)
    {
        string[] sectionIds = ["dialogAndOverlay", "questAndJournal", "toast", "gameWindow", "worldSpaceAndNamePlate", "hiddenOrRestricted"];
        return new SupportMatrixLocaleResourceSet(
            title,
            modeFamiliesHeading,
            modeFamilyHeader,
            surfaceHeader,
            configToggleHeader,
            modesHeader,
            notesHeader,
            releaseStatusHeader,
            modeFamilyNames,
            CanonicalModeLabels,
            releaseStatuses,
            sectionIds.Zip(sectionHeadings).ToDictionary(
                pair => pair.First,
                pair => pair.Second,
                StringComparer.Ordinal));
    }

    private static IReadOnlyDictionary<string, string> CreateModeFamilyNames(
        string nativeTooltip,
        string overlay,
        string nativeDistanceAwareHybrid,
        string questNativeWindow) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["nativeTooltip"] = nativeTooltip,
            ["overlay"] = overlay,
            ["nativeDistanceAwareHybrid"] = nativeDistanceAwareHybrid,
            ["questNativeWindow"] = questNativeWindow,
        };

    private static IReadOnlyDictionary<string, string> CreateReleaseStatuses(string enabled) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Enabled"] = enabled,
            ["N/A"] = "N/A",
        };
}
