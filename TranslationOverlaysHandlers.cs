// <copyright file="TranslationOverlaysHandlers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Logging;
using Dalamud.Utility;
using Echoglossian.Properties;
using ImGuiNET;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private bool talkDisplayTranslation;

    private string currentNameTranslation = string.Empty;
    private volatile int currentNameTranslationId;

    private string currentTalkTranslation = string.Empty;
    private volatile int currentTalkTranslationId;

    private Vector2 talkTextDimensions = Vector2.Zero;
    private Vector2 talkTextImguiSize = Vector2.Zero;
    private Vector2 talkTextPosition = Vector2.Zero;

    private bool talkSubtitleDisplayTranslation;

    private string currentTalkSubtitleTranslation = string.Empty;
    private volatile int currentTalkSubtitleTranslationId;

    private Vector2 talkSubtitleTextDimensions = Vector2.Zero;
    private Vector2 talkSubtitleTextImguiSize = Vector2.Zero;
    private Vector2 talkSubtitleTextPosition = Vector2.Zero;

    private bool battleTalkDisplayTranslation;

    private string currentSenderTranslation = string.Empty;
    private volatile int currentSenderTranslationId;

    private string currentBattleTalkTranslation = string.Empty;
    private volatile int currentBattleTalkTranslationId;

    private Vector2 battleTalkTextDimensions = Vector2.Zero;
    private Vector2 battleTalkTextImguiSize = Vector2.Zero;
    private Vector2 battleTalkTextPosition = Vector2.Zero;

    private bool addonDisplayTranslation;

    private Vector2 addonTranslationTextDimensions = Vector2.Zero;
    private Vector2 addonTranslationTextImguiSize = Vector2.Zero;
    private Vector2 addonTranslationTextPosition = Vector2.Zero;

    private bool toastDisplayTranslation;

    private string currentToastTranslation = string.Empty;
    private volatile int currentToastTranslationId;

    private Vector2 toastTranslationTextDimensions = Vector2.Zero;
    private Vector2 toastTranslationTextImguiSize = Vector2.Zero;
    private Vector2 toastTranslationTextPosition = Vector2.Zero;

    private string currentQuestToastTranslation = string.Empty;
    private volatile int currentQuestToastTranslationId;

    private bool questToastDisplayTranslation;

    private Vector2 questToastTranslationTextDimensions = Vector2.Zero;
    private Vector2 questToastTranslationTextImguiSize = Vector2.Zero;
    private Vector2 questToastTranslationTextPosition = Vector2.Zero;

    private string currentClassChangeToastTranslation = string.Empty;
    private volatile int currentClassChangeToastTranslationId;

    private bool classChangeToastDisplayTranslation;

    private Vector2 classChangeToastTranslationTextDimensions = Vector2.Zero;
    private Vector2 classChangeToastTranslationTextImguiSize = Vector2.Zero;
    private Vector2 classChangeToastTranslationTextPosition = Vector2.Zero;

    private string currentWideTextToastTranslation = string.Empty;
    private volatile int currentWideTextToastTranslationId;

    private bool wideTextToastDisplayTranslation;

    private Vector2 wideTextToastTranslationTextDimensions = Vector2.Zero;
    private Vector2 wideTextToastTranslationTextImguiSize = Vector2.Zero;
    private Vector2 wideTextToastTranslationTextPosition = Vector2.Zero;

    private string currentAreaToastTranslation = string.Empty;
    private volatile int currentAreaToastTranslationId;

    private bool areaToastDisplayTranslation;

    private Vector2 areaToastTranslationTextDimensions = Vector2.Zero;
    private Vector2 areaToastTranslationTextImguiSize = Vector2.Zero;
    private Vector2 areaToastTranslationTextPosition = Vector2.Zero;

    private string currentErrorToastTranslation = string.Empty;
    private volatile int currentErrorToastTranslationId;

    private bool errorToastDisplayTranslation;

    private Vector2 errorToastTranslationTextDimensions = Vector2.Zero;
    private Vector2 errorToastTranslationTextImguiSize = Vector2.Zero;
    private Vector2 errorToastTranslationTextPosition = Vector2.Zero;

    public string GetTranslatedNpcNameForWindow()
    {
      string name;

      if (this.nameTranslationSemaphore.Wait(0))
      {
        name = this.currentNameTranslation;

        this.nameTranslationSemaphore.Release();
      }
      else
      {
        name = Resources.WaitingForTranslation;
      }

      return name;
    }

    public string GetTranslatedSenderNameForWindow()
    {
      string name;

      if (this.senderTranslationSemaphore.Wait(0))
      {
        name = this.currentSenderTranslation;

        this.senderTranslationSemaphore.Release();
      }
      else
      {
        name = Resources.WaitingForTranslation;
      }

      return name;
    }

    private void DrawTranslatedBattleDialogueWindow()
    {
      ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(
        this.battleTalkTextPosition.X + (this.battleTalkTextDimensions.X / 2) - (this.battleTalkTextImguiSize.X / 2),
        this.battleTalkTextPosition.Y - this.battleTalkTextImguiSize.Y - 20) + this.configuration.ImGuiWindowPosCorrection);
      var size = Math.Min(
        this.battleTalkTextDimensions.X * this.configuration.ImGuiBattleTalkWindowWidthMult * 1.5f,
        ImGui.CalcTextSize(this.currentBattleTalkTranslation).X + (ImGui.GetStyle().WindowPadding.X * 3));
      ImGui.SetNextWindowSizeConstraints(new Vector2(size, 0), new Vector2(size, this.battleTalkTextDimensions.Y * 2.5f * this.configuration.ImGuiBattleTalkWindowHeightMult));
      ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(this.configuration.OverlayBattleTalkTextColor, 255));
      if (this.FontLoaded)
      {
#if DEBUG
        // PluginLog.LogVerbose("Pushing font");
#endif
        ImGui.PushFont(this.UiFont);
      }

      if (this.configuration.TranslateNpcNames)
      {
        var name = this.GetTranslatedSenderNameForWindow();
        if (!name.IsNullOrEmpty())
        {
          ImGui.Begin(
            name,
            ImGuiWindowFlags.NoNav
            | ImGuiWindowFlags.NoCollapse
            | ImGuiWindowFlags.AlwaysAutoResize
            | ImGuiWindowFlags.NoFocusOnAppearing
            | ImGuiWindowFlags.NoMouseInputs
            | ImGuiWindowFlags.NoScrollbar);
        }
        else
        {
          ImGui.Begin(
            "BattleTalk translation",
            ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoNav
            | ImGuiWindowFlags.AlwaysAutoResize
            | ImGuiWindowFlags.NoFocusOnAppearing
            | ImGuiWindowFlags.NoMouseInputs
            | ImGuiWindowFlags.NoScrollbar);
        }
      }
      else
      {
        ImGui.Begin(
          "BattleTalk translation",
          ImGuiWindowFlags.NoTitleBar
          | ImGuiWindowFlags.NoNav
          | ImGuiWindowFlags.AlwaysAutoResize
          | ImGuiWindowFlags.NoFocusOnAppearing
          | ImGuiWindowFlags.NoMouseInputs
          | ImGuiWindowFlags.NoScrollbar);
      }

      ImGui.SetWindowFontScale(this.configuration.FontScale);
      if (this.battleTalkTranslationSemaphore.Wait(0))
      {
        ImGui.TextWrapped(this.currentBattleTalkTranslation);

        this.battleTalkTranslationSemaphore.Release();
      }
      else
      {
        ImGui.Text(Resources.WaitingForTranslation);
      }

      this.battleTalkTextImguiSize = ImGui.GetWindowSize();
      ImGui.PopStyleColor(1);

      ImGui.End();
      if (this.FontLoaded)
      {
#if DEBUG
        // PluginLog.LogVerbose("Popping font!");
#endif
        ImGui.PopFont();
      }
    }

    private void DrawTranslatedDialogueWindow()
    {
#if DEBUG
      // PluginLog.LogVerbose("Inside DrawTranslatedDialogueWindow method!");
#endif
      ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(
          this.talkTextPosition.X + (this.talkTextDimensions.X / 2) - (this.talkTextImguiSize.X / 2),
          this.talkTextPosition.Y - this.talkTextImguiSize.Y - 20) + this.configuration.ImGuiWindowPosCorrection);
      var size = Math.Min(
          this.talkTextDimensions.X * this.configuration.ImGuiTalkWindowWidthMult,
          ImGui.CalcTextSize(this.currentTalkTranslation).X + (ImGui.GetStyle().WindowPadding.X * 2));
      ImGui.SetNextWindowSizeConstraints(new Vector2(size, 0), new Vector2(size, this.talkTextDimensions.Y * this.configuration.ImGuiTalkWindowHeightMult));
      ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(this.configuration.OverlayTextColor, 255));
      if (this.FontLoaded)
      {
#if DEBUG
        // PluginLog.LogVerbose("Pushing font");
#endif
        ImGui.PushFont(this.UiFont);
      }

      if (this.configuration.TranslateNpcNames)
      {
        var name = this.GetTranslatedNpcNameForWindow();
        if (!name.IsNullOrEmpty())
        {
          ImGui.Begin(
            name,
            ImGuiWindowFlags.NoNav
            | ImGuiWindowFlags.NoCollapse
            | ImGuiWindowFlags.AlwaysAutoResize
            | ImGuiWindowFlags.NoFocusOnAppearing
            | ImGuiWindowFlags.NoMouseInputs
            | ImGuiWindowFlags.NoScrollbar);
        }
        else
        {
          ImGui.Begin(
            "Talk translation",
            ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoNav
            | ImGuiWindowFlags.AlwaysAutoResize
            | ImGuiWindowFlags.NoFocusOnAppearing
            | ImGuiWindowFlags.NoMouseInputs
            | ImGuiWindowFlags.NoScrollbar);
        }
      }
      else
      {
        ImGui.Begin(
          "Talk translation",
          ImGuiWindowFlags.NoTitleBar
          | ImGuiWindowFlags.NoNav
          | ImGuiWindowFlags.AlwaysAutoResize
          | ImGuiWindowFlags.NoFocusOnAppearing
          | ImGuiWindowFlags.NoMouseInputs
          | ImGuiWindowFlags.NoScrollbar);
      }

      ImGui.SetWindowFontScale(this.configuration.FontScale);
      if (this.talkTranslationSemaphore.Wait(0))
      {
        ImGui.TextWrapped(this.currentTalkTranslation);

        this.talkTranslationSemaphore.Release();
      }
      else
      {
        ImGui.Text(Resources.WaitingForTranslation);
      }

      this.talkTextImguiSize = ImGui.GetWindowSize();

      ImGui.PopStyleColor(1);

      ImGui.End();
      if (this.FontLoaded)
      {
#if DEBUG
        // PluginLog.LogVerbose("Popping font!");
#endif
        ImGui.PopFont();
      }
    }

    private void DrawTranslatedToastWindow()
    {
      if (this.configuration.UseImGui && this.configuration.TranslateToast && this.toastDisplayTranslation)
      {
        ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(
          this.toastTranslationTextPosition.X + (this.toastTranslationTextDimensions.X / 2) - (this.toastTranslationTextImguiSize.X / 2),
          this.toastTranslationTextPosition.Y - this.toastTranslationTextImguiSize.Y - 20) + this.configuration.ImGuiToastWindowPosCorrection);
        var size = Math.Min(
          this.toastTranslationTextDimensions.X * this.configuration.ImGuiToastWindowWidthMult,
          ImGui.CalcTextSize(this.currentToastTranslation).X + (ImGui.GetStyle().WindowPadding.X * 2));
        ImGui.SetNextWindowSizeConstraints(new Vector2(size, 0), new Vector2(size * 1.5f, this.toastTranslationTextDimensions.Y));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(this.configuration.OverlayTextColor, 255));
        if (this.FontLoaded)
        {
#if DEBUG
          // PluginLog.LogVerbose("Pushing font");
#endif
          ImGui.PushFont(this.UiFont);
        }

        ImGui.Begin(
          "Toast Translation",
          ImGuiWindowFlags.NoTitleBar
          | ImGuiWindowFlags.NoNav
          | ImGuiWindowFlags.AlwaysAutoResize
          | ImGuiWindowFlags.NoFocusOnAppearing
          | ImGuiWindowFlags.NoMouseInputs);

        if (this.toastTranslationSemaphore.Wait(0))
        {
          ImGui.Text(this.currentToastTranslation);
          this.toastTranslationSemaphore.Release();
        }
        else
        {
          ImGui.Text(Resources.WaitingForTranslation);
        }

        this.toastTranslationTextImguiSize = ImGui.GetWindowSize();
        ImGui.PopStyleColor(1);
        ImGui.End();
      }
    }

    private void DrawTranslatedErrorToastWindow()
    {
#if DEBUG
      // PluginLog.LogWarning("Using Toast Overlay - inside Draw Error toast Overlay");
#endif
      ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(
        this.errorToastTranslationTextPosition.X + (this.errorToastTranslationTextDimensions.X / 2) - (this.errorToastTranslationTextImguiSize.X / 2),
        this.errorToastTranslationTextPosition.Y - this.errorToastTranslationTextImguiSize.Y - 20) + this.configuration.ImGuiToastWindowPosCorrection);
      var size = Math.Min(
        this.errorToastTranslationTextDimensions.X * this.configuration.ImGuiToastWindowWidthMult,
        ImGui.CalcTextSize(this.currentErrorToastTranslation).X + ImGui.GetStyle().WindowPadding.X);
#if DEBUG
      // PluginLog.LogWarning($"size: {size}");
#endif
      ImGui.SetNextWindowSizeConstraints(new Vector2(size, 0), new Vector2(size * 4, this.errorToastTranslationTextDimensions.Y * 2));
#if DEBUG
      // PluginLog.LogWarning($"size min: {new Vector2(size, 0)}, Size max: {new Vector2(size * 4, this.errorToastTranslationTextDimensions.Y * 2)}");
#endif
      ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(this.configuration.OverlayTextColor, 255));
      if (this.FontLoaded)
      {
#if DEBUG
        // PluginLog.LogVerbose("Pushing font");
#endif
        ImGui.PushFont(this.UiFont);
      }

      ImGui.Begin(
        "Error Toast Translation",
        ImGuiWindowFlags.NoTitleBar
        | ImGuiWindowFlags.NoNav
        | ImGuiWindowFlags.AlwaysAutoResize
        | ImGuiWindowFlags.NoFocusOnAppearing
        | ImGuiWindowFlags.NoMouseInputs
        | ImGuiWindowFlags.NoBackground);

      if (this.errorToastTranslationSemaphore.Wait(0))
      {
        ImGui.Text(this.currentErrorToastTranslation);
        this.errorToastTranslationSemaphore.Release();
      }
      else
      {
        ImGui.Text(Resources.WaitingForTranslation);
      }

      ImGui.SetWindowFontScale(this.configuration.FontScale);
      this.errorToastTranslationTextImguiSize = ImGui.GetWindowSize();
      ImGui.PopStyleColor(1);
      ImGui.End();
      if (this.FontLoaded)
      {
#if DEBUG
        // PluginLog.LogVerbose("Popping font!");
#endif
        ImGui.PopFont();
      }
    }

    private void DrawTranslatedClassChangeToastWindow()
    {
#if DEBUG
      // PluginLog.LogWarning("Using Toast Overlay - inside Draw Class change toast Overlay");
#endif
      ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(
        this.classChangeToastTranslationTextPosition.X + (this.classChangeToastTranslationTextDimensions.X / 2) - (this.classChangeToastTranslationTextImguiSize.X / 2),
        this.classChangeToastTranslationTextPosition.Y - this.classChangeToastTranslationTextImguiSize.Y - 20) + this.configuration.ImGuiToastWindowPosCorrection);
      var size = Math.Min(
        this.classChangeToastTranslationTextDimensions.X * this.configuration.ImGuiToastWindowWidthMult,
        ImGui.CalcTextSize(this.currentClassChangeToastTranslation).X + ImGui.GetStyle().WindowPadding.X);
#if DEBUG
      PluginLog.LogWarning($"size: {size}");
#endif
      ImGui.SetNextWindowSizeConstraints(new Vector2(size, 0), new Vector2(size * 4, this.classChangeToastTranslationTextDimensions.Y * 2));
#if DEBUG
      PluginLog.LogWarning($"size min: {new Vector2(size, 0)}, Size max: {new Vector2(size * 4, this.classChangeToastTranslationTextDimensions.Y * 2)}");
#endif
      ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(this.configuration.OverlayTextColor, 255));
      if (this.FontLoaded)
      {
#if DEBUG
        // PluginLog.LogVerbose("Pushing font");
#endif
        ImGui.PushFont(this.UiFont);
      }

      ImGui.Begin(
        "Error Toast Translation",
        ImGuiWindowFlags.NoTitleBar
        | ImGuiWindowFlags.NoNav
        | ImGuiWindowFlags.AlwaysAutoResize
        | ImGuiWindowFlags.NoFocusOnAppearing
        | ImGuiWindowFlags.NoMouseInputs
        | ImGuiWindowFlags.NoBackground);

      if (this.classChangeToastTranslationSemaphore.Wait(0))
      {
        ImGui.Text(this.currentClassChangeToastTranslation);
        this.classChangeToastTranslationSemaphore.Release();
      }
      else
      {
        ImGui.Text(Resources.WaitingForTranslation);
      }

      ImGui.SetWindowFontScale(this.configuration.FontScale);
      this.classChangeToastTranslationTextImguiSize = ImGui.GetWindowSize();
      ImGui.PopStyleColor(1);
      ImGui.End();
      if (this.FontLoaded)
      {
#if DEBUG
        // PluginLog.LogVerbose("Popping font!");
#endif
        ImGui.PopFont();
      }
    }
  }
}