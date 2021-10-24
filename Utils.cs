// <copyright file="Utils.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Globalization;
using System.IO;
using System.Drawing;

using System.Drawing.Text;

namespace Echoglossian
{
  public partial class Echoglossian
  {
#if DEBUG
    public void ListCultureInfos()
    {
      using StreamWriter logStream = new(this.configDir + "CultureInfos.txt", append: true);

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

    private readonly string[] languages =
    {
      @"Afrikaans", @"Shqip; Albanian", @"العَرَبِيَّة Al'Arabiyyeẗ; Arabic",
      @"Aragonés; Aragonese", @"Հայերէն Hayerèn; Հայերեն Hayeren; Armenian",
      @"Armãneashce; Armãneashti; Rrãmãneshti; Aromanian; Arumanian; Macedo-Romanian",
      @"Azərbaycan Dili; آذربایجان دیلی; Азәрбајҹан Дили; Azerbaijani",
      @"Euskara; Basque", @"Беларуская Мова Belaruskaâ Mova; Belarusian",
      @"Беларуская Мова Belaruskaâ Mova; Belarusian", @"বাংলা Bāŋlā; Bengali",
      @"ইমার ঠার;বিষ্ণুপ্রিয়া মণিপুরী Bishnupriya Manipuri Language; Bishnupriya Manipuri",
      @"Bosanski; Bosnian", @"Brezhoneg; Breton", @"Български Език Bălgarski Ezik; Bulgarian",
      @"မြန်မာစာ Mrãmācā; မြန်မာစကား Mrãmākā; Burmese", @"广东话; Cantonese",
      @"Català,Valencià; Catalan; Valencian", @"Sinugbuanong Binisayâ; Cebuano",
      @"ភាសាខ្មែរ Phiəsaakhmær; Central Khmer", @"Chichewa; Chinyanja; Chichewa; Chewa; Nyanja",
      @"中文 Zhōngwén; Chinese Simplified", @"汉语; 漢語 Hànyǔ; Chinese Traditional",
      @"Corsu; Lingua Corsa; Corsican", @"Hrvatski; Croatian", @"Čeština; Český Jazyk; Czech",
      @"Dansk; Danish", @"Nederlands; Vlaams; Dutch; Flemish", @"English",
      @"Esperanto; Esperanto", @"Eesti Keel; Estonian", @"Suomen Kieli; Finnish",
      @"Français; French", @"Gàidhlig; Gaelic; Scottish Gaelic", @"Galego; Galician",
      @"ᲥᲐᲠᲗᲣᲚᲘ Kharthuli; Georgian", @"Deutsch; German",
      @"Νέα Ελληνικά Néa Ellêniká; Greek, Modern (1453-)", @"ગુજરાતી Gujarātī; Gujarati",
      @"Kreyòl Ayisyen; Haitian; Haitian Creole", @"Harshen Hausa; هَرْشَن; Hausa",
      @"ʻŌlelo HawaiʻI; Hawaiian", @"עברית 'Ivriyþ; Hebrew", @"हिन्दी Hindī; Hindi",
      @"Magyar Nyelv; Hungarian", @"Íslenska; Icelandic", @"AsụSụ Igbo; Igbo",
      @"Bahasa Indonesia; Indonesian", @"Gaeilge; Irish", @"Italiano; Lingua Italiana; Italian",
      @"日本語 Nihongo; Japanese", @"ꦧꦱꦗꦮ ; Basa Jawa; Javanese", @"ಕನ್ನಡ Kannađa; Kannada",
      @"Қазақ Тілі Qazaq Tili; Қазақша Qazaqşa; Kazakh", @"Ikinyarwanda; Kinyarwanda",
      @"Кыргызча Kırgızça; Кыргыз Тили Kırgız Tili; Kirghiz; Kyrgyz", @"한국어 Han'Gug'Ô; Korean",
      @"Kurdî ; کوردی; Kurdish", @"ພາສາລາວ Phasalaw; Lao", @"Lingua Latīna; Latin",
      @"Latviešu Valoda; Latvian", @"Lietuvių Kalba; Lithuanian", @"Lombard",
      @"Lëtzebuergesch; Luxembourgish; Letzeburgesch", @"Македонски Јазик Makedonski Jazik; Macedonian",
      @"Malagasy", @"Bahasa Melayu; Malay", @"മലയാളം Malayāļã; Malayalam", @"Malti; Maltese",
      @"Te Reo Māori; Maori", @"मराठी Marāţhī; Marathi", @"Монгол Хэл Mongol Xel; ᠮᠣᠩᠭᠣᠯ ᠬᠡᠯᠡ; Mongolian",
      @"नेपाली भाषा Nepālī Bhāśā; Nepali", @"Norsk; Norwegian",
      @"Norsk Nynorsk; Norwegian Nynorsk; Nynorsk, Norwegian", @"Occitan; Lenga D'Òc; Occitan (Post 1500)",
      @"ଓଡ଼ିଆ; Oriya", @"ਪੰਜਾਬੀ ; پنجابی Pãjābī; Panjabi; Punjabi",
      @"فارسی Fārsiy; Persian", @"Piemontèis; Lenga Piemontèisa; Piemontese; Piedmontese",
      @"Język Polski; Polish", @"Português; Portuguese", @"پښتو Pax̌Tow; Pushto; Pashto",
      @"Limba Română; Romanian; Moldavian; Moldovan", @"Русский Язык Russkiĭ Âzık; Russian",
      @"Gagana FaʻA Sāmoa; Samoan", @"Српски ; Srpski; Serbian",
      @"Srpskohrvatski; Hrvatskosrpski; српскохрватски; хрватскосрпски; Serbo-Croatian",
      @"Chishona; Shona", @"سنڌي ; सिन्धी ; ਸਿੰਧੀ; Sindhi", @"සිංහල Sĩhala; Sinhala; Sinhalese",
      @"Slovenčina; Slovenský Jazyk; Slovak", @"Slovenski Jezik; Slovenščina; Slovenian",
      @"Af Soomaali; Somali", @"Sesotho [Southern]; Sotho, Southern", @"Español; Castellano; Spanish; Castilian",
      @"Kiswahili; Swahili", @"Svenska; Swedish", @"Wikang Tagalog; Tagalog", @"Тоҷикӣ Toçikī; Tajik",
      @"தமிழ் Tamił; Tamil", @"Татар Теле ; Tatar Tele ; تاتار; Tatar", @"తెలుగు Telugu; Telugu",
      @"ภาษาไทย Phasathay; Thai", @"Türkçe; Turkish",
      @"Türkmençe ; Түркменче ; تورکمن تیلی تورکمنچ; Türkmen Dili ; Түркмен Дили; Turkmen",
      @"ئۇيغۇرچە; ئۇيغۇر تىلى; Uighur; Uyghur", @"Українська Мова; Українська; Ukrainian",
      @"اُردُو Urduw; Urdu",
      @"OʻZbekcha ; Ózbekça ; Ўзбекча ; ئوزبېچه; OʻZbek Tili ; Ўзбек Тили ; ئوبېک تیلی; Uzbek",
      @"TiếNg ViệT; Vietnamese", @"Volapük",
      @"Winaray; Samareño; Lineyte-Samarnon; Binisayâ Nga Winaray; Binisayâ Nga Samar-Leyte; Binisayâ Nga Waray; Waray",
      @"Cymraeg; Y Gymraeg; Welsh", @"Frysk; Western Frisian",
      @"Isixhosa; Xhosa", @"ייִדיש; יידיש; אידיש Yidiš; Yiddish",
      @"Èdè Yorùbá; Yoruba", @"Isizulu; Zulu",
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

    /// <summary>
    /// Creates an image containing the given text.
    /// NOTE: the image should be disposed after use.
    /// </summary>
    /// <param name="text">Text to draw</param>
    /// <param name="fontOptional">Font to use, defaults to Control.DefaultFont</param>
    /// <param name="textColorOptional">Text color, defaults to Black</param>
    /// <param name="backColorOptional">Background color, defaults to white</param>
    /// <param name="minSizeOptional">Minimum image size, defaults the size required to display the text</param>
    /// <returns>The image containing the text, which should be disposed after use</returns>
    public Image DrawText(string text, Font fontOptional = null, Color? textColorOptional = null, Color? backColorOptional = null, Size? minSizeOptional = null)
    {

      PrivateFontCollection pfc = new PrivateFontCollection();
      pfc.AddFontFile($@"{this.pluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}{this.specialFontFileName}");

      Font font = new Font(pfc.Families[0], this.configuration.FontSize, FontStyle.Regular);
      if (fontOptional != null)
      {
        font = fontOptional;
      }

      Color textColor = Color.White;
      if (textColorOptional != null)
      {
        textColor = (Color)textColorOptional;
      }

      Color backColor = Color.Black;
      if (backColorOptional != null)
      {
        backColor = (Color)backColorOptional;
      }

      Size minSize = Size.Empty;
      if (minSizeOptional != null)
      {
        minSize = (Size)minSizeOptional;
      }

      // first, create a dummy bitmap just to get a graphics object
      SizeF textSize;
      using (Image img = new Bitmap(1, 1))
      {
        using (Graphics drawing = Graphics.FromImage(img))
        {
          // measure the string to see how big the image needs to be
          textSize = drawing.MeasureString(text, font);
          if (!minSize.IsEmpty)
          {
            textSize.Width = textSize.Width > minSize.Width ? textSize.Width : minSize.Width;
            textSize.Height = textSize.Height > minSize.Height ? textSize.Height : minSize.Height;
          }
        }
      }

      // create a new image of the right size
      Image retImg = new Bitmap((int)textSize.Width, (int)textSize.Height);
      using (var drawing = Graphics.FromImage(retImg))
      {
        // paint the background
        drawing.Clear(backColor);

        // create a brush for the text
        using (Brush textBrush = new SolidBrush(textColor))
        {
          drawing.DrawString(text, font, textBrush, 0, 0);
          drawing.Save();
        }
      }

      return retImg;
    }

  }
}