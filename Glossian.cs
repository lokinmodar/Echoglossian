﻿using System;
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
using Newtonsoft.Json.Linq;
 using NTextCat;
 using XivCommon;

 namespace Echoglossian
{
    public partial class Echoglossian
    {
        private static XivCommonBase Common { get; set; }

        private const string GTranslateUrl =
            "https://clients5.google.com/translate_a/t?client=dict-chrome-ex&sl=auto&tl=";

        private const string UaString =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.104 Safari/537.36";

        private static string LangIdentErrorMessage =
            "The language could not be identified with an acceptable degree of certainty";

        #region NTextCat Support
   
        private static readonly RankedLanguageIdentifierFactory Factory = new RankedLanguageIdentifierFactory();
        private static readonly RankedLanguageIdentifier Identifier = Factory.Load(Path.Combine(AssemblyDirectory, "Wiki82.profile.xml"));

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
        #endregion
        public static string Lang(string message)
        {
            PluginLog.LogInformation($"message in Lang Method {message}");
            var languages = Identifier.Identify(message);
            var mostCertainLanguage = languages.FirstOrDefault();
            PluginLog.LogInformation($"most Certain language: {mostCertainLanguage?.ToString()}");
            return mostCertainLanguage != null
                ? mostCertainLanguage.Item1.Iso639_2T
                : LangIdentErrorMessage;
        }

        public static string Translate(string text)
        {
            try
            {
                var lang = _codes[_languageInt];
                PluginLog.LogInformation($"LANG: {lang}");

                var url = $"{GTranslateUrl}{lang}&q={text}";

                PluginLog.LogInformation($"URL: {url}");

                var request = (HttpWebRequest)WebRequest.Create(url);
                request.UserAgent = UaString;
                var requestResult = request.GetResponse();

                var reader = new StreamReader(requestResult.GetResponseStream() ?? throw new Exception());
                var read = reader.ReadToEnd();

                var parsed = JObject.Parse(read);

                var dialogueSentenceList = parsed.SelectTokens("sentences[*].trans").Select(i => (string)i).ToList();

                var finalDialogueText = dialogueSentenceList.Aggregate("", (current, dialogueSentence) => current + dialogueSentence);

                var src = ((JValue)parsed["src"]);
                Debug.Assert(finalDialogueText != null, nameof(finalDialogueText) + " != null");
                PluginLog.LogInformation($"FinalDialogueText: {finalDialogueText}");
                PluginLog.LogInformation($"SRC: {src}");
                PluginLog.LogInformation($"SRC Cultureinfo: {src?.ToString(CultureInfo.InvariantCulture)}");
                PluginLog.LogInformation($"LANG: {lang}");

                if (src != null && (src.ToString(CultureInfo.InvariantCulture) == lang || finalDialogueText == text))
                {
                    return text;
                }
                return finalDialogueText;

            }
            catch (Exception e)
            {
                PluginLog.Error(e.ToString());
                throw;
            }
        }

/*        private SeString Tran(SeString messageSeString)
        {
            var cleanMessage = new SeString(new List<Payload>());
            cleanMessage.Append(messageSeString);
            var originalMessage = new SeString(new List<Payload>());
            originalMessage.Append(messageSeString);

            var run = true;
            if (messageSeString.Payloads.Count < 2) { }
            else if (messageSeString.Payloads[0] == new UIForegroundPayload(_pluginInterface.Data, 48) && messageSeString.Payloads[1] == new UIForegroundPayload(_pluginInterface.Data, 0))
            {
                PluginLog.Log("Caught loop B");
                run = false;
            }

            if (!run) return messageSeString;
            var tranDone = false;

            for (var i = 0; i < messageSeString.Payloads.Count; i++)
            {
                if (messageSeString.Payloads[i].Type == PayloadType.MapLink ||
                    messageSeString.Payloads[i].Type == PayloadType.Item ||
                    messageSeString.Payloads[i].Type == PayloadType.Quest)
                {
                    i += 7;
                    continue;
                }
                if (messageSeString.Payloads[i].Type == PayloadType.Player)
                {
                    i += 2;
                    continue;
                }
                if (messageSeString.Payloads[i].Type == PayloadType.Status)
                {
                    i += 10;
                    continue;
                }

                if (messageSeString.Payloads[i].Type != PayloadType.RawText) continue;
                //PluginLog.Log("Type PASS");
                var text = (TextPayload)messageSeString.Payloads[i];
                var translatedText = Translate(text.Text);
                if (translatedText == "LOOP") continue;
                messageSeString.Payloads[i] = new TextPayload(translatedText);
                messageSeString.Payloads.Insert(i, new UIForegroundPayload(_pluginInterface.Data, (ushort)_textColour[0].Option));
                messageSeString.Payloads.Insert(i, new UIForegroundPayload(_pluginInterface.Data, 0));

                if (i + 3 < messageSeString.Payloads.Count)
                    messageSeString.Payloads.Insert(i + 3, new UIForegroundPayload(_pluginInterface.Data, 0));
                else
                    messageSeString.Payloads.Append(new UIForegroundPayload(_pluginInterface.Data, 0));
                i += 2;
                tranDone = true;
            }

            if (!tranDone) return messageSeString;
            // Adding to the rolling "last translation" list
            _lastTranslations.Insert(0,cleanMessage.TextValue);
            if(_lastTranslations.Count > 10) _lastTranslations.RemoveAt(10);
            
            if (_tranMode == 0) // Append
            {
                var tranMessage = new SeString(new List<Payload>());
                tranMessage.Payloads.Add(new TextPayload(" | "));
                //originalMessage.Payloads.Insert(0, new UIForegroundPayload(_pluginInterface.Data, 0));
                //originalMessage.Payloads.Insert(0, new UIForegroundPayload(_pluginInterface.Data, 48));
                originalMessage.Append(tranMessage);
                originalMessage.Append(messageSeString);
                return originalMessage;
                //PrintChat(type, senderName, originalMessage);
            }
            return messageSeString;

        }*/
    }
}
