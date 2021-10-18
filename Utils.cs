// <copyright file="Utils.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Globalization;
using System.IO;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private Tuple<string, int> LoadFontHelper(int chosenLanguage)
    {
      return new Tuple<string, int>("a", chosenLanguage);
    }

#if DEBUG
    public void ListCultureInfos()
    {
      using StreamWriter logStream = new(this.ConfigDir + "CultureInfos.txt", append: true);

      var cus = CultureInfo.GetCultures(CultureTypes.AllCultures);
      foreach (var cu in cus)
      {
        logStream.WriteLine(cu.ToString());
      }
    }
#endif

    public string MovePathUp(string path, int noOfLevels)
    {
      string parentPath = path.TrimEnd(new[] { '/', '\\' });
      for (int i = 0; i < noOfLevels; i++)
      {
        if (parentPath != null)
        {
          parentPath = Directory.GetParent(parentPath)?.ToString();
        }
      }

      return parentPath;
    }

    private static readonly string[] Codes =
    {
      "af", "sq", "ar", "an", "hy", "roa_rup", "az",
      "eu", "be", "be_x_old", "bn", "bpy", "bs", "br",
      "bg", "my", "zh_yue", "ca", "ceb", "km", "ny",
      "zh-CN", "zh-TW", "co", "hr", "cs", "da", "nl",
      "en", "simple", "eo", "et", "fi", "fr", "gd",
      "gl", "ka", "de", "el", "gu", "ht", "ha", "haw",
      "he", "hi", "hmn", "hu", "is", "ig", "id", "ga",
      "it", "ja", "jv", "kn", "kk", "rw", "ky", "ko",
      "ku", "lo", "la", "lv", "lt", "lmo", "lb", "mk",
      "mg", "ms", "ml", "mt", "mi", "mr", "mn", "ne",
      "no", "nn", "oc", "or", "pa", "fa", "pms", "pl",
      "pt", "ps", "ro", "ru", "sm", "sr", "hbs", "sn",
      "sd", "si", "sk", "sl", "so", "st", "es", "su",
      "sw", "sv", "tl", "tg", "ta", "tt", "te", "th",
      "tr", "tk", "ug", "uk", "ur", "uz", "vi", "vo",
      "war", "cy", "fy", "xh", "yi", "yo", "zu",
    };

    private readonly string[] languages =
    {
      @"Afrikaans; Afrikaans", @"Shqip; Albanian", @"العَرَبِيَّة Al'Arabiyyeẗ; Arabic",
      @"Aragonés; Aragonese", @"Հայերէն Hayerèn; Հայերեն Hayeren; Armenian",
      @"Armãneashce; Armãneashti; Rrãmãneshti; Aromanian; Arumanian; Macedo-Romanian",
      @"AzəRbaycan Dili; آذربایجان دیلی; Азәрбајҹан Дили; Azerbaijani",
      @"Euskara; Basque", @"Беларуская Мова Belaruskaâ Mova; Belarusian",
      @"Беларуская Мова Belaruskaâ Mova; Belarusian",
      @"বাংলা Bāŋlā; Bengali", @"ইমার ঠার;বিষ্ণুপ্রিয়া মণিপুরী Bishnupriya Manipuri Language; Bishnupriya Manipuri",
      @"Bosanski; Bosnian", @"Brezhoneg; Breton", @"Български Език Bălgarski Ezik; Bulgarian",
      @"မြန်မာစာ Mrãmācā; မြန်မာစကား Mrãmākā:; Burmese", @"Cantonese",
      @"Català,Valencià; Catalan; Valencian", @"Sinugbuanong Binisayâ; Cebuano",
      @"ភាសាខ្មែរ PhiəSaakhmær; Central Khmer", @"Chichewa; Chinyanja; Chichewa; Chewa; Nyanja",
      @"中文 Zhōngwén; 汉语; 漢語 Hànyǔ; Chinese", @"中文 Zhōngwén; 汉语; 漢語 Hànyǔ; Chinese",
      @"Corsu; Lingua Corsa; Corsican", @"Hrvatski; Croatian", @"Čeština; Český Jazyk; Czech",
      @"Dansk; Danish", @"Nederlands; Vlaams; Dutch; Flemish", @"English; English", @"English; English",
      @"Esperanto; Esperanto", @"Eesti Keel; Estonian", @"Suomen Kieli; Finnish",
      @"Français; French", @"Gàidhlig; Gaelic; Scottish Gaelic", @"Galego; Galician",
      @"ᲥᲐᲠᲗᲣᲚᲘ Kharthuli; Georgian", @"Deutsch; German", @"Νέα Ελληνικά Néa Ellêniká; Greek, Modern (1453-)",
      @"ગુજરાતી Gujarātī; Gujarati", @"Kreyòl Ayisyen; Haitian; Haitian Creole",
      @"Harshen Hausa; هَرْشَن; Hausa", @"ʻŌlelo HawaiʻI; Hawaiian", @"עברית 'Ivriyþ; Hebrew",
      @"हिन्दी Hindī; Hindi", @"Lus Hmoob; Lug Moob; Lol Hmongb; 𖬇𖬰𖬞 𖬌𖬣𖬵; Hmong; Mong", @"Magyar Nyelv; Hungarian",
      @"Íslenska; Icelandic", @"AsụSụ Igbo; Igbo", @"Bahasa Indonesia; Indonesian", @"Gaeilge; Irish",
      @"Italiano; Lingua Italiana; Italian", @"日本語 Nihongo; Japanese", @"ꦧꦱꦗꦮ ; Basa Jawa; Javanese",
      @"ಕನ್ನಡ Kannađa; Kannada", @"Қазақ Тілі Qazaq Tili; Қазақша Qazaqşa; Kazakh", @"Ikinyarwanda; Kinyarwanda",
      @"Кыргызча Kırgızça; Кыргыз Тили Kırgız Tili; Kirghiz; Kyrgyz", @"한국어 Han'Gug'Ô; Korean", @"Kurdî ; کوردی; Kurdish",
      @"ພາສາລາວ Phasalaw; Lao", @"Lingua Latīna; Latin", @"Latviešu Valoda; Latvian", @"Lietuvių Kalba; Lithuanian",
      @"Lombard", @"Lëtzebuergesch; Luxembourgish; Letzeburgesch", @"Македонски Јазик Makedonski Jazik; Macedonian",
      @"Malagasy", @"Bahasa Melayu; Malay", @"മലയാളം Malayāļã; Malayalam", @"Malti; Maltese", @"Te Reo Māori; Maori",
      @"मराठी Marāţhī; Marathi", @"Монгол Хэл Mongol Xel; ᠮᠣᠩᠭᠣᠯ ᠬᠡᠯᠡ; Mongolian", @"नेपाली भाषा Nepālī Bhāśā; Nepali",
      @"Norsk; Norwegian", @"Norsk Nynorsk; Norwegian Nynorsk; Nynorsk, Norwegian",
      @"Occitan; Lenga D'Òc; Occitan(Post 1500)", @"ଓଡ଼ିଆ; Oriya", @"ਪੰਜਾਬੀ ; پنجابی Pãjābī; Panjabi; Punjabi",
      @"فارسی Fārsiy; Persian", @"Piedmontese", @"Język Polski; Polish", @"Português; Portuguese",
      @"پښتو Pax̌Tow; Pushto; Pashto", @"Limba Română; Romanian; Moldavian; Moldovan",
      @"Русский Язык Russkiĭ Âzık; Russian", @"Gagana FaʻA Sāmoa; Samoan", @"Српски ; Srpski; Serbian",
      @"Serbo-Croatian", @"Chishona; Shona", @"سنڌي ; सिन्धी ; ਸਿੰਧੀ; Sindhi", @"සිංහල Sĩhala; Sinhala; Sinhalese",
      @"Slovenčina; Slovenský Jazyk; Slovak", @"Slovenski Jezik; Slovenščina; Slovenian", @"Af Soomaali; Somali",
      @"Sesotho[Southern]; Sotho, Southern", @"Español; Castellano; Spanish; Castilian", @"ᮘᮞ ᮞᮥᮔ᮪ᮓ ; Basa Sunda; Sundanese",
      @"Kiswahili; Swahili", @"Svenska; Swedish", @"Wikang Tagalog; Tagalog", @"Тоҷикӣ Toçikī; Tajik",
      @"தமிழ் Tamił; Tamil", @"Татар Теле ; Tatar Tele ; تاتار; Tatar", @"తెలుగు Telugu; Telugu",
      @"ภาษาไทย Phasathay; Thai", @"Türkçe; Turkish",
      @"Türkmençe ; Түркменче ; تورکمن تیلی تورکمنچ; Türkmen Dili ; Түркмен Дили; Turkmen",
      @"ئۇيغۇرچە  ; ئۇيغۇر تىلى; Uighur; Uyghur", @"Українська Мова; Українська; Ukrainian",
      @"اُردُو Urduw; Urdu", @"OʻZbekcha ; Ózbekça ; Ўзбекча ; ئوزبېچه; OʻZbek Tili ; Ўзбек Тили ; ئوبېک تیلی; Uzbek",
      @"TiếNg ViệT; Vietnamese", @"Volapük",
      @"Winaray; Samareño; Lineyte-Samarnon; Binisayâ Nga Winaray; Binisayâ Nga Samar-Leyte; Binisayâ Nga Waray; Waray",
      @"Cymraeg; Y Gymraeg; Welsh", @"Frysk; Western Frisian", @"Isixhosa; Xhosa",
      @"ייִדיש; יידיש; אידיש Yidiš; Yiddish", @"Èdè Yorùbá; Yoruba", @"Isizulu; Zulu",
    };

    private void FixConfig()
    {
      if (!this.pluginInterface.ConfigFile.Exists)
      {
        return;
      }

      if (this.configuration.Version != 0)
      {
        this.pluginInterface.ConfigFile.Delete();
        if (this.pluginInterface.ConfigDirectory.Exists)
        {
          this.pluginInterface.ConfigDirectory.Delete(true);
          // this.config = true;
        }
      }

      this.SaveConfig();
    }

    private void SaveConfig()
    {
      // this.configuration.Lang = languageInt;
      this.pluginInterface.SavePluginConfig(this.configuration);
    }

    [Flags]
    public enum TransEngines
    {
      Google = 1 << 0, // Google Translator (free engine)
      Deepl = 1 << 1, // DeepL Translator
      Bing = 1 << 2, // Microsoft Bing Translator (free engine)
      Yandex = 1 << 3, // Yandex Translator
      GTranslate = 1 << 4, // Uses Google, Bing and Yandex (free engines)
      Amazon = 1 << 5, // Amazon Translate
      Azure = 1 << 6, // Microsoft Azure Translate
    }
  }
}