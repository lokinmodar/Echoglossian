// <copyright file="AssetsManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>
namespace Echoglossian;

public static class AssetsManager
{
  private const int DownloadRetryCount = 3;
  private static readonly object MissingAssetFilesLock = new();
  private static readonly IReadOnlyDictionary<string, string> AssetDownloadUris =
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["NotoSansCJKhk-Regular.otf"] =
            "https://github.com/googlefonts/noto-cjk/raw/main/Sans/OTF/TraditionalChineseHK/NotoSansCJKhk-Regular.otf",
        ["NotoSansCJKjp-Regular.otf"] =
            "https://github.com/googlefonts/noto-cjk/raw/main/Sans/OTF/Japanese/NotoSansCJKjp-Regular.otf",
        ["NotoSansCJKkr-Regular.otf"] =
            "https://github.com/googlefonts/noto-cjk/raw/main/Sans/OTF/Korean/NotoSansCJKkr-Regular.otf",
        ["NotoSansCJKsc-Regular.otf"] =
            "https://github.com/googlefonts/noto-cjk/raw/main/Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf",
        ["NotoSansCJKtc-Regular.otf"] =
            "https://github.com/googlefonts/noto-cjk/raw/main/Sans/OTF/TraditionalChinese/NotoSansCJKtc-Regular.otf",
      };
  public static List<string> AssetFiles = new();

  public static List<string> MissingAssetFiles = new();

  public static string AssetsPath = string.Empty;

  public static bool PluginAssetsState = false;

  public static bool PluginAssetsDownloaded = false;

  /// <summary>
  /// Determines whether a font file name depends on a downloaded external asset.
  /// </summary>
  /// <param name="fontFileName">The font file name to inspect.</param>
  /// <returns><c>true</c> if the font requires a downloaded asset; otherwise, <c>false</c>.</returns>
  public static bool RequiresDownloadedAsset(string? fontFileName)
  {
    return !string.IsNullOrWhiteSpace(fontFileName) &&
           AssetFiles.Contains(fontFileName, StringComparer.OrdinalIgnoreCase);
  }

  /// <summary>
  /// Determines whether the specified language requires a downloaded font asset.
  /// </summary>
  /// <param name="languageInfo">The language to inspect.</param>
  /// <returns><c>true</c> if the language depends on a downloaded asset; otherwise, <c>false</c>.</returns>
  public static bool RequiresDownloadedAssets(LanguageInfo? languageInfo)
  {
    return languageInfo is not null &&
           RequiresDownloadedAsset(languageInfo.FontName);
  }

  /// <summary>
  /// Returns the downloaded font assets required by the specified language.
  /// </summary>
  /// <param name="languageInfo">The language to inspect.</param>
  /// <returns>The required downloaded asset file names.</returns>
  public static IReadOnlyList<string> GetRequiredAssetFiles(
      LanguageInfo? languageInfo)
  {
    if (!RequiresDownloadedAssets(languageInfo))
    {
      return [];
    }

    return [languageInfo!.FontName];
  }

  /// <summary>
  /// Determines whether all downloaded assets required by the specified language are present.
  /// </summary>
  /// <param name="languageInfo">The language to inspect.</param>
  /// <returns><c>true</c> when every required downloaded asset exists; otherwise, <c>false</c>.</returns>
  public static bool AreRequiredAssetsPresent(LanguageInfo? languageInfo)
  {
    foreach (var assetFile in GetRequiredAssetFiles(languageInfo))
    {
      if (!File.Exists(Path.Combine(AssetsPath, assetFile)))
      {
        return false;
      }
    }

    return true;
  }

  /// <summary>
  /// Returns whether the specified language currently has one or more missing
  /// required downloaded assets.
  /// </summary>
  /// <param name="languageInfo">The language to inspect.</param>
  /// <returns>
  /// <c>true</c> when the language requires downloaded assets and at least one
  /// required file is missing; otherwise, <c>false</c>.
  /// </returns>
  public static bool HasMissingRequiredAssets(LanguageInfo? languageInfo)
  {
    return RequiresDownloadedAssets(languageInfo) &&
           !AreRequiredAssetsPresent(languageInfo);
  }

  /// <summary>
  /// Recomputes the runtime asset state for the specified language.
  /// </summary>
  /// <param name="languageInfo">The language to inspect.</param>
  public static void RefreshPluginAssetsState(LanguageInfo? languageInfo)
  {
    var assetsReady = !RequiresDownloadedAssets(languageInfo) ||
                      AreRequiredAssetsPresent(languageInfo);
    PluginAssetsState = assetsReady;
    PluginAssetsDownloaded = assetsReady;
  }

  /// <summary>
  /// Returns the manual download URI for a known plugin asset file.
  /// </summary>
  /// <param name="assetFileName">The asset file name.</param>
  /// <returns>The manual download URI, or <see langword="null" /> when unknown.</returns>
  public static string? GetAssetDownloadUri(string assetFileName)
  {
    return AssetDownloadUris.TryGetValue(assetFileName, out var uri)
        ? uri
        : null;
  }

  /// <summary>
  /// Opens the configured plugin font-asset directory in the system shell.
  /// </summary>
  public static void OpenAssetsDirectory()
  {
    Directory.CreateDirectory(AssetsPath);
    Process.Start(
        new ProcessStartInfo
        {
          FileName = AssetsPath,
          UseShellExecute = true,
        });
  }

  /// <summary>
  /// Opens the manual download links for all downloaded assets required by the
  /// specified language.
  /// </summary>
  /// <param name="languageInfo">The language whose assets should be opened.</param>
  public static void OpenRequiredAssetDownloadLinks(LanguageInfo? languageInfo)
  {
    foreach (var assetFile in GetRequiredAssetFiles(languageInfo))
    {
      var uri = GetAssetDownloadUri(assetFile);
      if (string.IsNullOrWhiteSpace(uri))
      {
        continue;
      }

      Process.Start(
          new ProcessStartInfo
          {
            FileName = uri,
            UseShellExecute = true,
          });
    }
  }

  /// <summary>
  /// Checks whether the plugin font assets required by the specified language are available.
  /// </summary>
  /// <param name="languageInfo">The language that may require downloaded assets.</param>
  public static void PluginAssetsChecker(LanguageInfo? languageInfo = null)
  {
#if DEBUG
    PluginRuntimeLog.Debug("Checking Plugin assets!");
#endif

    var assetFilesToCheck = languageInfo is null
        ? AssetFiles
        : [.. GetRequiredAssetFiles(languageInfo)];

    if (assetFilesToCheck.Count == 0)
    {
      PluginAssetsState = true;
      PluginAssetsDownloaded = true;
      return;
    }

    lock (MissingAssetFilesLock)
    {
      MissingAssetFiles.Clear();
    }

    Echoglossian.NotificationManager.AddNotification(new Notification
    {
      Content = Resources.AssetsCheckingPopupMsg,
      Title = Resources.Name,
      Icon = NotificationUtilities.ToNotificationIcon(Dalamud.Interface.FontAwesomeIcon.Vault),
      Type = NotificationType.Warning,
    });

    foreach (string f in assetFilesToCheck)
    {
#if DEBUG
      PluginRuntimeLog.Debug($"Asset file: {f}");
#endif
      if (!File.Exists($"{AssetsPath}{f}"))
      {
#if DEBUG
        PluginRuntimeLog.Debug($"Missing file: {f}");
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

    foreach (string f in MissingAssetFiles.ToArray())
    {
      var assetIndex = AssetFiles.IndexOf(f);
      if (assetIndex < 0)
      {
        PluginRuntimeLog.Warning(
            $"Unknown asset file in missing assets list: {f}");
        continue;
      }

      DownloadPluginAssets(assetIndex, f);
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
      var downloadSucceeded = DownloadAssets(missingAssetIndex);

      bool allAssetsDownloaded;
      lock (MissingAssetFilesLock)
      {
        if (downloadSucceeded)
        {
          MissingAssetFiles.Remove(assetFile);
        }

        allAssetsDownloaded = MissingAssetFiles.Count == 0;
        PluginAssetsState = allAssetsDownloaded;
        PluginAssetsDownloaded = allAssetsDownloaded;
      }

      if (allAssetsDownloaded)
      {
        Echoglossian.MountFontPaths();
        Echoglossian.PluginInterface.UiBuilder.FontAtlas.BuildFontsAsync();
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

  /// <summary>
  /// Downloads a plugin font asset by index with bounded retry behavior.
  /// </summary>
  /// <param name="index">The asset index to download.</param>
  /// <returns><c>true</c> if the asset was downloaded successfully; otherwise, <c>false</c>.</returns>
  public static bool DownloadAssets(int index)
  {
    using HttpClient client = new();

    string path = AssetsPath;
    if (index < 0 || index >= AssetFiles.Count)
    {
      throw new ArgumentOutOfRangeException(nameof(index), "Unknown asset index.");
    }

    var uriText = GetAssetDownloadUri(AssetFiles[index]);
    if (string.IsNullOrWhiteSpace(uriText))
    {
      throw new ArgumentOutOfRangeException(nameof(index), "Unknown asset index.");
    }

    var uri = new Uri(uriText);

    Directory.CreateDirectory(path);
    var targetFilePath = Path.Combine(path, AssetFiles[index]);

    if (File.Exists(targetFilePath))
    {
      return true;
    }

    for (var attempt = 1; attempt <= DownloadRetryCount; attempt++)
    {
      try
      {
        DownloadFileAsync(client, uri, targetFilePath).Wait();
        WebClientDownloadCompleted();
        return true;
      }
      catch (Exception e)
      {
        PluginRuntimeLog.Warning(
            $"Error downloading plugin assets (attempt {attempt}/{DownloadRetryCount}): {e}");

        if (attempt >= DownloadRetryCount)
        {
          Echoglossian.NotificationManager.AddNotification(new Notification
          {
            Content = $"{Resources.AssetsDownloadError1stPart} {AssetFiles[index]} {Resources.AssetsDownloadError2ndPart}",
            Title = Resources.Name,
            Icon = NotificationUtilities.ToNotificationIcon(Dalamud.Interface.FontAwesomeIcon.Vault),
            Type = NotificationType.Error,
          });
          return false;
        }

        Thread.Sleep(TimeSpan.FromSeconds(attempt));
      }
    }

    return false;
  }

  private static async Task DownloadFileAsync(HttpClient client, Uri uri, string filename)
  {
    using var s = await client.GetStreamAsync(uri);
    using var fs = new FileStream(filename, FileMode.Create);
    await s.CopyToAsync(fs);
  }

  private static void WebClientDownloadCompleted()
  {
#if DEBUG
    PluginRuntimeLog.Debug("Download finished!");
#endif

    Echoglossian.NotificationManager.AddNotification(new Notification
    {
      Content = Resources.AssetsDownloadComplete,
      Title = Resources.Name,
      Icon = NotificationUtilities.ToNotificationIcon(Dalamud.Interface.FontAwesomeIcon.Vault),
      Type = NotificationType.Success,
    });
  }
}



