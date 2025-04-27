// <copyright file="TranslationOverlayDrawer.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>
namespace Echoglossian
{

  public partial class Echoglossian
  {
    private void DrawTranslationWindow(
      TranslationOverlay overlay,
      TranslationWindowConfig config,
      string? customTitle = null)
    {

      if (!overlay.Display)
      {
        return;
      }

      PluginLog.Debug($"Drawing translation window: {overlay.CurrentTextId}");

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

      if (overlay.Semaphore.Wait(0))
      {
        ImGui.TextWrapped(overlay.CurrentText);
        overlay.Semaphore.Release();
      }
      else
      {
        ImGui.Text(Resources.WaitingForTranslation);
      }

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
