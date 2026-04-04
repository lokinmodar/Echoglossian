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
    /// Updates the overlay bounds using a live addon instance already available in
    /// the current lifecycle callback.
    /// </summary>
    /// <param name="overlay">Overlay whose bounds should be updated.</param>
    /// <param name="addon">Visible addon providing the current bounds.</param>
    private unsafe void UpdateOverlayBounds(
        TranslationOverlay overlay,
        AtkUnitBase* addon)
    {
      if (addon == null || addon->RootNode == null)
      {
        return;
      }

      overlay.Position = new Vector2(addon->RootNode->X, addon->RootNode->Y);
      overlay.Dimensions = new Vector2(
          addon->RootNode->Width * addon->Scale,
          addon->RootNode->Height * addon->Scale);
    }

    /// <summary>
    /// Updates toast overlay bounds using addon-level coordinates, which are more
    /// stable for transient toast addons than the generic root-node anchoring used
    /// by Talk/BattleTalk overlays.
    /// </summary>
    /// <param name="overlay">Overlay whose bounds should be updated.</param>
    /// <param name="addon">Visible toast addon providing the current bounds.</param>
    private unsafe void UpdateToastOverlayBounds(
        TranslationOverlay overlay,
        AtkUnitBase* addon,
        AtkTextNode* textNode)
    {
      if (addon == null || addon->RootNode == null || textNode == null)
      {
        return;
      }

      var paddingScale = 1.05f;
      overlay.Position = new Vector2(textNode->ScreenX, textNode->ScreenY);
      overlay.Dimensions = new Vector2(
          Math.Max(1f, textNode->GetWidth() * addon->Scale * paddingScale),
          Math.Max(1f, textNode->GetHeight() * addon->Scale * paddingScale));
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
    /// Synchronizes toast overlay bounds to the current addon position on the UI
    /// thread using addon-level coordinates instead of root-node local offsets.
    /// </summary>
    /// <param name="addonName">Toast addon name to query.</param>
    /// <param name="overlay">Overlay to update.</param>
    /// <param name="index">Addon index.</param>
    /// <returns>True when the addon exists and is visible.</returns>
    private unsafe bool TrySyncToastOverlayToAddon(
        string addonName,
        TranslationOverlay overlay,
        ResolveToastTextNodeDelegate resolveToastTextNode,
        int index = 1)
    {
      var addonPtr = GameGuiInterface.GetAddonByName(addonName, index);
      if (addonPtr.Address == IntPtr.Zero)
      {
        this.ClearOverlay(overlay);
        return false;
      }

      var addon = (AtkUnitBase*)addonPtr.Address;
      if (addon == null || !addon->IsVisible || addon->RootNode == null)
      {
        this.ClearOverlay(overlay);
        return false;
      }

      var textNode = resolveToastTextNode(addon);
      if (textNode == null || textNode->NodeText.IsEmpty)
      {
        this.ClearOverlay(overlay);
        return false;
      }

      this.UpdateToastOverlayBounds(overlay, addon, textNode);
      return true;
    }

    /// <summary>
    /// Synchronizes the quest toast overlay to a stable top-center viewport anchor.
    /// Quest toasts do not currently expose a dedicated addon path in the new
    /// runtime, so the overlay uses a predictable screen anchor plus per-type
    /// position correction.
    /// </summary>
    /// <returns>
    /// <see langword="true" /> when the quest toast overlay currently has visible
    /// content; otherwise, <see langword="false" />.
    /// </returns>
    private bool TrySyncQuestToastOverlayToViewport()
    {
      this.questToastOverlay.Semaphore.Wait();
      var shouldDisplay = this.questToastOverlay.Display;
      this.questToastOverlay.Semaphore.Release();

      if (!shouldDisplay)
      {
        return false;
      }

      var viewport = ImGui.GetMainViewport();
      this.questToastOverlay.Position = new Vector2(
          viewport.Pos.X + (viewport.Size.X * 0.5f),
          viewport.Pos.Y + (viewport.Size.Y * 0.14f));
      this.questToastOverlay.Dimensions = new Vector2(
          viewport.Size.X * 0.35f,
          56f);
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

      var desiredPosition = config.CenterOnAddon
          ? new Vector2(
              overlay.Position.X + (overlay.Dimensions.X * 0.5f) -
              (overlay.ImGuiSize.X * 0.5f),
              overlay.Position.Y + (overlay.Dimensions.Y * 0.5f) -
              (overlay.ImGuiSize.Y * 0.5f))
          : new Vector2(
              overlay.Position.X + (overlay.Dimensions.X / 2) -
              (overlay.ImGuiSize.X / 2),
              overlay.Position.Y - overlay.ImGuiSize.Y - 20);

      ImGuiHelpers.SetNextWindowPosRelativeMainViewport(
          desiredPosition + config.PosCorrection);

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
      if (config.AutoSizeToTextWithMaxWidth)
      {
        desiredWidth = Math.Max(baseWidth, textWidth);
      }
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

      if (config.AutoSizeToTextWithMaxWidth)
      {
        ImGui.SetNextWindowSizeConstraints(
            Vector2.Zero,
            new Vector2(width, maxHeight));
      }
      else if (config.UseFixedWindowSize)
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

      if (config.NoBackground || config.BackgroundOpacity <= 0f)
      {
        flags |= ImGuiWindowFlags.NoBackground;
      }
      else
      {
        ImGui.SetNextWindowBgAlpha(Math.Clamp(config.BackgroundOpacity, 0f, 1f));
      }

      var windowLabel = !string.IsNullOrWhiteSpace(resolvedTitle)
          ? resolvedTitle
          : $"{config.DefaultTitle}##overlay-{overlay.GetHashCode()}";
      ImGui.Begin(windowLabel, flags);
      ImGui.SetWindowFontScale(config.FontScale);
      var renderedWindowPos = ImGui.GetWindowPos();

      overlay.Semaphore.Wait();
      ImGui.TextWrapped(overlay.CurrentText);
      overlay.Semaphore.Release();

      overlay.ImGuiSize = ImGui.GetWindowSize();
      if (config.DefaultTitle.StartsWith("Screen Info", StringComparison.OrdinalIgnoreCase))
      {
        PluginLog.Debug(
            $"Rendered toast overlay '{windowLabel}' at ({renderedWindowPos.X:0.##}, {renderedWindowPos.Y:0.##}) size ({overlay.ImGuiSize.X:0.##} x {overlay.ImGuiSize.Y:0.##})");
      }
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
