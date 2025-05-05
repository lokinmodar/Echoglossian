// <copyright file="TranslationOverlayDrawer.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian
{
  public partial class Echoglossian
  {
    /// <summary>
    /// Starts tracking the overlay for a specific addon.
    /// </summary>
    /// <param name="addonName">Addon name to be tracked.</param>
    /// <param name="overlay">Overlay to be used.</param>
    /// <param name="shouldShowOverlay">If the overlay should be shown.</param>
    /// <param name="shouldStopOverlay">If the overlay should be hidden.</param>
    private void StartOverlayTracking(
    string addonName,
    TranslationOverlay overlay,
    Func<bool> shouldShowOverlay,
    Func<bool> shouldStopOverlay = null)
    {
      Task.Run(() =>
      {
        PluginLog.Debug($"StartOverlayTracking: {addonName}");
        IntPtr addonPtr = GameGuiInterface.GetAddonByName(addonName, 1);
        if (addonPtr == IntPtr.Zero)
        {
          PluginLog.Debug($"StartOverlayTracking: {addonName} not found.");
          return;
        }

        unsafe
        {
          AtkUnitBase* addon = (AtkUnitBase*)addonPtr;
          while (addon->IsVisible && (shouldStopOverlay == null || !shouldStopOverlay()))
          {
            if (shouldShowOverlay())
            {
              overlay.Position = new Vector2(addon->RootNode->X, addon->RootNode->Y);
              overlay.Dimensions = new Vector2(addon->RootNode->Width * addon->Scale, addon->RootNode->Height * addon->Scale);
              overlay.Display = true;
            }

            Thread.Sleep(100);
          }

          overlay.Display = false;
        }
      });
    }


    /// <summary>
    /// Draws the translation window.
    /// </summary>
    /// <param name="overlay">Overlay to be drawn.</param>
    /// <param name="config">Overlay configurations.</param>
    /// <param name="customTitle">Custom overlay title.</param>
    private void DrawTranslationWindow(
        TranslationOverlay overlay,
        TranslationWindowConfig config,
        string? customTitle = null)
    {
      if (!overlay.Display)
      {
        return;
      }

      overlay.Semaphore.Wait();
      bool shouldDraw = !string.IsNullOrEmpty(overlay.CurrentText) && overlay.CurrentText != Resources.WaitingForTranslation;
      overlay.Semaphore.Release();

      if (!shouldDraw)
      {
        return;
      }

      PluginLog.Debug($"Drawing translation window: {overlay.CurrentName} -  {overlay.CurrentText}");

      customTitle ??= overlay.CurrentName;

      ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(
          overlay.Position.X + (overlay.Dimensions.X / 2) - (overlay.ImGuiSize.X / 2),
          overlay.Position.Y - overlay.ImGuiSize.Y - 20) + config.PosCorrection);

      float width = Math.Min(
          overlay.Dimensions.X * config.WidthMultiplier,
          ImGui.CalcTextSize(overlay.CurrentText).X + (ImGui.GetStyle().WindowPadding.X * 2));
      float height = overlay.Dimensions.Y * config.HeightMultiplier;

      ImGui.SetNextWindowSizeConstraints(new Vector2(width, 0), new Vector2(width * 4, height));
      ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(config.TextColor.X, config.TextColor.Y, config.TextColor.Z, 1.0f));

      if (this.configuration.SwapTextsUsingImGui)
      {
        UINewFontHandler.GeneralFontHandle.Push();
      }
      else
      {
        UINewFontHandler.LanguageFontHandle.Push();
      }

      ImGuiWindowFlags flags = ImGuiWindowFlags.NoNav
                              | ImGuiWindowFlags.AlwaysAutoResize
                              | ImGuiWindowFlags.NoFocusOnAppearing
                              | ImGuiWindowFlags.NoMouseInputs
                              | ImGuiWindowFlags.NoScrollbar;

      if (!config.ForceShowTitle)
      {
        flags |= ImGuiWindowFlags.NoTitleBar;
      }

      if (config.NoBackground)
      {
        flags |= ImGuiWindowFlags.NoBackground;
      }

      ImGui.Begin(customTitle ?? config.DefaultTitle, flags);
      ImGui.SetWindowFontScale(this.configuration.FontScale);

      overlay.Semaphore.Wait();
      ImGui.TextWrapped(overlay.CurrentText);
      overlay.Semaphore.Release();

      overlay.ImGuiSize = ImGui.GetWindowSize();
      ImGui.PopStyleColor(1);
      ImGui.End();

      if (this.configuration.SwapTextsUsingImGui)
      {
        UINewFontHandler.GeneralFontHandle.Pop();
      }
      else
      {
        UINewFontHandler.LanguageFontHandle.Pop();
      }
    }
  }
}
