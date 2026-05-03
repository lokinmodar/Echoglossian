// <copyright file="TranslationOverlayDrawer.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private readonly Dictionary<nint, TranslationOverlay> miniTalkBubbleOverlays = new();
    private readonly Dictionary<TranslationOverlay, int> overlayRenderTraceVersions = new();
    private readonly object miniTalkBubbleOverlaysGate = new();
    private readonly object overlayRenderTraceGate = new();

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
      if (overlay == null || overlay.IsDisposed)
      {
        return;
      }

      bool hasValidText = !string.IsNullOrWhiteSpace(translatedText);

      if (!this.TryEnterOverlaySemaphore(overlay.NameSemaphore))
      {
        return;
      }

      try
      {
        if (overlay.IsDisposed)
        {
          return;
        }

        overlay.OriginalName = originalName ?? string.Empty;
        overlay.CurrentName = translatedName ?? string.Empty;
        overlay.CurrentNameId++;
      }
      finally
      {
        this.TryReleaseOverlaySemaphore(overlay.NameSemaphore);
      }

      if (!this.TryEnterOverlaySemaphore(overlay.Semaphore))
      {
        return;
      }

      try
      {
        if (overlay.IsDisposed)
        {
          return;
        }

        overlay.CurrentText =
            hasValidText ? translatedText : Resources.WaitingForTranslation;
        overlay.Display = hasValidText;
        overlay.CurrentTextId++;
      }
      finally
      {
        this.TryReleaseOverlaySemaphore(overlay.Semaphore);
      }
    }

    /// <summary>
    /// Gets or creates the overlay object associated with a MiniTalk bubble.
    /// </summary>
    /// <param name="bubbleKey">Stable key for the visible bubble instance.</param>
    /// <returns>The tracked overlay state for the bubble.</returns>
    private TranslationOverlay GetOrCreateMiniTalkBubbleOverlay(nint bubbleKey)
    {
      lock (this.miniTalkBubbleOverlaysGate)
      {
        if (this.miniTalkBubbleOverlays.TryGetValue(bubbleKey, out var overlay))
        {
          return overlay;
        }

        overlay = new TranslationOverlay();
        this.miniTalkBubbleOverlays[bubbleKey] = overlay;
        return overlay;
      }
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
      if (overlay == null ||
          overlay.IsDisposed ||
          !this.TryEnterOverlaySemaphore(overlay.Semaphore))
      {
        return;
      }

      try
      {
        if (overlay.IsDisposed)
        {
          return;
        }

        overlay.Display = false;

        if (clearText)
        {
          overlay.CurrentText = string.Empty;
        }
      }
      finally
      {
        this.TryReleaseOverlaySemaphore(overlay.Semaphore);
      }
    }

    /// <summary>
    /// Attempts to enter an overlay semaphore without throwing when the overlay
    /// has already been disposed during plugin unload.
    /// </summary>
    /// <param name="semaphore">The overlay semaphore.</param>
    /// <returns>
    /// <see langword="true" /> when the semaphore was entered successfully;
    /// otherwise, <see langword="false" />.
    /// </returns>
    private bool TryEnterOverlaySemaphore(SemaphoreSlim semaphore)
    {
      try
      {
        semaphore.Wait();
        return true;
      }
      catch (ObjectDisposedException)
      {
        return false;
      }
    }

    /// <summary>
    /// Releases an overlay semaphore without throwing when it has already been
    /// disposed during plugin unload.
    /// </summary>
    /// <param name="semaphore">The overlay semaphore.</param>
    private void TryReleaseOverlaySemaphore(SemaphoreSlim semaphore)
    {
      try
      {
        semaphore.Release();
      }
      catch (ObjectDisposedException)
      {
      }
      catch (SemaphoreFullException)
      {
      }
    }

    /// <summary>
    /// Updates or creates the overlay content for a single MiniTalk bubble.
    /// </summary>
    /// <param name="bubbleKey">Stable key for the visible bubble instance.</param>
    /// <param name="translatedName">Translated speaker or title.</param>
    /// <param name="translatedText">Translated content.</param>
    /// <param name="originalName">Original speaker or title.</param>
    private void UpdateMiniTalkBubbleOverlayContent(
        nint bubbleKey,
        string translatedName,
        string translatedText,
        string originalName = "")
    {
      var overlay = this.GetOrCreateMiniTalkBubbleOverlay(bubbleKey);
      this.UpdateOverlayContent(overlay, translatedName, translatedText, originalName);
    }

    /// <summary>
    /// Clears the overlay state for a single MiniTalk bubble.
    /// </summary>
    /// <param name="bubbleKey">Stable key for the visible bubble instance.</param>
    /// <param name="clearText">Whether to clear the translated text.</param>
    private void ClearMiniTalkBubbleOverlay(
        nint bubbleKey,
        bool clearText = false)
    {
      lock (this.miniTalkBubbleOverlaysGate)
      {
        if (!this.miniTalkBubbleOverlays.TryGetValue(bubbleKey, out var overlay))
        {
          return;
        }

        this.ClearOverlay(overlay, clearText);

        if (clearText)
        {
          this.miniTalkBubbleOverlays.Remove(bubbleKey);
        }
      }
    }

    /// <summary>
    /// Clears and disposes every MiniTalk bubble overlay currently tracked.
    /// </summary>
    private void DisposeMiniTalkBubbleOverlays()
    {
      lock (this.miniTalkBubbleOverlaysGate)
      {
        foreach (var overlay in this.miniTalkBubbleOverlays.Values)
        {
          overlay.Dispose();
        }

        this.miniTalkBubbleOverlays.Clear();
      }
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
    /// Splits overlay text into individual render lines while preserving empty
    /// lines as spacing markers.
    /// </summary>
    /// <param name="text">The overlay text to split.</param>
    /// <returns>The individual lines to render.</returns>
    private static string[] SplitOverlayTextLines(string text)
    {
      if (string.IsNullOrEmpty(text))
      {
        return [];
      }

      return text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
    }

    /// <summary>
    /// Updates the overlay bounds for a single MiniTalk bubble instance.
    /// </summary>
    /// <param name="bubbleKey">Stable key for the visible bubble instance.</param>
    /// <param name="addon">Visible addon providing the current bounds.</param>
    /// <param name="textNode">The visible MiniTalk text node.</param>
    private unsafe void SyncMiniTalkBubbleOverlayBounds(
        nint bubbleKey,
        AtkUnitBase* addon,
        AtkTextNode* textNode)
    {
      if (addon == null || addon->RootNode == null || textNode == null)
      {
        return;
      }

      var overlay = this.GetOrCreateMiniTalkBubbleOverlay(bubbleKey);
      this.UpdateToastOverlayBounds(overlay, addon, textNode);
    }

    /// <summary>
    /// Draws every active MiniTalk bubble overlay instance.
    /// </summary>
    private void DrawMiniTalkBubbleOverlays()
    {
      if (!this.configuration.TranslateMiniTalk ||
          !NativeUI.Helpers.TranslationDisplayModeHelper.UsesOverlayPresentation(
              this.configuration.MiniTalkTranslationDisplayMode,
              this.configuration.OverlayOnlyLanguage))
      {
        return;
      }

      List<TranslationOverlay> snapshot;
      lock (this.miniTalkBubbleOverlaysGate)
      {
        snapshot = this.miniTalkBubbleOverlays.Values.ToList();
      }

      if (snapshot.Count == 0)
      {
        return;
      }

      var config = TranslationWindowConfig.FromConfigForMiniTalk(this.configuration);
      foreach (var overlay in snapshot)
      {
        this.DrawTranslationWindow(overlay, config);
      }
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
      if (!FrameworkAccessGuard.IsClientReadyForFrameworkAccess())
      {
        this.ClearOverlay(overlay);
        return false;
      }

      var addonPtr = GameGuiInterface.GetAddonByName(addonName, index);
      if (addonPtr.Address == IntPtr.Zero)
      {
        if (FrameworkAccessGuard.TryGetRaptureAtkUnitManager(out var manager))
        {
          foreach (var unit in manager->AllLoadedUnitsList.Entries)
          {
            var unitPtr = unit.Value;
            if (unitPtr == null ||
                !unitPtr->IsReady ||
                !string.Equals(
                    unitPtr->NameString,
                    addonName,
                    StringComparison.Ordinal))
            {
              continue;
            }

            addonPtr = (nint)unitPtr;
            break;
          }
        }
      }

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
      var hasPublishedContent = overlay.Display;

      if (!this.TryResolveToastAddon(addonName, index, out var addon, out var resolvedIndex))
      {
        return hasPublishedContent;
      }

      if (addon == null || !addon->IsVisible || addon->RootNode == null)
      {
        return hasPublishedContent;
      }

      var textNode = resolveToastTextNode(addon);
      if (textNode == null || textNode->NodeText.IsEmpty)
      {
        return hasPublishedContent;
      }

      this.UpdateToastOverlayBounds(overlay, addon, textNode);
      // PluginRuntimeLog.Debug(
      //     $"Synced toast overlay '{addonName}' index={resolvedIndex} at ({overlay.Position.X:0.##}, {overlay.Position.Y:0.##}) " +
      //     $"size ({overlay.Dimensions.X:0.##} x {overlay.Dimensions.Y:0.##})");
      return true;
    }

    /// <summary>
    /// Resolves a toast addon by name, preferring the requested index and falling
    /// back to the first loaded matching instance when the requested index is not
    /// present.
    /// </summary>
    /// <param name="addonName">Toast addon name to query.</param>
    /// <param name="index">Preferred addon index.</param>
    /// <param name="addon">Receives the resolved addon pointer.</param>
    /// <param name="resolvedIndex">Receives the resolved addon index.</param>
    /// <returns>True when a live addon instance was found.</returns>
    private unsafe bool TryResolveToastAddon(
        string addonName,
        int index,
        out AtkUnitBase* addon,
        out int resolvedIndex)
    {
      addon = null;
      resolvedIndex = index;

      if (!FrameworkAccessGuard.IsClientReadyForFrameworkAccess())
      {
        return false;
      }

      var addonPtr = GameGuiInterface.GetAddonByName(addonName, index);
      if (addonPtr.Address != IntPtr.Zero)
      {
        addon = (AtkUnitBase*)addonPtr.Address;
        resolvedIndex = index;
        return true;
      }

      if (!FrameworkAccessGuard.TryGetRaptureAtkUnitManager(out var manager))
      {
        return false;
      }

      var matchIndex = 0;
      foreach (var unit in manager->AllLoadedUnitsList.Entries)
      {
        var unitPtr = unit.Value;
        if (unitPtr == null || !unitPtr->IsReady)
        {
          continue;
        }

        if (!string.Equals(unitPtr->NameString, addonName, StringComparison.Ordinal))
        {
          continue;
        }

        if (matchIndex++ != index)
        {
          continue;
        }

        addon = unitPtr;
        resolvedIndex = index;
        // PluginRuntimeLog.Debug(
        //     $"Toast overlay resolved '{addonName}' requestedIndex={index} using fallback loaded unit");
        return true;
      }

      matchIndex = 0;
      foreach (var unit in manager->AllLoadedUnitsList.Entries)
      {
        var unitPtr = unit.Value;
        if (unitPtr == null || !unitPtr->IsReady)
        {
          continue;
        }

        if (!string.Equals(unitPtr->NameString, addonName, StringComparison.Ordinal))
        {
          continue;
        }

        addon = unitPtr;
        resolvedIndex = matchIndex;
        // PluginRuntimeLog.Debug(
        //     $"Toast overlay resolved '{addonName}' requestedIndex={index} using first available matching unit");
        return true;
      }

      return false;
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
      // PluginRuntimeLog.Debug(
      //     $"Synced quest toast overlay at ({this.questToastOverlay.Position.X:0.##}, {this.questToastOverlay.Position.Y:0.##}) " +
      //     $"size ({this.questToastOverlay.Dimensions.X:0.##} x {this.questToastOverlay.Dimensions.Y:0.##})");
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
      // PluginRuntimeLog.Debug($"DrawTranslationWindow: {overlay.CurrentName} - {overlay.CurrentText}");
      if (!overlay.Display)
      {
        return;
      }

      overlay.Semaphore.Wait();
      var overlayText = overlay.CurrentText;
      bool shouldDraw = !string.IsNullOrEmpty(overlayText) && overlayText != Resources.WaitingForTranslation;
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

      // PluginRuntimeLog.Debug($"Drawing translation window: {overlay.CurrentName} -  {overlay.CurrentText}");

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

      var shouldUseGeneralFont = this.ShouldUseGeneralOverlayFont(config);
      var effectiveFontScale = GetEffectiveOverlayFontScale(config.FontScale);
      var viewportWidth = ImGui.GetMainViewport().Size.X;
      var horizontalPadding = ImGui.GetStyle().WindowPadding.X * 2;
      var baseWidth = overlay.Dimensions.X * config.WidthMultiplier;
      var overlayTextLines = SplitOverlayTextLines(overlayText);
      var textWidth = this.MeasureOverlayTextWidth(
          overlayText,
          overlayTextLines,
          shouldUseGeneralFont,
          effectiveFontScale,
          horizontalPadding);
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
            new Vector2(width, 0f),
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

      if (shouldUseGeneralFont)
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
      ImGui.SetWindowFontScale(effectiveFontScale);
      var renderedWindowPos = ImGui.GetWindowPos();

      overlay.Semaphore.Wait();
      foreach (var line in overlayTextLines)
      {
        if (string.IsNullOrEmpty(line))
        {
          ImGui.Spacing();
          continue;
        }

        ImGui.TextWrapped(line);
      }
      overlay.Semaphore.Release();

      overlay.ImGuiSize = ImGui.GetWindowSize();
      this.TraceOverlayRenderOnce(config, overlay, windowLabel, renderedWindowPos, textWidth, width);
      if (config.SurfaceId == TranslationOverlaySurfaceId.WideTextToast)
      {
        // PluginRuntimeLog.Debug(
        //     $"Rendered toast overlay '{windowLabel}' at ({renderedWindowPos.X:0.##}, {renderedWindowPos.Y:0.##}) size ({overlay.ImGuiSize.X:0.##} x {overlay.ImGuiSize.Y:0.##})");
      }
      ImGui.PopStyleColor(1);
      ImGui.End();

      if (this.IsToastLikeOverlaySurface(config.SurfaceId))
      {
        // PluginRuntimeLog.Debug(
        //     $"Rendered overlay '{windowLabel}' at ({renderedWindowPos.X:0.##}, {renderedWindowPos.Y:0.##}) " +
        //     $"size ({overlay.ImGuiSize.X:0.##} x {overlay.ImGuiSize.Y:0.##}) " +
        //     $"contentWidth={textWidth:0.##} windowWidth={width:0.##} overlayPos=({overlay.Position.X:0.##}, {overlay.Position.Y:0.##})");
      }

      if (shouldUseGeneralFont)
      {
        UINewFontHandler.GeneralFontHandle.Pop();
      }
      else
      {
        UINewFontHandler.LanguageFontHandle.Pop();
      }
    }

    /// <summary>
    /// Measures the width of the overlay text using the same font family and
    /// effective scale that will be used during rendering.
    /// </summary>
    /// <param name="overlayText">The full overlay text content.</param>
    /// <param name="overlayTextLines">The split overlay lines.</param>
    /// <param name="shouldUseGeneralFont">
    /// Whether the general/original font path should be used.
    /// </param>
    /// <param name="effectiveFontScale">
    /// The sanitized font scale that will be used for rendering.
    /// </param>
    /// <param name="horizontalPadding">
    /// The horizontal window padding that must be added to the text width.
    /// </param>
    /// <returns>The measured overlay width including horizontal padding.</returns>
    private float MeasureOverlayTextWidth(
        string overlayText,
        string[] overlayTextLines,
        bool shouldUseGeneralFont,
        float effectiveFontScale,
        float horizontalPadding)
    {
      if (shouldUseGeneralFont)
      {
        UINewFontHandler.GeneralFontHandle.Push();
      }
      else
      {
        UINewFontHandler.LanguageFontHandle.Push();
      }

      try
      {
        var rawTextWidth = overlayTextLines.Length == 0
            ? ImGui.CalcTextSize(overlayText).X
            : overlayTextLines.Max(line => string.IsNullOrEmpty(line)
                ? 0f
                : ImGui.CalcTextSize(line).X);
        return (rawTextWidth * effectiveFontScale) + horizontalPadding;
      }
      finally
      {
        if (shouldUseGeneralFont)
        {
          UINewFontHandler.GeneralFontHandle.Pop();
        }
        else
        {
          UINewFontHandler.LanguageFontHandle.Pop();
        }
      }
    }

    /// <summary>
    /// Logs CutSceneSelectString overlay renders once per content version.
    /// </summary>
    private void TraceOverlayRenderOnce(
        TranslationWindowConfig config,
        TranslationOverlay overlay,
        string windowLabel,
        Vector2 renderedWindowPos,
        float textWidth,
        float windowWidth)
    {
      if (config.SurfaceId != TranslationOverlaySurfaceId.CutSceneSelectString)
      {
        return;
      }

      lock (this.overlayRenderTraceGate)
      {
        if (this.overlayRenderTraceVersions.TryGetValue(overlay, out var lastVersion) &&
            lastVersion == overlay.CurrentTextId)
        {
          return;
        }

        this.overlayRenderTraceVersions[overlay] = overlay.CurrentTextId;
      }

      PluginRuntimeLog.Debug(
          "CutSceneSelectStringOverlay",
          $"rendered '{windowLabel}' version={overlay.CurrentTextId} " +
          $"at ({renderedWindowPos.X:0.##}, {renderedWindowPos.Y:0.##}) " +
          $"size ({overlay.ImGuiSize.X:0.##} x {overlay.ImGuiSize.Y:0.##}) " +
          $"contentWidth={textWidth:0.##} windowWidth={windowWidth:0.##} " +
          $"overlayPos=({overlay.Position.X:0.##}, {overlay.Position.Y:0.##})");
    }

    /// <summary>
    ///     Determines whether the current overlay surface should render using
    ///     the game's general font because it is showing original text in swap
    ///     mode.
    /// </summary>
    /// <param name="config">The overlay configuration being drawn.</param>
    /// <returns>
    ///     <see langword="true" /> when the overlay should use the original-text
    ///     font path; otherwise, <see langword="false" />.
    /// </returns>
    private bool ShouldUseGeneralOverlayFont(TranslationWindowConfig config)
    {
      var displayMode = config.SurfaceId switch
      {
        TranslationOverlaySurfaceId.Talk => this.configuration.TalkTranslationDisplayMode,
        TranslationOverlaySurfaceId.BattleTalk => this.configuration.BattleTalkTranslationDisplayMode,
        TranslationOverlaySurfaceId.TalkSubtitle => this.configuration.TalkSubtitleTranslationDisplayMode,
        TranslationOverlaySurfaceId.MiniTalk => this.configuration.MiniTalkTranslationDisplayMode,
        TranslationOverlaySurfaceId.CutSceneSelectString => this.configuration.CutSceneSelectStringTranslationDisplayMode,
        TranslationOverlaySurfaceId.TextGimmickHint => this.configuration.TextGimmickHintTranslationDisplayMode,
        TranslationOverlaySurfaceId.WideTextToast => this.configuration.WideTextToastTranslationDisplayMode,
        TranslationOverlaySurfaceId.ErrorToast => this.configuration.ErrorToastTranslationDisplayMode,
        TranslationOverlaySurfaceId.AreaToast => this.configuration.AreaToastTranslationDisplayMode,
        TranslationOverlaySurfaceId.ClassChangeToast => this.configuration.ClassChangeToastTranslationDisplayMode,
        TranslationOverlaySurfaceId.QuestToast => this.configuration.QuestToastTranslationDisplayMode,
        _ => JournalTranslationDisplayMode.TooltipTranslation,
      };

      return NativeUI.Helpers.TranslationDisplayModeHelper.ShowsOriginalOverlayText(
          displayMode,
          this.configuration.OverlayOnlyLanguage);
    }

    /// <summary>
    /// Normalizes the overlay font scale to the supported render range.
    /// </summary>
    /// <param name="configuredFontScale">The configured overlay font scale.</param>
    /// <returns>The effective positive font scale to use for measurement and render.</returns>
    private static float GetEffectiveOverlayFontScale(float configuredFontScale)
    {
      return Math.Clamp(configuredFontScale, 0.25f, 3.0f);
    }

    /// <summary>
    ///     Gets whether the specified surface behaves like one of the toast-style
    ///     overlays for diagnostics and layout tracing.
    /// </summary>
    /// <param name="surfaceId">The overlay surface identifier.</param>
    /// <returns>
    ///     <see langword="true" /> when the overlay belongs to the toast/gimmick
    ///     family; otherwise, <see langword="false" />.
    /// </returns>
    private bool IsToastLikeOverlaySurface(TranslationOverlaySurfaceId surfaceId)
    {
      return surfaceId is TranslationOverlaySurfaceId.TextGimmickHint
          or TranslationOverlaySurfaceId.WideTextToast
          or TranslationOverlaySurfaceId.ErrorToast
          or TranslationOverlaySurfaceId.AreaToast
          or TranslationOverlaySurfaceId.ClassChangeToast
          or TranslationOverlaySurfaceId.QuestToast;
    }
  }
}


