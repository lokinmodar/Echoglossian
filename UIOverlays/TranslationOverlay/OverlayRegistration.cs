// <copyright file="OverlayRegistration.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.UIOverlays.TranslationOverlay;

internal class OverlayRegistration
{
  public TranslationOverlay Overlay { get; }

  public TranslationWindowConfig Config { get; }

  public Func<string?>? CustomTitleGetter { get; }

  public OverlayRegistration(
      TranslationOverlay overlay,
      TranslationWindowConfig config,
      Func<string?>? customTitleGetter = null)
  {
    this.Overlay = overlay;
    this.Config = config;
    this.CustomTitleGetter = customTitleGetter;
  }
}
