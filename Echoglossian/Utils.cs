﻿// <copyright file="Utils.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Drawing;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Logging;
using Dalamud.Utility;
using Echoglossian.Properties;
using Lumina.Data;

namespace Echoglossian
{
  public partial class Echoglossian
  {
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

#if DEBUG
    public void ListCultureInfos()
    {
      using StreamWriter logStream = new(this.configDir + "CultureInfos.txt", true);

      var cus = CultureInfo.GetCultures(CultureTypes.AllCultures);
      foreach (var cu in cus)
      {
        logStream.WriteLine(cu.ToString());
      }
    }
#endif

    public string MovePathUp(string path, int noOfLevels)
    {
      var parentPath = path.TrimEnd('/', '\\');
      for (var i = 0; i < noOfLevels; i++)
      {
        if (parentPath != null)
        {
          parentPath = Directory.GetParent(parentPath)?.ToString();
        }
      }

      return parentPath;
    }

    private void ResetSettings()
    {
      this.configuration.Lang = 28;

      this.configuration.FontSize = 24;

      this.configuration.ShowInCutscenes = true;

      this.configuration.TranslateBattleTalk = false;
      this.configuration.TranslateTalk = false;
      this.configuration.TranslateTalkSubtitle = false;
      this.configuration.TranslateToast = false;
      this.configuration.TranslateNpcNames = false;
      this.configuration.TranslateErrorToast = false;
      this.configuration.TranslateQuestToast = false;
      this.configuration.TranslateAreaToast = false;
      this.configuration.TranslateClassChangeToast = false;
      this.configuration.TranslateWideTextToast = false;
      this.configuration.TranslateYesNoScreen = false;
      this.configuration.TranslateCutSceneSelectString = false;
      this.configuration.TranslateSelectString = false;
      this.configuration.TranslateSelectOk = false;
      this.configuration.TranslateToDoList = false;
      this.configuration.TranslateScenarioTree = false;
      this.configuration.TranslateTooltips = false;
      this.configuration.TranslateJournal = false;

      this.configuration.UseImGuiForTalk = false;
      this.configuration.UseImGuiForBattleTalk = false;
      this.configuration.UseImGuiForToasts = false;
      this.configuration.SwapTextsUsingImGui = false;
      this.configuration.ChosenTransEngine = 0;
      this.configuration.Version = 4;

      this.SaveConfig();
      PluginInterface.UiBuilder.AddNotification(Resources.SettingsReset, this.Name, NotificationType.Info, 5000);
    }

    private void FixConfig()
    {
#if DEBUG
      PluginLog.LogDebug($"Config File Version: {this.configuration.Version}");
#endif

      if (!File.Exists($"{PluginInterface.ConfigFile.FullName}"))
      {
#if DEBUG
        PluginLog.LogDebug($"Inside config file fixer - Config File Info: {PluginInterface.ConfigFile.FullName}");
#endif

        this.SaveConfig();
        return;
      }

      if (this.configuration.Version.CompareTo(4) != 0)
      {
        PluginInterface.ConfigFile.Delete();
        /* if (pluginInterface.ConfigDirectory.Exists)
         {
           pluginInterface.ConfigDirectory.Delete(true);
           // this.config = true;
         }*/
        this.SaveConfig();
        this.ResetSettings();
        PluginInterface.GetPluginConfig();
      }
    }

    private void SaveConfig()
    {
      // this.configuration.Lang = this.configuration.Lang;
      PluginInterface.SavePluginConfig(this.configuration);
    }

    /// <summary>
    ///   Creates an image containing the given text.
    ///   NOTE: the image should be disposed after use.
    /// </summary>
    /// <param name="text">Text to draw.</param>
    /// <param name="fontOptional">Font to use, defaults to Control.DefaultFont.</param>
    /// <param name="textColorOptional">Text color, defaults to Black.</param>
    /// <param name="backColorOptional">Background color, defaults to white.</param>
    /// <param name="minSizeOptional">Minimum image size, defaults the size required to display the text.</param>
    /// <returns>The image containing the text, which should be disposed after use.</returns>
    private Image DrawText(
      string text,
      Font fontOptional = null,
      Color? textColorOptional = null,
      Color? backColorOptional = null,
      Size? minSizeOptional = null)
    {
#if DEBUG
      PluginLog.LogVerbose("Inside image creation method");
#endif
      PrivateFontCollection pfc = new();
      pfc.AddFontFile(
        $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}{this.specialFontFileName}");

      Font font = new(pfc.Families[0], this.configuration.FontSize, FontStyle.Regular);
      if (fontOptional != null)
      {
        font = fontOptional;
      }

      var textColor = Color.White;
      if (textColorOptional != null)
      {
        textColor = (Color)textColorOptional;
      }

      var backColor = Color.Black;
      if (backColorOptional != null)
      {
        backColor = (Color)backColorOptional;
      }

      var minSize = Size.Empty;
      if (minSizeOptional != null)
      {
        minSize = (Size)minSizeOptional;
      }

      // first, create a dummy bitmap just to get a graphics object
      SizeF textSize;
      using (Image img = new Bitmap(1, 1))
      {
        using (var drawing = Graphics.FromImage(img))
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
      Image textAsImage = new Bitmap((int)textSize.Width, (int)textSize.Height);
      using (var drawing = Graphics.FromImage(textAsImage))
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
#if DEBUG
      PluginLog.LogVerbose("Before returning the image created");
#endif
      return textAsImage;
    }

    /// <summary>
    ///   Converts Image to byte array.
    /// </summary>
    /// <param name="image">Image to be converted.</param>
    /// <returns>Byte array to be used elsewhere.</returns>
    private static byte[] TranslationImageConverter(Image image)
    {
#if DEBUG
      PluginLog.LogVerbose("Conversion to byte");
#endif
      var imageConverter = new ImageConverter();
      return (byte[])imageConverter.ConvertTo(image, typeof(byte[])) ?? throw new InvalidOperationException();
    }

    private static string FormatText(string text)
    {
#if DEBUG
      PluginLog.LogVerbose($"Text to break in lines: {text}");
#endif
      if (text.IsNullOrWhitespace())
      {
        return string.Empty;
      }
      var newLinePayload = new NewLinePayload().Text;
#if DEBUG
      PluginLog.LogVerbose($"New Line payload: {newLinePayload}");
#endif
      var regex = new Regex(@"(.{1,64})(?:\s|$)"); // .{0,70}\S(?=$|\s)
      var formattedText = regex.Matches(text)
        .Select(m => m.Groups[1].Value).ToList()
        .Select(s => string.Join(s, newLinePayload)).ToList()
        .Aggregate((current, next) => current + next);
#if DEBUG
      PluginLog.LogVerbose($"Formatted text: {formattedText}");
#endif

      return formattedText;
    }

    private static string ConvertClientLanguageToLangCode(Language currentClientLanguage)
    {
      return currentClientLanguage switch
      {
        Language.English => "en",
        Language.German => "de",
        Language.French => "fr",
        Language.Japanese => "ja",
        Language.Korean => "ko",
        Language.None => "simple",
        Language.ChineseSimplified => "zh-CN",
        Language.ChineseTraditional => "zh-TW",
        _ => throw new ArgumentOutOfRangeException(nameof(currentClientLanguage), currentClientLanguage, Resources.Echoglossian_ConvertClientLanguageToLangCode_Language_not_supported)
      };
    }
  }
}

