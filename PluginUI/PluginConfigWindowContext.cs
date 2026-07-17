// <copyright file="PluginConfigWindowContext.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI;

/// <summary>
/// Provides the configuration and runtime-owned dependencies required to draw
/// the plugin configuration window.
/// </summary>
/// <param name="Configuration">The configuration edited by the window.</param>
/// <param name="Languages">The languages available for translation.</param>
/// <param name="LogoTextureHandle">The plugin logo texture handle.</param>
/// <param name="PixTextureHandle">The Pix QR-code texture handle.</param>
/// <param name="CryptoTextureHandle">The crypto QR-code texture handle.</param>
/// <param name="RebuildTranslationService">
/// Rebuilds translation services after engine settings change.
/// </param>
/// <param name="PluginVersion">The plugin version shown in the title.</param>
public sealed record PluginConfigWindowContext(
    Config Configuration,
    IReadOnlyDictionary<int, LanguageInfo> Languages,
    ImTextureID LogoTextureHandle,
    ImTextureID PixTextureHandle,
    ImTextureID CryptoTextureHandle,
    Action RebuildTranslationService,
    string PluginVersion)
{
    /// <summary>
    /// Gets a value indicating whether logo and QR imagery can be rendered.
    /// </summary>
    public bool ImagesAvailable { get; init; } = true;

    /// <summary>
    /// Gets the host-specific general-font push operation.
    /// </summary>
    internal Func<IDisposable> PushGeneralFont { get; init; } =
        static () => Echoglossian.UINewFontHandler.GeneralFontHandle.Push();

    /// <summary>
    /// Gets the host-specific language runtime update operation.
    /// </summary>
    internal Action<Config, LanguageInfo> ApplyLanguageRuntimeChanges { get; init; } =
        ApplyLiveLanguageRuntimeChanges;

    /// <summary>
    /// Applies the existing live asset and font refresh behavior.
    /// </summary>
    /// <param name="config">The active plugin configuration.</param>
    /// <param name="language">The newly selected language.</param>
    private static void ApplyLiveLanguageRuntimeChanges(
        Config config,
        LanguageInfo language)
    {
        PluginRuntimeLog.Debug("Language selected: " + language.LanguageName);
        PluginRuntimeLog.Debug("Language font: " + language.FontName);

        AssetsManager.RefreshPluginAssetsState(language);
        config.PluginAssetsDownloaded = AssetsManager.PluginAssetsDownloaded;
        Echoglossian.MountFontPaths();
        if (!config.PluginAssetsDownloaded &&
            AssetsManager.RequiresDownloadedAssets(language))
        {
            AssetsManager.PluginAssetsChecker(language);
            PluginAssetRequirementUiHelper.RequestForSelectedLanguage();
        }
        else
        {
            Echoglossian.PluginInterface.UiBuilder.FontAtlas.BuildFontsAsync();
        }
    }
}
