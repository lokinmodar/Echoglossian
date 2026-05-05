// <copyright file="AssetsManagerTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Xunit;
using Echoglossian.LanguagesHandling;

namespace Echoglossian.Tests;

public class AssetsManagerTests
{
  [Fact]
  public void RequiresDownloadedAssets_ReturnsFalse_ForBundledFont()
  {
    AssetsManager.AssetFiles =
    [
        "NotoSansCJKhk-Regular.otf",
        "NotoSansCJKjp-Regular.otf",
        "NotoSansCJKkr-Regular.otf",
        "NotoSansCJKsc-Regular.otf",
        "NotoSansCJKtc-Regular.otf",
    ];

    var languageInfo = new LanguageInfo(
        "en",
        "English",
        "NotoSans-Medium.ttf",
        string.Empty,
        []);

    Assert.False(AssetsManager.RequiresDownloadedAssets(languageInfo));
    Assert.Empty(AssetsManager.GetRequiredAssetFiles(languageInfo));
  }

  [Fact]
  public void RequiresDownloadedAssets_ReturnsTrue_ForExternalCjkFont()
  {
    AssetsManager.AssetFiles =
    [
        "NotoSansCJKhk-Regular.otf",
        "NotoSansCJKjp-Regular.otf",
        "NotoSansCJKkr-Regular.otf",
        "NotoSansCJKsc-Regular.otf",
        "NotoSansCJKtc-Regular.otf",
    ];

    var languageInfo = new LanguageInfo(
        "ja",
        "Japanese",
        "NotoSansCJKjp-Regular.otf",
        string.Empty,
        []);

    Assert.True(AssetsManager.RequiresDownloadedAssets(languageInfo));
    Assert.Equal(
        ["NotoSansCJKjp-Regular.otf"],
        AssetsManager.GetRequiredAssetFiles(languageInfo));
  }

  [Fact]
  public void AreRequiredAssetsPresent_ReturnsExpectedResult_ForCurrentLanguage()
  {
    var tempDirectory = Directory.CreateTempSubdirectory();

    try
    {
      AssetsManager.AssetFiles =
      [
          "NotoSansCJKhk-Regular.otf",
          "NotoSansCJKjp-Regular.otf",
          "NotoSansCJKkr-Regular.otf",
          "NotoSansCJKsc-Regular.otf",
          "NotoSansCJKtc-Regular.otf",
      ];
      AssetsManager.AssetsPath = tempDirectory.FullName;

      var languageInfo = new LanguageInfo(
          "ko",
          "Korean",
          "NotoSansCJKkr-Regular.otf",
          string.Empty,
          []);

      Assert.False(AssetsManager.AreRequiredAssetsPresent(languageInfo));

      File.WriteAllText(
          Path.Combine(tempDirectory.FullName, "NotoSansCJKkr-Regular.otf"),
          "placeholder");

      Assert.True(AssetsManager.AreRequiredAssetsPresent(languageInfo));
    }
    finally
    {
      tempDirectory.Delete(recursive: true);
    }
  }
}
