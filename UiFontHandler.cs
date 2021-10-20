// <copyright file="UiFontHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Dalamud.Logging;
using ImGuiNET;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    public bool FontLoaded;
    public bool FontLoadFailed;
    public ImFontPtr UiFont;

    private void LoadFont()
    {
      // TODO: Get font by languageint
      var fontFileName = "";




#if DEBUG
      PluginLog.LogVerbose("Inside LoadFont method");

      var fontFile = $@"{this.pluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-Medium-Custom2.otf";
      PluginLog.LogVerbose($"Font file in DEBUG Mode: {fontFile}");

#else

      var fontFile = $@"{this.pluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-Regular.ttf";
      PluginLog.LogVerbose($"Font file in Prod Mode: {fontFile}");
#endif
      this.FontLoaded = false;
      if (File.Exists(fontFile))
      {
        try
        {
          unsafe
          {
            var io = ImGui.GetIO();
            List<ushort> chars = new();

            var builder = new ImFontGlyphRangesBuilderPtr(ImGuiNative.ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder());
            builder.AddText("\"──᭠ ᠆֊⸗־・﹣－･᐀‧⁃⸚⹀゠𐺭··ˑ·ּ᛫•‧∘∙⋅⏺●◦⚫⦁⸰⸳⸱・ꞏ･ּ・･᛫⸰··⸱⸳𐄁•‧∘∙⋅⏺●◦⦁⚫ˑꞏ•‣⁃⁌⁍∙○◘◦☙❥❧⦾⦿«»‘’‚‛“”„‟‹›꙰꙱꙲꙼꙽꙯;·꙳꙾΄˜˘˙΅˚˝˛ʹ͵ʺ˂˃˄˅ˆˇˈˉˊˋˌˍˎˏ˒˓˔˕˖˗˞˟˥˦˧˨˩˪˫ˬ˭˯˰˱˲˳˴˵˶˷˸˹˺˻˼˽˾˿҂϶҈҉҆҅҄҇◌҃ːˑĂăǍǎǺǻǞǟȦȧǠǡĄąĀāȀȁȂȃǼǽǢǣȺḂḃƀɃƁƂƃĆćĈĉČčĊċȻȼƇƈĎďḊḋĐđȸǱǲǳǄǅǆƉƊƋƌɗȡĔĕĚěĖėȨȩĘęĒēȄȅȆȇɆɇƎǝƏəƐḞḟƑƒǴǵĞğĜĝǦǧĠġĢģǤǥƓƔˠƢƣʰĤĥȞȟĦħƕǶʱʻʽĬĭǏǐĨĩİĮįĪīȈȉȊȋĲĳıƗƖʲĴĵǰȷɈɉǨǩĶķƘƙˡĹĺĽľĻļŁłĿŀǇǈǉƚȽȴƛṀṁŃńǸǹŇňŅņǊǋǌƝƞȠȵŊŋŎŏǑǒȪȫŐőȬȭȮȯȰȱǾǿǪǫǬǭŌōȌȍȎȏƠơŒœƆƟȢȣṖṗƤƥȹɊɋĸʳŔŕŘřŖŗȐȑȒȓƦɌɍʴʵɼʶˢŚśŜŝŠšṠṡŞşȘșſẛȿƩƪŤťṪṫŢţȚțƾŦŧȾƫƬƭƮȶŬŭǓǔŮůǗǘǛǜǙǚǕǖŰűŨũŲųŪūȔȕȖȗƯưɄƜƱƲɅʷẂẃẀẁŴŵẄẅˣʸỲỳŶŷŸȲȳɎɏƳƴȜȝŹźŽžŻżƍƵƶȤȥɀƷʒǮǯƸƹƺƿǷƻƧƨƼƽƄƅɁɂˀʼˮʾˤʿˁǀǁǂǃΑαΆάΒβϐΓγΔδΕεϵΈέϜϝͶͷϚϛΖζͰͱΗηΉήΘθϑϴͺΙιΊίΪϊΐͿϳΚκϰϏϗΛλΜμΝνΞξΟοΌόΠπϖϺϻϞϟϘϙΡρϱϼΣςσϲϹͼϾͻϽͽϿΤτΥυϒΎύϓΫϋϔΰΦφϕΧχΨψΩωΏώϠϡͲͳϷϸϢϣϤϥϦϧϨϩϪϫϬϭϮϯАаⷶӐӑӒӓӘәӚӛӔӕБбⷠВвⷡГгⷢЃѓҐґҒғӺӻҔҕӶӷДдⷣԀԁꚀꚁЂђꙢꙣԂԃҘҙЕеⷷЀѐӖӗЁёЄєꙴЖжⷤӁӂӜӝԪԫꚄꚅҖҗЗзⷥӞӟꙀꙁԄԅԐԑꙂꙃЅѕꙄꙅӠӡꚈꚉԆԇꚂꚃИиꙵЍѝӤӥӢӣҊҋІіЇїꙶꙆꙇЙйЈјⷸꙈꙉКкⷦЌќҚқӃӄҠҡҞҟҜҝԞԟԚԛЛлⷧӅӆԮԯԒԓԠԡЉљꙤꙥԈԉԔԕМмⷨӍӎꙦꙧНнⷩԨԩӉӊҢңӇӈԢԣҤҥЊњԊԋОоⷪꙨꙩꙪꙫꙬꙭꙮꚘꚙꚚꚛӦӧӨөӪӫПпⷫԤԥҦҧҀҁРрⷬҎҏԖԗСсⷭⷵԌԍҪҫТтⷮꚌꚍԎԏҬҭꚊꚋЋћУуꙷЎўӰӱӲӳӮӯҮүҰұⷹꙊꙋѸѹФфꚞХхⷯӼӽӾӿҲҳҺһԦԧꚔꚕѠѡꙻѾѿꙌꙍѼѽѺѻЦцⷰꙠꙡꚎꚏҴҵꚐꚑЧчⷱӴӵԬԭꚒꚓҶҷӋӌҸҹꚆꚇҼҽҾҿЏџШшⷲꚖꚗЩщⷳꙎꙏꙿЪъꙸꚜꙐꙑЫыꙹӸӹЬьꙺꚝҌҍѢѣⷺꙒꙓЭэӬӭЮюⷻꙔꙕⷼꙖꙗЯяԘԙѤѥꚟѦѧⷽꙘꙙѪѫⷾꙚꙛѨѩꙜꙝѬѭⷿѮѯѰѱѲѳⷴѴѵѶѷꙞꙟҨҩԜԝӀӏ");

            builder.BuildRanges(out ImVector ranges);

            // if (this.configuration.Lang == )
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesDefault());
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesVietnamese());
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesCyrillic());
            this.AddCharsFromIntPtr(chars, (ushort*)ranges.Data);

            var addChars = string.Join(string.Empty, chars.Select(c => new string((char)c, 2))).Select(c => (ushort)c).ToArray();
            chars.AddRange(addChars);

            chars.Add(0);

            var arr = chars.ToArray();

            fixed (ushort* ptr = &arr[0])
            {
              this.UiFont = ImGui.GetIO().Fonts.AddFontFromFileTTF(fontFile, this.configuration.FontSize /*imguiFontSize*/, null, new IntPtr((void*)ptr));
            }

#if DEBUG
            // PluginLog.Debug($"Glyphs pointer: {neededGlyphs}");
#endif
            // this.UiFont = ImGui.GetIO().Fonts.AddFontFromFileTTF(fontFile, this.configuration.FontSize /*imguiFontSize*/, null, rangeHandle.AddrOfPinnedObject());
#if DEBUG
            PluginLog.Debug($"UiFont Data size: {ImGui.GetIO().Fonts.Fonts.Size}");
#endif
            this.FontLoaded = true;
#if DEBUG
            PluginLog.Debug($"Font loaded? {this.FontLoaded}");
#endif
          }
        }
        catch (Exception ex)
        {
          PluginLog.Log($"Font failed to load. {fontFile}");
          PluginLog.Log(ex.ToString());
          this.FontLoadFailed = true;
        }
      }
      else
      {
        PluginLog.Log($"Font doesn't exist. {fontFile}");
        this.FontLoadFailed = true;
      }
    }


    public bool ConfigFontLoaded;
    public bool ConfigFontLoadFailed;
    public ImFontPtr ConfigUiFont;

    private void LoadConfigFont()
    {
#if DEBUG
      PluginLog.LogVerbose("Inside LoadConfigFont method");
      var fontFile = $@"{this.pluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-Medium-Custom2.otf";
      PluginLog.LogVerbose($"Font file in DEBUG Mode: {fontFile}");
#else

      var fontFile = $@"{this.pluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-Regular.ttf";
      PluginLog.LogVerbose($"Font file in Prod Mode: {fontFile}");
#endif
      this.ConfigFontLoaded = false;
      if (File.Exists(fontFile))
      {
        try
        {
          unsafe
          {
            var io = ImGui.GetIO();
            List<ushort> chars = new();

            var builder = new ImFontGlyphRangesBuilderPtr(ImGuiNative.ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder());
            builder.AddText(
              "Afrikaans; Afrikaans Shqip; Albanian العَرَبِيَّة Al'Arabiyyeẗ; Arabic Aragonés; Aragonese Հայերէն Hayerèn; Հայերեն Hayeren; Armenian Armãneashce; Armãneashti; Rrãmãneshti; Aromanian; Arumanian; Macedo-Romanian Azərbaycan Dili; آذربایجان دیلی; Азәрбајҹан Дили; Azerbaijani Euskara; Basque Беларуская Мова Belaruskaâ Mova; Belarusian Беларуская Мова Belaruskaâ Mova; Belarusian বাংলা Bāŋlā; Bengali ইমার ঠার/বিষ্ণুপ্রিয়া মণিপুরী Bishnupriya Manipuri Language; Bishnupriya Manipuri Bosanski; Bosnian Brezhoneg; Breton Български Език Bălgarski Ezik; Bulgarian မြန်မာစာ Mrãmācā; မြန်မာစကား Mrãmākā:; Burmese ; Cantonese Català,Valencià; Catalan; Valencian Sinugbuanong Binisayâ; Cebuano ភាសាខ្មែរ Phiəsaakhmær; Central Khmer Chichewa; Chinyanja; Chichewa; Chewa; Nyanja 中文 Zhōngwén; 汉语; 漢語 Hànyǔ; Chinese 中文 Zhōngwén; 汉语; 漢語 Hànyǔ; Chinese Corsu; Lingua Corsa; Corsican Hrvatski; Croatian Čeština; Český Jazyk; Czech Dansk; Danish Nederlands; Vlaams; Dutch; Flemish English; English English; English Esperanto; Esperanto Eesti Keel; Estonian Suomen Kieli; Finnish Français; French Gàidhlig; Gaelic; Scottish Gaelic Galego; Galician Ქართული Kharthuli; Georgian Deutsch; German Νέα Ελληνικά Néa Ellêniká; Greek, Modern (1453-) ગુજરાતી Gujarātī; Gujarati Kreyòl Ayisyen; Haitian; Haitian Creole Harshen Hausa; هَرْشَن; Hausa ʻōlelo Hawaiʻi; Hawaiian עברית 'Ivriyþ; Hebrew हिन्दी Hindī; Hindi Lus Hmoob; Lug Moob; Lol Hmongb; Hmong; Mong Magyar Nyelv; Hungarian Íslenska; Icelandic Asụsụ Igbo; Igbo Bahasa Indonesia; Indonesian Gaeilge; Irish Italiano; Lingua Italiana; Italian 日本語 Nihongo; Japanese ꦧꦱꦗꦮ  Basa Jawa; Javanese ಕನ್ನಡ Kannađa; Kannada Қазақ Тілі Qazaq Tili; Қазақша Qazaqşa; Kazakh Ikinyarwanda; Kinyarwanda Кыргызча Kırgızça; Кыргыз Тили Kırgız Tili; Kirghiz; Kyrgyz 한국어 Han'Gug'Ô; Korean Kurdî  کوردی; Kurdish ພາສາລາວ Phasalaw; Lao Lingua Latīna; Latin Latviešu Valoda; Latvian Lietuvių Kalba; Lithuanian ; Lombard Lëtzebuergesch; Luxembourgish; Letzeburgesch Македонски Јазик Makedonski Jazik; Macedonian ; Malagasy Bahasa Melayu; Malay മലയാളം Malayāļã; Malayalam Malti; Maltese Te Reo Māori; Maori मराठी Marāţhī; Marathi Монгол Хэл Mongol Xel; ᠮᠣᠩᠭᠣᠯ ᠬᠡᠯᠡ; Mongolian नेपाली भाषा Nepālī Bhāśā; Nepali Norsk; Norwegian Norsk Nynorsk; Norwegian Nynorsk; Nynorsk, Norwegian Occitan; Lenga D'Òc; Occitan (Post 1500) ଓଡ଼ିଆ; Oriya ਪੰਜਾਬੀ  پنجابی Pãjābī; Panjabi; Punjabi فارسی Fārsiy; Persian ; Piedmontese Język Polski; Polish Português; Portuguese پښتو Pax̌Tow; Pushto; Pashto Limba Română; Romanian; Moldavian; Moldovan Русский Язык Russkiĭ Âzık; Russian Gagana Faʻa Sāmoa; Samoan Српски  Srpski; Serbian ; Serbo-Croatian Chishona; Shona سنڌي  सिन्धी  ਸਿੰਧੀ; Sindhi සිංහල Sĩhala; Sinhala; Sinhalese Slovenčina; Slovenský Jazyk; Slovak Slovenski Jezik; Slovenščina; Slovenian Af Soomaali; Somali Sesotho [Southern]; Sotho, Southern Español; Castellano; Spanish; Castilian Basa Sunda; Sundanese Kiswahili; Swahili Svenska; Swedish Wikang Tagalog; Tagalog Тоҷикӣ Toçikī; Tajik தமிழ் Tamił; Tamil Татар Теле  Tatar Tele  تاتار; Tatar తెలుగు Telugu; Telugu ภาษาไทย Phasathay; Thai Türkçe; Turkish Türkmençe  Түркменче  تورکمن تیلی تورکمنچ; Türkmen Dili  Түркмен Дили; Turkmen ئۇيغۇرچە  ; ئۇيغۇر تىلى; Uighur; Uyghur Українська Мова; Українська; Ukrainian اُردُو Urduw; Urdu Oʻzbekcha  Ózbekça  Ўзбекча  ئوزبېچه; Oʻzbek Tili  Ўзбек Тили  ئوبېک تیلی; Uzbek Tiếng Việt; Vietnamese ; Volapük Winaray; Samareño; Lineyte-Samarnon; Binisayâ Nga Winaray; Binisayâ Nga Samar-Leyte; “Binisayâ Nga Waray”; Waray Cymraeg; Y Gymraeg; Welsh Frysk; Western Frisian Isixhosa; Xhosa ייִדיש; יידיש; אידיש Yidiš; Yiddish ");
            builder.BuildRanges(out ImVector ranges);

            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesVietnamese());
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesCyrillic());
            this.AddCharsFromIntPtr(chars, (ushort*)ranges.Data);

            var addChars = string.Join(string.Empty, chars.Select(c => new string((char)c, 2))).Select(c => (ushort)c).ToArray();
            chars.AddRange(addChars);

            chars.Add(0);

            var arr = chars.ToArray();

            fixed (ushort* ptr = &arr[0])
            {
              this.ConfigUiFont = ImGui.GetIO().Fonts.AddFontFromFileTTF(fontFile, 15.0f, null, new IntPtr((void*)ptr));
            }

#if DEBUG
            // PluginLog.Debug($"Glyphs pointer: {neededGlyphs}");


#endif
            // this.UiFont = ImGui.GetIO().Fonts.AddFontFromFileTTF(fontFile, this.configuration.FontSize /*imguiFontSize*/, null, rangeHandle.AddrOfPinnedObject());
#if DEBUG
            PluginLog.Debug($"ConfigUiFont data size: {ImGui.GetIO().Fonts.Fonts.Size}");
#endif
            this.ConfigFontLoaded = true;
#if DEBUG
            PluginLog.Debug($"Config Font loaded? {this.ConfigFontLoaded}");
#endif
          }
        }
        catch (Exception ex)
        {
          PluginLog.Log($"Config Font failed to load. {fontFile}");
          PluginLog.Log(ex.ToString());
          this.ConfigFontLoadFailed = true;
        }
      }
      else
      {
        PluginLog.Log($"Config Font doesn't exist. {fontFile}");
        this.ConfigFontLoadFailed = true;
      }
    }

    private unsafe void AddCharsFromIntPtr(List<ushort> chars, ushort* ptr)
    {
      while (*ptr != 0)
      {
        chars.Add(*ptr);
        ptr++;
      }
    }
  }
}