// <copyright file="AssetsManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Net;

using Dalamud.Interface.ImGuiNotification;

using Echoglossian.Properties;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    public List<string> AssetFiles = new();

    public List<string> MissingAssetFiles = new();

    private string assetsPath;

    private void PluginAssetsChecker()
    {
#if DEBUG
      PluginLog.Debug("Checking Plugin assets!");
#endif

      var notification = new Notification
      {
        Content = Resources.AssetsCheckingPopupMsg,
        Title = Resources.Name,
        Icon = NotificationUtilities.ToNotificationIcon(Dalamud.Interface.FontAwesomeIcon.Vault),
        Type = NotificationType.Warning,
      };
      NotificationManager.AddNotification(notification);

      foreach (string f in this.AssetFiles)
      {
#if DEBUG
        PluginLog.Debug($"Asset file:{f}");
#endif
        if (!File.Exists($"{this.assetsPath}{f}"))
        {
#if DEBUG
          PluginLog.Debug($"missing file:{f}");
#endif
          this.MissingAssetFiles.Add(f);
#if DEBUG
          PluginLog.Debug($"missing files list: {this.MissingAssetFiles.ToArray()}");
#endif
        }
      }

      if (this.MissingAssetFiles.Count == 0)
      {
        this.pluginAssetsState = true;
        this.configuration.PluginAssetsDownloaded = true;
        var assetsNotification = new Notification
        {
          Content = Resources.AssetsPresentPopupMsg,
          Title = Resources.Name,
          Icon = NotificationUtilities.ToNotificationIcon(Dalamud.Interface.FontAwesomeIcon.Vault),
          Type = NotificationType.Success,
        };
        NotificationManager.AddNotification(assetsNotification);

        this.SaveConfig();
        return;
      }

      foreach (string f in this.MissingAssetFiles)
      {
        this.DownloadPluginAssets(this.MissingAssetFiles.IndexOf(f), f);
      }

      var assetsDownloadNotification = new Notification
      {
        Content = Resources.DownloadingAssetsPopupMsg,
        Title = Resources.Name,
        Icon = NotificationUtilities.ToNotificationIcon(Dalamud.Interface.FontAwesomeIcon.Vault),
        Type = NotificationType.Warning,
      };
      NotificationManager.AddNotification(assetsDownloadNotification);
    }

    private void DownloadPluginAssets(int missingAssetIndex, string assetFile)
    {
      Task.Run(() =>
      {
        this.DownloadAssets(missingAssetIndex);
        this.MissingAssetFiles.Remove(assetFile);

        if (this.MissingAssetFiles.Count == 0)
        {
          this.pluginAssetsState = true;
          this.configuration.PluginAssetsDownloaded = true;
          this.SaveConfig();

          var assetsSuccessNotification = new Notification
          {
            Content = Resources.AssetsPresentPopupMsg,
            Title = Resources.Name,
            Icon = NotificationUtilities.ToNotificationIcon(Dalamud.Interface.FontAwesomeIcon.Vault),
            Type = NotificationType.Success,
          };

          NotificationManager.AddNotification(assetsSuccessNotification);
          this.config = true;
        }
      });
    }

    private void DownloadAssets(int index)
    {
      using HttpClient client = new HttpClient();

      try
      {
        string path = this.assetsPath;

        Uri uri;
        switch (index)
        {
          case 0: // hk
            uri = new Uri(
              "https://github.com/googlefonts/noto-cjk/raw/main/Sans/OTF/TraditionalChineseHK/NotoSansCJKhk-Regular.otf");
            DownloadFileAsync(client, uri, $"{path}{this.AssetFiles[index]}").Wait();
            this.WebClientDownloadCompleted();
            break;
          case 1: // jp
            uri = new Uri(
              "https://github.com/googlefonts/noto-cjk/raw/main/Sans/OTF/Japanese/NotoSansCJKjp-Regular.otf");
            DownloadFileAsync(client, uri, $"{path}{this.AssetFiles[index]}").Wait();
            this.WebClientDownloadCompleted();
            break;
          case 2: // kr
            uri = new Uri("https://github.com/googlefonts/noto-cjk/raw/main/Sans/OTF/Korean/NotoSansCJKkr-Regular.otf");
            DownloadFileAsync(client, uri, $"{path}{this.AssetFiles[index]}").Wait();
            this.WebClientDownloadCompleted();
            break;
          case 3: // sc
            uri = new Uri(
              "https://github.com/googlefonts/noto-cjk/raw/main/Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf");
            DownloadFileAsync(client, uri, $"{path}{this.AssetFiles[index]}").Wait();
            this.WebClientDownloadCompleted();
            break;
          case 4: // tc
            uri = new Uri(
              "https://github.com/googlefonts/noto-cjk/raw/main/Sans/OTF/TraditionalChinese/NotoSansCJKtc-Regular.otf");
            DownloadFileAsync(client, uri, $"{path}{this.AssetFiles[index]}").Wait();
            this.WebClientDownloadCompleted();
            break;
        }
      }
      catch (Exception e)
      {
        PluginLog.Debug($"Error downloading plugin assets: {e}");

        var assetsErrorNotification = new Notification
        {
          Content = $"{Resources.AssetsDownloadError1stPart} {this.AssetFiles[index]}{Resources.AssetsDownloadError2ndPart}",
          Title = Resources.Name,
          Icon = NotificationUtilities.ToNotificationIcon(Dalamud.Interface.FontAwesomeIcon.Vault),
          Type = NotificationType.Error,
        };

        NotificationManager.AddNotification(assetsErrorNotification);
      }
    }

    private static async Task DownloadFileAsync(HttpClient client, Uri uri, string filename)
    {
      using (var s = await client.GetStreamAsync(uri))
      {
        using (var fs = new FileStream(filename, FileMode.CreateNew))
        {
          await s.CopyToAsync(fs);
        }
      }
    }

    private void WebClientDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
    {
#if DEBUG
      PluginLog.Debug($"Download status: {e.ProgressPercentage}%.");
#endif
    }

    private void WebClientDownloadCompleted()
    {
#if DEBUG
      PluginLog.Debug("Download finished!");
#endif

      var assetsDownloadCompleteNotification = new Notification
      {
        Content = Resources.AssetsDownloadComplete,
        Title = Resources.Name,
        Icon = NotificationUtilities.ToNotificationIcon(Dalamud.Interface.FontAwesomeIcon.Vault),
        Type = NotificationType.Success,
      };

      NotificationManager.AddNotification(assetsDownloadCompleteNotification);

      if (this.MissingAssetFiles.Count == 0)
      {
        this.pluginAssetsState = true;
        this.configuration.PluginAssetsDownloaded = true;

        var assetsSuccessNotification = new Notification
        {
          Content = Resources.AssetsPresentPopupMsg,
          Title = Resources.Name,
          Icon = NotificationUtilities.ToNotificationIcon(Dalamud.Interface.FontAwesomeIcon.Vault),
          Type = NotificationType.Success,
        };

        NotificationManager.AddNotification(assetsSuccessNotification);

        this.SaveConfig();
        this.config = true;
      }
    }
  }
}