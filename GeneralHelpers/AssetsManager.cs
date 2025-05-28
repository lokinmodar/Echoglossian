// <copyright file="AssetsManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>
namespace Echoglossian;

public static class AssetsManager
{
  public static List<string> AssetFiles = new();

  public static List<string> MissingAssetFiles = new();

  public static string AssetsPath = string.Empty;

  public static bool PluginAssetsState = false;

  public static bool PluginAssetsDownloaded = false;

  public static void PluginAssetsChecker()
  {
#if DEBUG
    Echoglossian.PluginLog.Debug("Checking Plugin assets!");
#endif

    Echoglossian.NotificationManager.AddNotification(new Notification
    {
      Content = Resources.AssetsCheckingPopupMsg,
      Title = Resources.Name,
      Icon = NotificationUtilities.ToNotificationIcon(Dalamud.Interface.FontAwesomeIcon.Vault),
      Type = NotificationType.Warning,
    });

    foreach (string f in AssetFiles)
    {
#if DEBUG
      Echoglossian.PluginLog.Debug($"Asset file: {f}");
#endif
      if (!File.Exists($"{AssetsPath}{f}"))
      {
#if DEBUG
        Echoglossian.PluginLog.Debug($"Missing file: {f}");
#endif
        MissingAssetFiles.Add(f);
      }
    }

    if (MissingAssetFiles.Count == 0)
    {
      PluginAssetsState = true;
      PluginAssetsDownloaded = true;

      Echoglossian.NotificationManager.AddNotification(new Notification
      {
        Content = Resources.AssetsPresentPopupMsg,
        Title = Resources.Name,
        Icon = NotificationUtilities.ToNotificationIcon(Dalamud.Interface.FontAwesomeIcon.Vault),
        Type = NotificationType.Success,
      });

      return;
    }

    foreach (string f in MissingAssetFiles)
    {
      DownloadPluginAssets(MissingAssetFiles.IndexOf(f), f);
    }

    Echoglossian.NotificationManager.AddNotification(new Notification
    {
      Content = Resources.DownloadingAssetsPopupMsg,
      Title = Resources.Name,
      Icon = NotificationUtilities.ToNotificationIcon(Dalamud.Interface.FontAwesomeIcon.Vault),
      Type = NotificationType.Warning,
    });
  }

  public static void DownloadPluginAssets(int missingAssetIndex, string assetFile)
  {
    Task.Run(() =>
    {
      DownloadAssets(missingAssetIndex);
      MissingAssetFiles.Remove(assetFile);

      if (MissingAssetFiles.Count == 0)
      {
        PluginAssetsState = true;
        PluginAssetsDownloaded = true;

        Echoglossian.NotificationManager.AddNotification(new Notification
        {
          Content = Resources.AssetsPresentPopupMsg,
          Title = Resources.Name,
          Icon = NotificationUtilities.ToNotificationIcon(Dalamud.Interface.FontAwesomeIcon.Vault),
          Type = NotificationType.Success,
        });
      }
    });
  }

  public static void DownloadAssets(int index)
  {
    using HttpClient client = new();

    try
    {
      string path = AssetsPath;
      Uri uri;

      switch (index)
      {
        case 0: // hk
          uri = new Uri("https://github.com/googlefonts/noto-cjk/raw/main/Sans/OTF/TraditionalChineseHK/NotoSansCJKhk-Regular.otf");
          break;
        case 1: // jp
          uri = new Uri("https://github.com/googlefonts/noto-cjk/raw/main/Sans/OTF/Japanese/NotoSansCJKjp-Regular.otf");
          break;
        case 2: // kr
          uri = new Uri("https://github.com/googlefonts/noto-cjk/raw/main/Sans/OTF/Korean/NotoSansCJKkr-Regular.otf");
          break;
        case 3: // sc
          uri = new Uri("https://github.com/googlefonts/noto-cjk/raw/main/Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf");
          break;
        case 4: // tc
          uri = new Uri("https://github.com/googlefonts/noto-cjk/raw/main/Sans/OTF/TraditionalChinese/NotoSansCJKtc-Regular.otf");
          break;
        default:
          throw new ArgumentOutOfRangeException(nameof(index), "Unknown asset index.");
      }

      DownloadFileAsync(client, uri, $"{path}{AssetFiles[index]}").Wait();
      WebClientDownloadCompleted();
    }
    catch (Exception e)
    {
      Echoglossian.PluginLog.Error($"Error downloading plugin assets: {e}");

      Echoglossian.NotificationManager.AddNotification(new Notification
      {
        Content = $"{Resources.AssetsDownloadError1stPart} {AssetFiles[index]} {Resources.AssetsDownloadError2ndPart}",
        Title = Resources.Name,
        Icon = NotificationUtilities.ToNotificationIcon(Dalamud.Interface.FontAwesomeIcon.Vault),
        Type = NotificationType.Error,
      });
    }
  }

  private static async Task DownloadFileAsync(HttpClient client, Uri uri, string filename)
  {
    using var s = await client.GetStreamAsync(uri);
    using var fs = new FileStream(filename, FileMode.CreateNew);
    await s.CopyToAsync(fs);
  }

  private static void WebClientDownloadCompleted()
  {
#if DEBUG
    Echoglossian.PluginLog.Debug("Download finished!");
#endif

    Echoglossian.NotificationManager.AddNotification(new Notification
    {
      Content = Resources.AssetsDownloadComplete,
      Title = Resources.Name,
      Icon = NotificationUtilities.ToNotificationIcon(Dalamud.Interface.FontAwesomeIcon.Vault),
      Type = NotificationType.Success,
    });

    if (MissingAssetFiles.Count == 0)
    {
      PluginAssetsState = true;
      PluginAssetsDownloaded = true;

      Echoglossian.NotificationManager.AddNotification(new Notification
      {
        Content = Resources.AssetsPresentPopupMsg,
        Title = Resources.Name,
        Icon = NotificationUtilities.ToNotificationIcon(Dalamud.Interface.FontAwesomeIcon.Vault),
        Type = NotificationType.Success,
      });
    }
  }
}
