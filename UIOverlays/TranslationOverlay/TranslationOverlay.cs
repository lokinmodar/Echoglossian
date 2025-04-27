// <copyright file="TranslationOverlay.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.UIOverlays.TranslationOverlay;

internal class TranslationOverlay
{
  public bool Display { get; set; }

  public string CurrentText { get; set; } = string.Empty;

  public volatile int CurrentTextId;
  public Vector2 Dimensions = Vector2.Zero;
  public Vector2 ImGuiSize = Vector2.Zero;
  public Vector2 Position = Vector2.Zero;

  public SemaphoreSlim Semaphore { get; }

  public TranslationOverlay()
  {
    this.Semaphore = new SemaphoreSlim(1, 1);
  }
}
