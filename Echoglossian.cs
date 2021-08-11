using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Dalamud.Configuration;
using Dalamud.Game.Command;
using Dalamud.Game.Internal;
using Dalamud.Interface;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using XivCommon;

namespace Echoglossian
{

    //TODO: Handle polish alphabet correctly. 
    //TODO: Add translation database history.
    //TODO: implement multiple fallback translation engines.
    public unsafe partial class Echoglossian : IDalamudPlugin
    {
        public string Name => "Echoglossian";
        private const string SlashCommand = "/eglo";
        private DalamudPluginInterface _pluginInterface;
        private Config _configuration;
        private bool _config;
        private static int _languageInt = 16;
        private UiColorPick _chooser;
        private bool _picker;
        private ExcelSheet<UIColor> _uiColours;
        private List<int> _chosenLanguages;

        SemaphoreSlim talkTranslationSemaphore;
        string talkCurrentTranslation = "";
        bool talkDisplayTranslation = false;
        Vector2 talkTextDimensions = Vector2.Zero;
        Vector2 talkTextPosition = Vector2.Zero;
        Vector2 talkTextImguiSize = Vector2.Zero;
        volatile int talkCurrentTranslationId = 0;

        // When loaded by LivePluginLoader, the executing assembly will be wrong.
        // Supplying this property allows LivePluginLoader to supply the correct location, so that
        // you have full compatibility when loaded normally and through LPL.
        public string AssemblyLocation { get => assemblyLocation; set => assemblyLocation = value; }
        private string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;


        private static readonly string[] Codes =
        {
            "af", "an", "ar", "az", "be_x_old",
            "bg", "bn", "br", "bs",
            "ca", "ceb", "cs", "cy", "da",
            "de", "el", "en", "eo", "es",
            "et", "eu", "fa", "fi", "fr",
            "gl", "he", "hi", "hr", "ht",
            "hu", "hy", "id", "is", "it",
            "ja", "jv", "ka", "kk", "ko",
            "la", "lb", "lt", "lv",
            "mg", "mk", "ml", "mr", "ms",
            "new", "nl", "nn", "no", "oc",
            "pl", "pt", "ro", "roa_rup",
            "ru", "sk", "sl",
            "sq", "sr", "sv", "sw", "ta",
            "te", "th", "tl", "tr", "uk",
            "ur", "vi", "vo", "war", "zh",
            "zh_classical", "zh_yue"
        };

        private readonly string[] _languages =
        {
            "Afrikaans", "Aragonese", "Arabic", "Azerbaijani", "Belarusian",
            "Bulgarian", "Bengali", "Breton", "Bosnian",
            "Catalan; Valencian", "Cebuano", "Czech", "Welsh", "Danish",
            "German", "Greek, Modern", "English", "Esperanto", "Spanish; Castilian",
            "Estonian", "Basque", "Persian", "Finnish", "French",
            "Galician", "Hebrew", "Hindi", "Croatian", "Haitian; Haitian Creole",
            "Hungarian", "Armenian", "Indonesian", "Icelandic", "Italian",
            "Japanese", "Javanese", "Georgian", "Kazakh", "Korean",
            "Latin", "Luxembourgish; Letzeburgesch", "Lithuanian", "Latvian",
            "Malagasy", "Macedonian", "Malayalam", "Marathi", "Malay",
            "Nepal Bhasa; Newari", "Dutch; Flemish", "Norwegian Nynorsk; Nynorsk, Norwegian", "Norwegian",
            "Occitan (post 1500)",
            "Polish", "Portuguese", "Romanian; Moldavian; Moldovan", "Romance languages",
            "Russian", "Slovak", "Slovenian",
            "Albanian", "Serbian", "Swedish", "Swahili", "Tamil",
            "Telugu", "Thai", "Tagalog", "Turkish", "Ukrainian",
            "Urdu", "Vietnamese", "Volapük", "Waray", "Chinese",
            "Chinese Classical", "Chinese yue"
        };

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            _pluginInterface = pluginInterface;
            _configuration = pluginInterface.GetPluginConfig() as Config ?? new Config();

            Common = new XivCommonBase(_pluginInterface, Hooks.Talk | Hooks.BattleTalk);

            Common.Functions.Talk.OnTalk += GetText;
            Common.Functions.BattleTalk.OnBattleTalk += GetBattleText;

            _pluginInterface.UiBuilder.OnBuildUi += EchoglossianConfigUi;
            _pluginInterface.UiBuilder.OnOpenConfigUi += EchoglossianConfig;
            _pluginInterface.CommandManager.AddHandler(SlashCommand, new CommandInfo(Command)
            {
                HelpMessage = "Opens Echoglossian config window"
            });
            _uiColours = pluginInterface.Data.Excel.GetSheet<UIColor>();
            _languageInt = _configuration.Lang;
            _chosenLanguages = _configuration.ChosenLanguages;

            _pluginInterface.Framework.OnUpdateEvent += Tick;
            _pluginInterface.UiBuilder.OnBuildUi += DrawTranslatedText;
            talkTranslationSemaphore = new SemaphoreSlim(1, 1);
        }

        public void Dispose()
        {
            Common.Functions.Talk.OnTalk -= GetText;
            Common.Functions.BattleTalk.OnBattleTalk -= GetBattleText;
            //_pluginInterface.Framework.Gui.Chat.OnChatMessage -= Chat_OnChatMessage;
            _pluginInterface.UiBuilder.OnBuildUi -= EchoglossianConfigUi;
            _pluginInterface.UiBuilder.OnOpenConfigUi -= EchoglossianConfig;
            _pluginInterface.CommandManager.RemoveHandler(SlashCommand);

            _pluginInterface.Framework.OnUpdateEvent -= Tick;
            _pluginInterface.UiBuilder.OnBuildUi -= DrawTranslatedText;
            _pluginInterface.Dispose();
        }

        private void DrawTranslatedText()
        {
            if (_configuration.UseImGui && _configuration.TranslateTalk && talkDisplayTranslation)
            {
                ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(
                    talkTextPosition.X + talkTextDimensions.X / 2 - talkTextImguiSize.X / 2,
                    talkTextPosition.Y - talkTextImguiSize.Y - 20
                    ) + _configuration.ImGuiWindowPosCorrection);
                var size = Math.Min(talkTextDimensions.X * _configuration.ImGuiWindowWidthMult, ImGui.CalcTextSize(talkCurrentTranslation).X + ImGui.GetStyle().WindowPadding.X * 2);
                ImGui.SetNextWindowSizeConstraints(new Vector2(size, 0), new Vector2(size, talkTextDimensions.Y));
                ImGui.Begin("Battle talk translation",
                    ImGuiWindowFlags.NoTitleBar
                    | ImGuiWindowFlags.NoNav
                    | ImGuiWindowFlags.AlwaysAutoResize
                    | ImGuiWindowFlags.NoFocusOnAppearing
                    | ImGuiWindowFlags.NoMouseInputs
                    );
                if (talkTranslationSemaphore.Wait(0))
                {
                    ImGui.TextWrapped(talkCurrentTranslation);
                    talkTranslationSemaphore.Release();
                }
                else
                {
                    ImGui.Text("Awaiting translation...");
                }
                talkTextImguiSize = ImGui.GetWindowSize();
                ImGui.End();
            }
        }

        private void Tick(Framework framework)
        {
            if (_configuration.UseImGui)
            {
                var talk = _pluginInterface.Framework.Gui.GetUiObjectByName("Talk", 1);
                if (talk != IntPtr.Zero)
                {
                    var talkMaster = (AtkUnitBase*)talk;
                    if (talkMaster->IsVisible)
                    {
                        talkDisplayTranslation = true;
                        talkTextDimensions.X = talkMaster->RootNode->Width * talkMaster->Scale;
                        talkTextDimensions.Y = talkMaster->RootNode->Height * talkMaster->Scale;
                        talkTextPosition.X = talkMaster->RootNode->X;
                        talkTextPosition.Y = talkMaster->RootNode->Y;
                    }
                    else
                    {
                        talkDisplayTranslation = false;
                    }
                }
                else
                {
                    talkDisplayTranslation = false;
                }
            }
            else
            {
                talkDisplayTranslation = false;
            }
        }


        private void Command(string command, string arguments)
        {
            _config = true;
        }

        // What to do when plugin install config button is pressed
        private void EchoglossianConfig(object sender, EventArgs args)
        {
            _config = true;
        }
    }

    public class UiColorPick
    {
        public uint Choice { get; set; }
        public uint Option { get; set; }
    }

    public class Config : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public int Lang { get; set; } = 16;

        public bool UseImGui = false;
        public Vector2 ImGuiWindowPosCorrection = Vector2.Zero;
        public bool TranslateTalk = true;
        public float ImGuiWindowWidthMult = 0.85f;
        public bool TranslateBattleTalk = true;

        public List<int> ChosenLanguages { get; set; } = new();
    }
}