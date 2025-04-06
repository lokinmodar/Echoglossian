// <copyright file="UiBattleTalkAsyncHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Memory;
using Dalamud.Utility;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Properties;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Humanizer;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private readonly int delayBetweenTriesToTranslateBattleTalk = 50;

    private BattleTalkMessage lastBattleTalkMessage = null;

    private HashSet<string> translatedBattleTalkTexts = new HashSet<string>();

    private unsafe void ManageBattleTalk()
    {
      Task.Run(() =>
        {
          for (int i = 0; i < 5; i++)
          {
            var addon = GameGuiInterface.GetAddonByName("_BattleTalk");
            var battleTalkAddon = (AtkUnitBase*)addon;
            if (battleTalkAddon == null || !battleTalkAddon->IsVisible)
            {
              Thread.Sleep(this.delayBetweenTriesToTranslateBattleTalk);
              continue;
            }

            var nameToTranslate = string.Empty;

            var nameNode = battleTalkAddon->GetTextNodeById(4);
            if (nameNode != null && !nameNode->NodeText.IsEmpty)
            {
              nameToTranslate = MemoryHelper.ReadSeStringAsString(out _, (nint)nameNode->NodeText.StringPtr.Value);
            }

            var textNode = battleTalkAddon->GetTextNodeById(6);
            if (textNode == null || textNode->NodeText.IsEmpty)
            {
              Thread.Sleep(this.delayBetweenTriesToTranslateBattleTalk);
              continue;
            }

            var textToTranslate = MemoryHelper.ReadSeStringAsString(out _, (nint)textNode->NodeText.StringPtr.Value);
            if (this.translatedBattleTalkTexts.Contains(sanitizer.Sanitize(textToTranslate)))
            {
              Thread.Sleep(this.delayBetweenTriesToTranslateBattleTalk);
              continue;
            }

            PluginLog.Debug($"ManageBattleTalk text to translate {nameToTranslate}: {textToTranslate}");

            this.lastBattleTalkMessage = new BattleTalkMessage(
                  senderName: nameToTranslate,
                  originalBattleTalkMessage: textToTranslate,
                  originalSenderNameLang: ClientStateInterface.ClientLanguage.Humanize(),
                  translatedBattleTalkMessage: string.Empty,
                  originalBattleTalkMessageLang: ClientStateInterface.ClientLanguage.Humanize(),
                  translationLang: langDict[languageInt].Code,
                  translationEngine: this.configuration.ChosenTransEngine,
                  translatedSenderName: string.Empty,
                  createdDate: DateTime.Now,
                  updatedDate: DateTime.Now);

            var foundBattleTalk = this.FindAndReturnBattleTalkMessage(this.lastBattleTalkMessage);
            if (foundBattleTalk == null)
            {
              string textTranslation = this.Translate(textToTranslate);
              string nameTranslation = nameToTranslate.IsNullOrEmpty() ? string.Empty : this.Translate(nameToTranslate);
              this.lastBattleTalkMessage.TranslatedSenderName = nameTranslation;
              this.lastBattleTalkMessage.TranslatedBattleTalkMessage = textTranslation;
              this.translatedBattleTalkTexts.Add(textTranslation);
              InsertBattleTalkData(this.lastBattleTalkMessage);

              return;
            }

            this.lastBattleTalkMessage.TranslatedSenderName = foundBattleTalk.TranslatedSenderName;
            this.lastBattleTalkMessage.TranslatedBattleTalkMessage = foundBattleTalk.TranslatedBattleTalkMessage;
            this.translatedBattleTalkTexts.Add(foundBattleTalk.TranslatedBattleTalkMessage);

            return;
          }
        });
    }

    private unsafe void TranslateBattleTalkReplacing()
    {
      PluginLog.Debug($"TranslateBattleTalkReplacing");

      if (this.lastBattleTalkMessage == null)
      {
        return;
      }

      try
      {
        var addon = GameGuiInterface.GetAddonByName("_BattleTalk");
        var battleTalkAddon = (AtkUnitBase*)addon;
        if (battleTalkAddon == null || !battleTalkAddon->IsVisible)
        {
          return;
        }

        var nameNode = battleTalkAddon->GetTextNodeById(4);
        var textNode = battleTalkAddon->GetTextNodeById(6);
        if (textNode == null || textNode->NodeText.IsEmpty)
        {
          return;
        }

        if (this.configuration.TranslateNpcNames && nameNode != null && !nameNode->NodeText.IsEmpty)
        {
          var translatedSName = this.lastBattleTalkMessage.TranslatedSenderName;

          if (this.configuration.RemoveDiacriticsWhenUsingReplacementTalkBTalk && translatedSName != null)
          {
            translatedSName = this.RemoveDiacritics(translatedSName, this.SpecialCharsSupportedByGameFont);
          }

          nameNode->SetText(translatedSName);
        }

        var parentNode = battleTalkAddon->GetNodeById(1);
        var nineGridNode = battleTalkAddon->GetNodeById(7);
        textNode->TextFlags = (byte)(TextFlags)((byte)TextFlags.WordWrap | (byte)TextFlags.MultiLine | (byte)TextFlags.AutoAdjustNodeSize);
        textNode->FontSize = 14;
        var timerNode = battleTalkAddon->GetNodeById(2);
        textNode->SetWidth(640);
        timerNode->SetXShort((short)(textNode->GetWidth() + 40));

        parentNode->SetWidth((ushort)(textNode->GetWidth() + 128));
        parentNode->SetHeight((ushort)(textNode->GetHeight() + 48));
        nineGridNode->SetWidth((ushort)(textNode->GetWidth() + 128));
        nineGridNode->SetHeight((ushort)(textNode->GetHeight() + 48));

        var translatedBTMessage = this.lastBattleTalkMessage.TranslatedBattleTalkMessage;

        if (this.configuration.RemoveDiacriticsWhenUsingReplacementTalkBTalk && translatedBTMessage != null)
        {
          translatedBTMessage = this.RemoveDiacritics(translatedBTMessage, this.SpecialCharsSupportedByGameFont);
        }

        textNode->SetText(translatedBTMessage);
        textNode->ResizeNodeForCurrentText();
      }
      catch (Exception e)
      {
        PluginLog.Debug("TranslateBattleTalkReplacing Exception: " + e);
      }
    }

    private unsafe void TranslateBattleTalkUsingImGuiAndSwapping()
    {
      PluginLog.Debug($"TranslateBattleTalkUsingImGuiAndSwapping");

      if (!this.configuration.TranslateBattleTalk)
      {
        return;
      }

      if (!this.configuration.UseImGuiForBattleTalk)
      {
        return;
      }

      if (!this.configuration.SwapTextsUsingImGui)
      {
        return;
      }

      if (this.lastBattleTalkMessage == null)
      {
        return;
      }

      try
      {
        var addon = GameGuiInterface.GetAddonByName("_BattleTalk");
        var battleTalkAddon = (AtkUnitBase*)addon;
        if (battleTalkAddon == null || !battleTalkAddon->IsVisible)
        {
          return;
        }

        var nameNode = battleTalkAddon->GetTextNodeById(4);
        var textNode = battleTalkAddon->GetTextNodeById(6);
        if (textNode == null || textNode->NodeText.IsEmpty)
        {
          return;
        }

        var nameToTranslate = MemoryHelper.ReadSeStringAsString(out _, (nint)nameNode->NodeText.StringPtr.Value);
        var textToTranslate = MemoryHelper.ReadSeStringAsString(out _, (nint)textNode->NodeText.StringPtr.Value);

        PluginLog.Debug($"TranslateBattleTalkUsingImGuiAndSwapping text to translate {nameToTranslate}: {textToTranslate}");

        if (textNode == null || textNode->NodeText.IsEmpty)
        {
          return;
        }

        if (this.configuration.TranslateNpcNames && nameNode != null && !nameNode->NodeText.IsEmpty)
        {
          var translatedSName = this.lastBattleTalkMessage.TranslatedSenderName;

          if (this.configuration.RemoveDiacriticsWhenUsingReplacementTalkBTalk && translatedSName != null)
          {
            translatedSName = this.RemoveDiacritics(translatedSName, this.SpecialCharsSupportedByGameFont);
          }

          nameNode->SetText(translatedSName);
        }

        var textTranslation = this.Translate(textToTranslate);
        var nameTranslation = this.configuration.TranslateNpcNames ? (nameToTranslate.IsNullOrEmpty() ? string.Empty : this.Translate(nameToTranslate)) : nameToTranslate;

        if (this.configuration.TranslateNpcNames && nameNode != null && !nameNode->NodeText.IsEmpty)
        {
          var translatedSName = this.lastBattleTalkMessage.TranslatedSenderName;

          if (this.configuration.RemoveDiacriticsWhenUsingReplacementTalkBTalk && translatedSName != null)
          {
            translatedSName = this.RemoveDiacritics(translatedSName, this.SpecialCharsSupportedByGameFont);
          }

          nameNode->SetText(translatedSName);
        }

        var parentNode = battleTalkAddon->GetNodeById(1);
        var nineGridNode = battleTalkAddon->GetNodeById(7);
        textNode->TextFlags = (byte)(TextFlags)((byte)TextFlags.WordWrap | (byte)TextFlags.MultiLine | (byte)TextFlags.AutoAdjustNodeSize);
        textNode->FontSize = 14;
        var timerNode = battleTalkAddon->GetNodeById(2);
        textNode->SetWidth(640);
        timerNode->SetXShort((short)(textNode->GetWidth() + 40));

        parentNode->SetWidth((ushort)(textNode->GetWidth() + 128));
        parentNode->SetHeight((ushort)(textNode->GetHeight() + 48));
        nineGridNode->SetWidth((ushort)(textNode->GetWidth() + 128));
        nineGridNode->SetHeight((ushort)(textNode->GetHeight() + 48));

        var translatedBTMessage = this.lastBattleTalkMessage.TranslatedBattleTalkMessage;

        if (this.configuration.RemoveDiacriticsWhenUsingReplacementTalkBTalk && translatedBTMessage != null)
        {
          translatedBTMessage = this.RemoveDiacritics(translatedBTMessage, this.SpecialCharsSupportedByGameFont);
        }

        textNode->SetText(translatedBTMessage);
        textNode->ResizeNodeForCurrentText();

        this.currentSenderTranslationId = Environment.TickCount;
        this.currentSenderTranslation = Resources.WaitingForTranslation;
        Task.Run(() =>
        {
          PluginLog.Debug($"TranslateBattleTalkUsingImGuiAndSwapping SenderName task");
          int nameId = this.currentSenderTranslationId;
          string senderTranslation = nameToTranslate;
          this.senderTranslationSemaphore.Wait();
          if (nameId == this.currentSenderTranslationId)
          {
            this.currentSenderTranslation = nameToTranslate;
          }

          this.senderTranslationSemaphore.Release();
        });

        this.currentBattleTalkTranslationId = Environment.TickCount;
        this.currentBattleTalkTranslation = textToTranslate;
        Task.Run(() =>
        {
          PluginLog.Debug($"TranslateBattleTalkUsingImGuiAndSwapping BattleTalk task");
          int id = this.currentBattleTalkTranslationId;
          string translation = textToTranslate;
          this.battleTalkTranslationSemaphore.Wait();
          if (id == this.currentBattleTalkTranslationId)
          {
            this.currentBattleTalkTranslation = textToTranslate;
          }

          this.battleTalkTranslationSemaphore.Release();

          // this.BattleTalkHandler("_BattleTalk", 1);
        }).ContinueWith(task =>
        {
          this.BattleTalkHandler("_BattleTalk", 1);
        });

        // this.BattleTalkHandler("_BattleTalk", 1); here it freezes the game and only sound goes on
      }
      catch (Exception e)
      {
        PluginLog.Debug("TranslateBattleTalkUsingImGuiAndSwapping Exception: " + e);
      }
    }

    public unsafe void TranslateBattleTalkUsingImGuiWithoutSwapping()
    {
      PluginLog.Debug($"TranslateBattleTalkUsingImGuiWithoutSwapping");

      if (!this.configuration.TranslateBattleTalk)
      {
        return;
      }

      if (!this.configuration.UseImGuiForBattleTalk)
      {
        return;
      }

      if (this.lastBattleTalkMessage == null)
      {
        return;
      }

      try
      {
        var addon = GameGuiInterface.GetAddonByName("_BattleTalk");
        var battleTalkAddon = (AtkUnitBase*)addon;
        if (battleTalkAddon == null || !battleTalkAddon->IsVisible)
        {
          return;
        }

        var nameNode = battleTalkAddon->GetTextNodeById(4);
        var textNode = battleTalkAddon->GetTextNodeById(6);
        if (textNode == null || textNode->NodeText.IsEmpty)
        {
          return;
        }

        var nameToTranslate = MemoryHelper.ReadSeStringAsString(out _, (nint)nameNode->NodeText.StringPtr.Value);
        var textToTranslate = MemoryHelper.ReadSeStringAsString(out _, (nint)textNode->NodeText.StringPtr.Value);

        PluginLog.Debug($"TranslateBattleTalkUsingImGuiWithoutSwapping text to translate {nameToTranslate}: {textToTranslate}");

        Task.Run(() =>
        {
          PluginLog.Debug($"TranslateBattleTalkUsingImGuiWithoutSwapping task");
          var textTranslation = this.lastBattleTalkMessage.TranslatedBattleTalkMessage;
          var nameTranslation = this.configuration.TranslateNpcNames ? (nameToTranslate.IsNullOrEmpty() ? string.Empty : this.lastBattleTalkMessage.TranslatedSenderName) : nameToTranslate;

          if (textTranslation.IsNullOrEmpty())
          {
            return;
          }

          this.currentSenderTranslationId = Environment.TickCount;
          this.currentSenderTranslation = Resources.WaitingForTranslation;

          int nameId = this.currentSenderTranslationId;
          string senderTranslation = nameTranslation;
          this.senderTranslationSemaphore.Wait();
          if (nameId == this.currentSenderTranslationId)
          {
            this.currentSenderTranslation = senderTranslation;
          }

          this.senderTranslationSemaphore.Release();

          this.currentBattleTalkTranslationId = Environment.TickCount;
          this.currentBattleTalkTranslation = Resources.WaitingForTranslation;

          int id = this.currentBattleTalkTranslationId;
          string translation = textTranslation;
          this.battleTalkTranslationSemaphore.Wait();
          if (id == this.currentBattleTalkTranslationId)
          {
            this.currentBattleTalkTranslation = translation;
          }

          this.battleTalkTranslationSemaphore.Release();

          // this.BattleTalkHandler("_BattleTalk", 1);
        }).ContinueWith(task =>
        {
          this.BattleTalkHandler("_BattleTalk", 1);
        });

        // this.BattleTalkHandler("_BattleTalk", 1); here it freezes the game and only sound goes on
      }
      catch (Exception e)
      {
        PluginLog.Debug("TranslateBattleTalkUsingImGuiWithoutSwapping Exception: " + e);
      }
    }

    private unsafe void TranslateBattleTalkUsingImGui()
    {
      PluginLog.Debug($"TranslateBattleTalkUsingImGui: YES!");

      if (this.configuration.SwapTextsUsingImGui)
      {
        this.translatedName = string.Empty;
        this.translatedText = string.Empty;
        this.TranslateBattleTalkUsingImGuiAndSwapping();
        return;
      }

      this.TranslateBattleTalkUsingImGuiWithoutSwapping();
    }

    private unsafe void UiBattleTalkAsyncHandler(AddonEvent type, AddonArgs args)
    {
      PluginLog.Debug($"UiBattleTalkAsyncHandler: {type} {args.AddonName}");

      var icDirector = EventFramework.Instance() != null ? EventFramework.Instance()->GetInstanceContentDirector() : null;

      var isInstanceContent = icDirector != null && icDirector->InstanceContentType != 0;

      if (isInstanceContent)
      {
        PluginLog.Debug($"UiBattleTalkAsyncHandler: isInstanceContent {isInstanceContent}");
      }

      if (!this.configuration.TranslateBattleTalk)
      {
        return;
      }

      switch (type)
      {
        case AddonEvent.PreReceiveEvent:
          // to be sure we don't show the same text twice
          this.lastBattleTalkMessage = null;
          return;
        case AddonEvent.PreDraw:
          if (this.configuration.UseImGuiForBattleTalk)
          {
            this.TranslateBattleTalkUsingImGui(); // had disabled due to concurrency issues for now
          }
          else
          {
            this.TranslateBattleTalkReplacing();
          }

          return;
      }

      try
      {
        this.lastBattleTalkMessage = null;
        this.ManageBattleTalk();
      }
      catch (Exception e)
      {
        PluginLog.Debug("UiTalkAsyncHandler Exception: " + e);
      }
    }
  }
}