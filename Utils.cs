// <copyright file="Utils.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Drawing;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Dalamud.Logging;

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
      string parentPath = path.TrimEnd('/', '\\');
      for (int i = 0; i < noOfLevels; i++)
      {
        if (parentPath != null)
        {
          parentPath = Directory.GetParent(parentPath)?.ToString();
        }
      }

      return parentPath;
    }

    private static async Task<bool> DownloadPluginAssets()
    {
      using WebClient webClient = new();
      try
      {
        await webClient.DownloadFileTaskAsync(
          new Uri("https://drive.google.com/file/d/10XDzXGxheebckhFL0Rbuq36C-ssUSd4P/view?usp=sharing"),
          "Wiki82.profile.xml");
        return true;
      }
      catch (Exception e)
      {
        PluginLog.LogError($"Error downloading plugin assets: {e}");
        return false;
      }
    }

    private void FixConfig()
    {
      if (!this.pluginInterface.ConfigFile.Exists)
      {
        return;
      }

      if (this.configuration.Version != 1)
      {
        this.pluginInterface.ConfigFile.Delete();
        /* if (this.pluginInterface.ConfigDirectory.Exists)
         {
           this.pluginInterface.ConfigDirectory.Delete(true);
           // this.config = true;
         }*/
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
    /// <param name="text">Text to draw.</param>
    /// <param name="fontOptional">Font to use, defaults to Control.DefaultFont.</param>
    /// <param name="textColorOptional">Text color, defaults to Black.</param>
    /// <param name="backColorOptional">Background color, defaults to white.</param>
    /// <param name="minSizeOptional">Minimum image size, defaults the size required to display the text.</param>
    /// <returns>The image containing the text, which should be disposed after use.</returns>
    public Image DrawText(string text, Font fontOptional = null, Color? textColorOptional = null, Color? backColorOptional = null, Size? minSizeOptional = null)
    {
#if DEBUG
      PluginLog.LogWarning("Inside image creation method");
#endif
      PrivateFontCollection pfc = new();
      pfc.AddFontFile($@"{this.pluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}{this.specialFontFileName}");

      Font font = new(pfc.Families[0], this.configuration.FontSize, FontStyle.Regular);
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
      PluginLog.LogWarning("Before returning the image created");
#endif
      return textAsImage;
    }

    /// <summary>
    /// Converts Image to byte array.
    /// </summary>
    /// <param name="image">Image to be converted.</param>
    /// <returns>Byte array to be used elsewhere.</returns>
    private byte[] TranslationImageConverter(Image image)
    {
#if DEBUG
      PluginLog.LogWarning("Conversion to byte");
#endif
      ImageConverter imageConverter = new ImageConverter();
      return (byte[])imageConverter.ConvertTo(image, typeof(byte[]));
    }
  }
}