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

namespace Echoglossian
{
  public partial class Echoglossian
  {
    public List<string> AssetFiles = new();

    public List<string> MissingAssetFiles = new();

    private string AssetsPath;

    private void PluginAssetsChecker()
    {
      PluginLog.LogInformation("Checking Plugin assets!");

      foreach (var f in this.AssetFiles)
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
        this.pluginInterface.UiBuilder.AddNotification($"All plugin assets present", "Echoglossian",
          NotificationType.Success, 3000);
        this.SaveConfig();
        return;
      }

      foreach (var f in this.MissingAssetFiles)
      {
        this.DownloadPluginAssets(this.MissingAssetFiles.IndexOf(f));
      }

      this.pluginInterface.UiBuilder.AddNotification($"Downloading extra plugin assets in background", "Echoglossian",
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
          this.pluginInterface.UiBuilder.AddNotification($"All plugin assets present", "Echoglossian",
            NotificationType.Success, 3000);
        }
      }
    }

    private void DownloadAssets(int index)
    {
      using WebClient client = new WebClient();
      try
      {
        var path = this.AssetsPath;

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
        this.pluginInterface.UiBuilder.AddNotification(
            $"Error downloading plugin assets: {this.AssetFiles[index]}",
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
      PluginLog.LogInformation("Download finished!");
      this.pluginInterface.UiBuilder.AddNotification(
        $"Downloading of plugin assets complete!",
        "Echoglossian",
        NotificationType.Success,
        3000);

      if (this.MissingAssetFiles?.Any() != true)
      {
        this.PluginAssetsState = true;
        this.configuration.PluginAssetsDownloaded = true;
        this.pluginInterface.UiBuilder.AddNotification($"All plugin assets present", "Echoglossian",
          NotificationType.Success, 3000);
        this.SaveConfig();
      }
    }
  }
}