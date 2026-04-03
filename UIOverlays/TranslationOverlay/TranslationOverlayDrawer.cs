// <copyright file="TranslationOverlayDrawer.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian
{
  public partial class Echoglossian
  {
    /// <summary>
    /// Updates an overlay with translated content.
    /// </summary>
    /// <param name="overlay">Overlay to update.</param>
    /// <param name="translatedName">Translated speaker or title.</param>
    /// <param name="translatedText">Translated content.</param>
    /// <param name="originalName">Original speaker or title.</param>
    private void UpdateOverlayContent(
        TranslationOverlay overlay,
        string translatedName,
        string translatedText,
        string originalName = "")
    {
      bool hasValidText = !string.IsNullOrWhiteSpace(translatedText);

      overlay.NameSemaphore.Wait();
      overlay.OriginalName = originalName ?? string.Empty;
      overlay.CurrentName = translatedName ?? string.Empty;

      overlay.NameSemaphore.Release();

      overlay.Semaphore.Wait();
      overlay.CurrentText =
          hasValidText ? translatedText : Resources.WaitingForTranslation;
      overlay.Display = hasValidText;
      overlay.Semaphore.Release();
    }

    /// <summary>
    /// Clears the overlay visibility and optionally its text.
    /// </summary>
    /// <param name="overlay">Overlay to clear.</param>
    /// <param name="clearText">Whether to clear the translated text.</param>
    private void ClearOverlay(
        TranslationOverlay overlay,
        bool clearText = false)
    {
      overlay.Semaphore.Wait();
      overlay.Display = false;

      if (clearText)
      {
        overlay.CurrentText = string.Empty;
      }

      overlay.Semaphore.Release();
    }

    /// <summary>
    /// Synchronizes overlay bounds to the current addon position on the UI thread.
    /// </summary>
    /// <param name="addonName">Addon name to query.</param>
    /// <param name="overlay">Overlay to update.</param>
    /// <param name="index">Addon index.</param>
    /// <returns>True when the addon exists and is visible.</returns>
    private unsafe bool TrySyncOverlayToAddon(
        string addonName,
        TranslationOverlay overlay,
        int index = 1)
    {
      // PluginLog.Debug($"StartOverlayTracking: {addonName}");
      var addonPtr = GameGuiInterface.GetAddonByName(addonName, index);
      if (addonPtr.Address == IntPtr.Zero)
      {
        // PluginLog.Debug($"StartOverlayTracking: {addonName} not found.");
        this.ClearOverlay(overlay);
        return false;
      }

      var addon = (AtkUnitBase*)addonPtr.Address;
      if (addon == null || !addon->IsVisible || addon->RootNode == null)
      {
        this.ClearOverlay(overlay);
        return false;
      }

      overlay.Position = new Vector2(addon->RootNode->X, addon->RootNode->Y);
      overlay.Dimensions = new Vector2(
          addon->RootNode->Width * addon->Scale,
          addon->RootNode->Height * addon->Scale);
      return true;
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
      // PluginLog.Debug($"DrawTranslationWindow: {overlay.CurrentName} - {overlay.CurrentText}");
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

      var resolvedTitle = customTitle;
      if (string.IsNullOrWhiteSpace(resolvedTitle))
      {
        resolvedTitle = !string.IsNullOrWhiteSpace(overlay.CurrentName)
            ? overlay.CurrentName
            : overlay.OriginalName;
      }

      // PluginLog.Debug($"Drawing translation window: {overlay.CurrentName} -  {overlay.CurrentText}");

      ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(
          overlay.Position.X + (overlay.Dimensions.X / 2) - (overlay.ImGuiSize.X / 2),
          overlay.Position.Y - overlay.ImGuiSize.Y - 20) + config.PosCorrection);

      var viewportWidth = ImGui.GetMainViewport().Size.X;
      var horizontalPadding = ImGui.GetStyle().WindowPadding.X * 2;
      var baseWidth = overlay.Dimensions.X * config.WidthMultiplier;
      var textWidth = ImGui.CalcTextSize(overlay.CurrentText).X + horizontalPadding;
      var defaultMaxWidth = Math.Max(320f, viewportWidth - 80f);
      var minWidth = config.MinWidthViewportFraction > 0.0f
          ? viewportWidth * config.MinWidthViewportFraction
          : 0.0f;
      var maxWidth = config.MaxWidthViewportFraction > 0.0f
          ? Math.Min(
              viewportWidth * config.MaxWidthViewportFraction,
              defaultMaxWidth)
          : defaultMaxWidth;
      var desiredWidth = baseWidth;
      if (config.ExpandWidthToFitText)
      {
        var autoExpandedWidth = Math.Min(
            textWidth,
            baseWidth * config.MaxAutoExpandedWidthMultiplier);
        desiredWidth = Math.Max(baseWidth, autoExpandedWidth);
      }
      float width = Math.Clamp(desiredWidth, minWidth, maxWidth);
      var viewportHeight = ImGui.GetMainViewport().Size.Y;
      var maxHeight = Math.Max(180f, viewportHeight - 80f);

      if (config.UseFixedWindowSize)
      {
        ImGui.SetNextWindowSizeConstraints(
            new Vector2(width, 0),
            new Vector2(width, maxHeight));
      }
      else
      {
        ImGui.SetNextWindowSizeConstraints(
            new Vector2(width, 0),
            new Vector2(width * 4, maxHeight));
      }

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
                              | ImGuiWindowFlags.NoFocusOnAppearing
                              | ImGuiWindowFlags.NoMouseInputs
                              | ImGuiWindowFlags.NoScrollbar;

      flags |= ImGuiWindowFlags.AlwaysAutoResize;

      if (!config.ForceShowTitle || string.IsNullOrWhiteSpace(resolvedTitle))
      {
        flags |= ImGuiWindowFlags.NoTitleBar;
      }

      if (config.NoBackground)
      {
        flags |= ImGuiWindowFlags.NoBackground;
      }

      var windowLabel = !string.IsNullOrWhiteSpace(resolvedTitle)
          ? resolvedTitle
          : $"{config.DefaultTitle}##overlay-{overlay.GetHashCode()}";
      ImGui.Begin(windowLabel, flags);
      ImGui.SetWindowFontScale(config.FontScale);

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
