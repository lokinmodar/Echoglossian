// <copyright file="AssetsManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using Dalamud.Interface.Internal.Notifications;
using Dalamud.Logging;
using Echoglossian.Properties;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    public List<string> AssetFiles = new();

    public List<string> MissingAssetFiles = new();

    private string AssetsPath;

    private void PluginAssetsChecker()
    {
#if DEBUG
      PluginLog.LogInformation("Checking Plugin assets!");
#endif
      PluginInterface.UiBuilder.AddNotification(Resources.AssetsCheckingPopupMsg, "Echoglossian",
        NotificationType.Warning, 3000);

      foreach (string f in this.AssetFiles)
      {
#if DEBUG
        PluginLog.LogInformation($"Asset file:{f}");
#endif
        if (!File.Exists($"{this.AssetsPath}{f}"))
        {
#if DEBUG
          PluginLog.LogInformation($"missing file:{f}");
#endif
          this.MissingAssetFiles.Add(f);
#if DEBUG
          PluginLog.LogVerbose($"missing files list: {this.MissingAssetFiles.ToArray()}");
#endif
        }
      }

      if (this.MissingAssetFiles?.Any() != true)
      {
        this.PluginAssetsState = true;
        this.configuration.PluginAssetsDownloaded = true;
        PluginInterface.UiBuilder.AddNotification(Resources.AssetsPresentPopupMsg, "Echoglossian",
          NotificationType.Success, 3000);
        this.SaveConfig();
        return;
      }

      foreach (string f in this.MissingAssetFiles)
      {
        this.DownloadPluginAssets(this.MissingAssetFiles.IndexOf(f));
      }

      PluginInterface.UiBuilder.AddNotification(Resources.DownloadingAssetsPopupMsg, "Echoglossian",
        NotificationType.Warning, 3000);
    }

    private void DownloadPluginAssets(int missingAssetIndex)
    {
      Task assetGrab = Task.Run(() => this.DownloadAssets(missingAssetIndex));
      if (assetGrab.IsCompleted)
      {
        this.MissingAssetFiles.RemoveAt(missingAssetIndex);
        if (this.MissingAssetFiles?.Any() != true)
        {
          this.PluginAssetsState = true;
          this.configuration.PluginAssetsDownloaded = true;
          this.SaveConfig();
          PluginInterface.UiBuilder.AddNotification(Resources.AssetsPresentPopupMsg, "Echoglossian",
            NotificationType.Success, 3000);
          this.config = true;
        }
      }
    }

    private void DownloadAssets(int index)
    {
      using WebClient client = new WebClient();
      try
      {
        string path = this.AssetsPath;

        Uri uri;
        switch (index)
        {
          case 0: // hk
            uri = new Uri(
              "https://github.com/googlefonts/noto-cjk/raw/main/Sans/OTF/TraditionalChineseHK/NotoSansCJKhk-Regular.otf");
            client.DownloadFileAsync(uri, $"{path}{this.AssetFiles[index]}");
            client.DownloadProgressChanged += this.WebClientDownloadProgressChanged;
            client.DownloadDataCompleted += this.WebClientDownloadCompleted;
            break;
          case 1: // jp
            uri = new Uri(
              "https://github.com/googlefonts/noto-cjk/raw/main/Sans/OTF/Japanese/NotoSansCJKjp-Regular.otf");
            client.DownloadFileAsync(uri, $"{path}{this.AssetFiles[index]}");
            client.DownloadProgressChanged += this.WebClientDownloadProgressChanged;
            client.DownloadDataCompleted += this.WebClientDownloadCompleted;
            break;
          case 2: // kr
            uri = new Uri("https://github.com/googlefonts/noto-cjk/raw/main/Sans/OTF/Korean/NotoSansCJKkr-Regular.otf");
            client.DownloadFileAsync(uri, $"{path}{this.AssetFiles[index]}");
            client.DownloadProgressChanged += this.WebClientDownloadProgressChanged;
            client.DownloadDataCompleted += this.WebClientDownloadCompleted;
            break;
          case 3: // sc
            uri = new Uri(
              "https://github.com/googlefonts/noto-cjk/raw/main/Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf");
            client.DownloadFileAsync(uri, $"{path}{this.AssetFiles[index]}");
            client.DownloadProgressChanged += this.WebClientDownloadProgressChanged;
            client.DownloadDataCompleted += this.WebClientDownloadCompleted;
            break;
          case 4: // tc
            uri = new Uri(
              "https://github.com/googlefonts/noto-cjk/raw/main/Sans/OTF/TraditionalChinese/NotoSansCJKtc-Regular.otf");
            client.DownloadFileAsync(uri, $"{path}{this.AssetFiles[index]}");
            client.DownloadProgressChanged += this.WebClientDownloadProgressChanged;
            client.DownloadDataCompleted += this.WebClientDownloadCompleted;
            break;
        }
      }
      catch (Exception e)
      {
        PluginLog.LogError($"Error downloading plugin assets: {e}");
        PluginInterface.UiBuilder.AddNotification(
            $"{Resources.AssetsDownloadError1stPart} {this.AssetFiles[index]}{Resources.AssetsDownloadError2ndPart}",
            "Echoglossian",
            NotificationType.Error,
            3000);
      }
    }

    private void WebClientDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
    {
#if DEBUG
      PluginLog.LogInformation($"Download status: {e.ProgressPercentage}%.");
#endif
    }

    private void WebClientDownloadCompleted(object sender, DownloadDataCompletedEventArgs e)
    {
#if DEBUG
      PluginLog.LogInformation("Download finished!");
#endif
      PluginInterface.UiBuilder.AddNotification(
        Resources.AssetsDownloadComplete,
        "Echoglossian",
        NotificationType.Success,
        3000);

      if (this.MissingAssetFiles?.Any() != true)
      {
        this.PluginAssetsState = true;
        this.configuration.PluginAssetsDownloaded = true;
        PluginInterface.UiBuilder.AddNotification(Resources.AssetsPresentPopupMsg, "Echoglossian",
          NotificationType.Success, 3000);
        this.SaveConfig();
        this.config = true;
      }
    }
  }
}