// <copyright file="PluginUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

public partial class Echoglossian
{
  public static bool LangToRemoveDiacritics;

  /// <summary>
  /// Draws the Echoglossian configuration UI through the shared renderer.
  /// </summary>
  private void EchoglossianConfigUi()
  {
    var context = new PluginConfigWindowContext(
        this.configuration,
        this.languagesDictionary,
        this.logo.Handle,
        this.pixImage.Handle,
        this.cryptoImage.Handle,
        this.RebuildTranslationServiceSafely,
        this.configuration.PluginVersion);

    this.configWindowRenderer.Draw(context, ref this.config);
  }

  /// <summary>
  /// Disables every toast-translation category and saves the configuration.
  /// </summary>
  /// <returns><see langword="true" /> after the settings are disabled.</returns>
  private bool DisableAllToastTranslations()
  {
    this.configuration.TranslateAreaToast = false;
    this.configuration.TranslateClassChangeToast = false;
    this.configuration.TranslateErrorToast = false;
    this.configuration.TranslateQuestToast = false;
    this.configuration.TranslateWideTextToast = false;
    SaveConfig(this.configuration);
    return true;
  }
}
