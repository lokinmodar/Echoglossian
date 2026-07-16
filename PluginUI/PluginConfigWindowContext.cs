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
    string PluginVersion);
