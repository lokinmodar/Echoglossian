// <copyright file="OverlayRegistration.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.UIOverlays.TranslationOverlay;

internal class OverlayRegistration
{
  private readonly TranslationWindowConfig config;

  private readonly Func<TranslationWindowConfig>? configFactory;

  public TranslationOverlay Overlay { get; }

  public TranslationWindowConfig Config =>
      this.configFactory?.Invoke() ?? this.config;

  public Func<string?>? CustomTitleGetter { get; }

  public Func<bool>? IsEnabled { get; }

  public Func<bool>? SyncBeforeDraw { get; }

  public OverlayRegistration(
      TranslationOverlay overlay,
      TranslationWindowConfig config,
      Func<string?>? customTitleGetter = null,
      Func<bool>? isEnabled = null,
      Func<bool>? syncBeforeDraw = null)
  {
    this.Overlay = overlay;
    this.config = config;
    this.CustomTitleGetter = customTitleGetter;
    this.IsEnabled = isEnabled;
    this.SyncBeforeDraw = syncBeforeDraw;
  }

  public OverlayRegistration(
      TranslationOverlay overlay,
      Func<TranslationWindowConfig> configFactory,
      Func<string?>? customTitleGetter = null,
      Func<bool>? isEnabled = null,
      Func<bool>? syncBeforeDraw = null)
  {
    this.Overlay = overlay;
    this.config = configFactory();
    this.configFactory = configFactory;
    this.CustomTitleGetter = customTitleGetter;
    this.IsEnabled = isEnabled;
    this.SyncBeforeDraw = syncBeforeDraw;
  }
}
