// <copyright file="UiTalkSubtitleAsyncHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;

using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Memory;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Properties;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Humanizer;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private unsafe void TranslateTalkSubtitle(string textToTranslate)
    {
      PluginLog.Debug("Translating Talk Subtitle: " + textToTranslate);
      Task.Run(() =>
      {
        try
        {
          TalkSubtitleMessage talkSubtitleMessage = this.FormatTalkSubtitleMessage(textToTranslate);
          TalkSubtitleMessage foundTalkSubtitleMessage = this.FindAndReturnTalkSubtitleMessage(talkSubtitleMessage);
          string translatedSubtitle = string.Empty;

          if (foundTalkSubtitleMessage != null)
          {
            translatedSubtitle = foundTalkSubtitleMessage.TranslatedTalkSubtitleMessage;
          }
          else
          {
            string textTranslation = this.Translate(textToTranslate);

            TalkSubtitleMessage translatedTalkSubtitleData = new TalkSubtitleMessage(
              textToTranslate, ClientStateInterface.ClientLanguage.Humanize(), textTranslation, langDict[languageInt].Code, this.configuration.ChosenTransEngine, DateTime.Now, DateTime.Now);

            string result = InsertTalkSubtitleData(translatedTalkSubtitleData);
            PluginLog.Debug("TalkSubtitle Insert Result: " + result);
            translatedSubtitle = textTranslation;
          }

          if (this.configuration.UseImGuiForTalkSubtitle)
          {
            this.TranslateTalkSubtitleUsingImGui(textToTranslate, translatedSubtitle);
          }
          else
          {
            this.TranslateTalkSubtitleReplacing(translatedSubtitle);
          }
        }
        catch (Exception ex)
        {
          PluginLog.Error("TranslateTalkSubtitle error: " + ex);
        }
      });
    }

    public unsafe void TranslateTalkSubtitleReplacing(string translatedTalkSubtitleText)
    {
      PluginLog.Debug("TranslateTalkSubtitleReplacing");

      if (this.configuration.UseImGuiForTalkSubtitle && !this.configuration.SwapTextsUsingImGui)
      {
        return;
      }

      try
      {
        var addon = GameGuiInterface.GetAddonByName("TalkSubtitle");
        if (addon == IntPtr.Zero)
        {
          return;
        }

        var talkSubtitleAddon = (AtkUnitBase*)addon;
        if (talkSubtitleAddon == null || !talkSubtitleAddon->IsVisible)
        {
          return;
        }

        var textNode = talkSubtitleAddon->GetTextNodeById(2);
        var textNode3 = talkSubtitleAddon->GetTextNodeById(3);
        var textNode4 = talkSubtitleAddon->GetTextNodeById(4);
        if (textNode == null ||
          textNode3 == null ||
          textNode4 == null ||
          textNode->NodeText.IsEmpty ||
          textNode3->NodeText.IsEmpty ||
          textNode4->NodeText.IsEmpty)
        {
          return;
        }

        var translTalkSubtitleText = translatedTalkSubtitleText;

        if (this.configuration.RemoveDiacriticsWhenUsingReplacementTalkBTalk)
        {
          translTalkSubtitleText = this.RemoveDiacritics(translTalkSubtitleText, this.SpecialCharsSupportedByGameFont);
        }

        textNode->SetText(translTalkSubtitleText);
        textNode3->SetText(translTalkSubtitleText);
        textNode4->SetText(translTalkSubtitleText);
      }
      catch (Exception ex)
      {
        PluginLog.Error("TranslateTalkSubtitleReplacing error: " + ex);
      }
    }

    private unsafe void TranslateTalkSubtitleUsingImGuiAndSwapping(string textToTranslate, string translatedSubtitle)
    {
      PluginLog.Debug($"TranslateTalkSubtitleUsingImGuiAndSwapping: {translatedSubtitle}");

      try
      {
        this.TranslateTalkSubtitleReplacing(translatedSubtitle);

        this.currentTalkSubtitleTranslationId = Environment.TickCount;
        this.currentTalkSubtitleTranslation = Resources.WaitingForTranslation;
        int id = this.currentTalkSubtitleTranslationId;
        this.talkSubtitleTranslationSemaphore.Wait();
        if (id == this.currentTalkSubtitleTranslationId)
        {
          this.currentTalkSubtitleTranslation = textToTranslate;
        }

        this.talkSubtitleTranslationSemaphore.Release();

        this.TalkSubtitleHandler("TalkSubtitle", 1);
      }
      catch (Exception e)
      {
        PluginLog.Debug("TranslateTalkSubtitleUsingImGuiAndSwapping Exception: " + e);
      }
    }

    private unsafe void TranslateTalkSubtitleUsingImGuiWithoutSwapping(string translatedSubtitle)
    {
      PluginLog.Debug($"TranslateTalkSubtitleUsingImGuiWithoutSwapping: {translatedSubtitle}");

      try
      {
        this.currentTalkSubtitleTranslationId = Environment.TickCount;
        this.currentTalkSubtitleTranslation = Resources.WaitingForTranslation;
        int id = this.currentTalkSubtitleTranslationId;
        string translatedTalkSubtitleMessage = translatedSubtitle;
        this.talkSubtitleTranslationSemaphore.Wait();
        if (id == this.currentTalkSubtitleTranslationId)
        {
          this.currentTalkSubtitleTranslation = translatedTalkSubtitleMessage;
        }

        this.talkSubtitleTranslationSemaphore.Release();
        this.TalkSubtitleHandler("TalkSubtitle", 1);
      }
      catch (Exception e)
      {
        PluginLog.Debug("TranslateTalkSubtitleUsingImGuiWithoutSwapping Exception: " + e);
      }
    }

    private unsafe void TranslateTalkSubtitleUsingImGui(string textToTranslate, string translatedSubtitle)
    {
      PluginLog.Debug($"TranslateTalkSubtitleUsingImGui: {translatedSubtitle}");

      if (this.configuration.SwapTextsUsingImGui)
      {
        this.TranslateTalkSubtitleUsingImGuiAndSwapping(textToTranslate, translatedSubtitle);
        return;
      }

      this.TranslateTalkSubtitleUsingImGuiWithoutSwapping(translatedSubtitle);
    }

    private unsafe void UiTalkSubtitleAsyncHandler(AddonEvent type, AddonArgs args)
    {
      if (!this.configuration.TranslateTalkSubtitle)
      {
        return;
      }

      PluginLog.Debug($"UiTalkSubtitleAsyncHandler: {type} {args.AddonName}");

      switch (args)
      {
        case AddonSetupArgs setupArgs:
          var setupAtkValues = (AtkValue*)setupArgs.AtkValues;
          if (setupAtkValues == null)
          {
            return;
          }

          if (setupAtkValues[0].Type != FFXIVClientStructs.FFXIV.Component.GUI.ValueType.String || setupAtkValues[0].String == null)
          {
            return;
          }

          var textToTranslate = MemoryHelper.ReadSeStringAsString(out _, (nint)setupAtkValues[0].String.Value);
          if (textToTranslate == string.Empty)
          {
            return;
          }

          if (!this.configuration.UseImGuiForTalkSubtitle || this.configuration.SwapTextsUsingImGui)
          {
            setupAtkValues[0].SetManagedString(string.Empty);
          }

          this.TranslateTalkSubtitle(textToTranslate);
          return;
        case AddonRefreshArgs refreshArgs:
          var refreshAtkValues = (AtkValue*)refreshArgs.AtkValues;
          if (refreshAtkValues == null)
          {
            return;
          }

          if (refreshAtkValues[0].Type != FFXIVClientStructs.FFXIV.Component.GUI.ValueType.String || refreshAtkValues[0].String == null)
          {
            return;
          }

          var refreshTextToTranslate = MemoryHelper.ReadSeStringAsString(out _, (nint)refreshAtkValues[0].String.Value);
          if (refreshTextToTranslate == string.Empty)
          {
            return;
          }

          if (!this.configuration.UseImGuiForTalkSubtitle || this.configuration.SwapTextsUsingImGui)
          {
            refreshAtkValues[0].SetManagedString(string.Empty);
          }

          this.TranslateTalkSubtitle(refreshTextToTranslate);
          return;
      }
    }

    private unsafe void TalkSubtitleHandler(string addonName, int index)
    {
      IntPtr talkSubtitle = GameGuiInterface.GetAddonByName(addonName, index);
      if (talkSubtitle != IntPtr.Zero)
      {
        AtkUnitBase* talkSubtitleMaster = (AtkUnitBase*)talkSubtitle;
        while (talkSubtitleMaster->IsVisible)
        {
          this.talkSubtitleDisplayTranslation = true;
          this.talkSubtitleTextDimensions.X = talkSubtitleMaster->RootNode->Width * talkSubtitleMaster->Scale;
          this.talkSubtitleTextDimensions.Y = talkSubtitleMaster->RootNode->Height * talkSubtitleMaster->Scale;
          this.talkSubtitleTextPosition.X = talkSubtitleMaster->RootNode->X;
          this.talkSubtitleTextPosition.Y = talkSubtitleMaster->RootNode->Y;

          Thread.Sleep(this.delayBetweenVisibilityCheckForOverlay);
        }

        this.talkSubtitleDisplayTranslation = false;
      }

      this.talkSubtitleDisplayTranslation = false;
    }
  }
}