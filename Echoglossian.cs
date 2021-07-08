using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NTextCat;

namespace Echoglossian
{
    public class Echoglossian : IDisposable
    {
        private Plugin Plugin;

        internal readonly List<XivChatType> _order = new List<XivChatType>
        {
            XivChatType.None,
            XivChatType.Debug,
            XivChatType.Urgent,
            XivChatType.Notice,
            XivChatType.Say,
            XivChatType.Shout,
            XivChatType.TellOutgoing,
            XivChatType.TellIncoming,
            XivChatType.Party,
            XivChatType.Alliance,
            XivChatType.Ls1,
            XivChatType.Ls2,
            XivChatType.Ls3,
            XivChatType.Ls4,
            XivChatType.Ls5,
            XivChatType.Ls6,
            XivChatType.Ls7,
            XivChatType.Ls8,
            XivChatType.FreeCompany,
            XivChatType.NoviceNetwork,
            XivChatType.CustomEmote,
            XivChatType.StandardEmote,
            XivChatType.Yell,
            XivChatType.CrossParty,
            XivChatType.PvPTeam,
            XivChatType.CrossLinkShell1,
            XivChatType.Echo,
            XivChatType.SystemMessage,
            XivChatType.SystemError,
            XivChatType.GatheringSystemMessage,
            XivChatType.ErrorMessage,
            XivChatType.RetainerSale,
            XivChatType.CrossLinkShell2,
            XivChatType.CrossLinkShell3,
            XivChatType.CrossLinkShell4,
            XivChatType.CrossLinkShell5,
            XivChatType.CrossLinkShell6,
            XivChatType.CrossLinkShell7,
            XivChatType.CrossLinkShell8
        };

        internal readonly string[] _orderString =
        {
            "None",
            "Debug",
            "Urgent",
            "Notice",
            "Say",
            "Shout",
            "TellOutgoing",
            "TellIncoming",
            "Party",
            "Alliance",
            "Ls1",
            "Ls2",
            "Ls3",
            "Ls4",
            "Ls5",
            "Ls6",
            "Ls7",
            "Ls8",
            "FreeCompany",
            "NoviceNetwork",
            "CustomEmote",
            "StandardEmote",
            "Yell",
            "CrossParty",
            "PvPTeam",
            "CrossLinkShell1",
            "Echo",
            "SystemMessage",
            "SystemError",
            "GatheringSystemMessage",
            "ErrorMessage",
            "RetainerSale",
            "CrossLinkShell2",
            "CrossLinkShell3",
            "CrossLinkShell4",
            "CrossLinkShell5",
            "CrossLinkShell6",
            "CrossLinkShell7",
            "CrossLinkShell8"
        };

        internal readonly bool[] _yesNo = {
            false, false, false, false, true,
            true, false, true, true, true,
            true, true, true, true, true,
            true, true, true, true, true,
            true, false, true, true, true,
            true, true, false, false, false,
            false, false, true, true, true,
            true, true, true, true
        };


        internal readonly string[] _codes = {
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

        internal readonly string[] _languages = {
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
            "Nepal Bhasa; Newari", "Dutch; Flemish", "Norwegian Nynorsk; Nynorsk, Norwegian", "Norwegian", "Occitan (post 1500)",
            "Polish", "Portuguese", "Romanian; Moldavian; Moldovan", "Romance languages",
            "Russian", "Slovak", "Slovenian",
            "Albanian", "Serbian", "Swedish", "Swahili", "Tamil",
            "Telugu", "Thai", "Tagalog", "Turkish", "Ukrainian",
            "Urdu", "Vietnamese", "Volapük", "Waray", "Chinese",
            "Chinese Classical", "Chinese yue"
        };

        // NCat
        private static readonly RankedLanguageIdentifierFactory Factory = new();
        private static readonly RankedLanguageIdentifier Identifier = Factory.Load(Path.Combine(AssemblyDirectory, "Wiki82.profile.xml"));

        public Echoglossian(Plugin plugin)
        {
            this.Plugin = plugin;
        }

        private static string AssemblyDirectory
        {
            get
            {
                var codeBase = typeof(Echoglossian).Assembly.CodeBase;
                var uri = new UriBuilder(codeBase);
                var path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }

        public string Lang(string message)
        {
            var languages = Identifier.Identify(message);
            var mostCertainLanguage = languages.FirstOrDefault();
            return mostCertainLanguage != null ? mostCertainLanguage.Item1.Iso639_2T : "The language could not be identified with an acceptable degree of certainty";
        }

        public string Translate(string text)
        {
            try
            {
                var lang = "pt";
                var url = "https://clients5.google.com/translate_a/t?client=dict-chrome-ex&sl=auto&tl=" + lang + "&q=" + text;
                PluginLog.LogDebug($"URL {url}");
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.104 Safari/537.36";

                var requestResult = request.GetResponse();
                var reader = new StreamReader(requestResult.GetResponseStream() ?? throw new Exception());
                var read = reader.ReadToEnd();
                PluginLog.LogError($"read: {read}");
                var parsed = JObject.Parse(read);
                var trans = ((JArray)parsed["sentences"])?[0]["trans"];

                //TODO: Join all "trans" sentences


                PluginLog.Fatal($"trans {trans.ToString()}");
                var src = ((JValue)parsed["src"]);
                Debug.Assert(trans != null, nameof(trans) + " != null");
                if (src != null && (src.ToString(CultureInfo.InvariantCulture) == lang || trans.ToString() == text))
                {
                    return "LOOP";
                }
                return trans.ToString();
            }
            catch (Exception e)
            {
                PluginLog.Error(e.ToString());
                throw;
            }
            
        }

        public void Dispose()
        {
            Plugin?.Dispose();
            throw new NotImplementedException();
        }
    }
}