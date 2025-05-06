// <copyright file="UiTalkHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private unsafe void BattleTalkHandler(string addonName, int index)
    {
      IntPtr battleTalk = GameGuiInterface.GetAddonByName(addonName, index);
      if (battleTalk != IntPtr.Zero)
      {
        AtkUnitBase* battleTalkMaster = (AtkUnitBase*)battleTalk;
        while (battleTalkMaster->IsVisible)
        {
          this.battleTalkDisplayTranslation = true;
          this.battleTalkTextDimensions.X = battleTalkMaster->RootNode->Width * battleTalkMaster->Scale;
          this.battleTalkTextDimensions.Y = battleTalkMaster->RootNode->Height * battleTalkMaster->Scale;
          this.battleTalkTextPosition.X = battleTalkMaster->RootNode->X;
          this.battleTalkTextPosition.Y = battleTalkMaster->RootNode->Y;

          Thread.Sleep(this.delayBetweenVisibilityCheckForOverlay);
        }

        this.battleTalkDisplayTranslation = false;
      }

      this.battleTalkDisplayTranslation = false;
    }

    /*private void GetBattleTalk(ref SeString sender, ref SeString message, *//*ref BattleTalkOptions options*//*
      ref bool ishandled)
    {
      if (!this.configuration.TranslateBattleTalk)
      {
        return;
      }
#if DEBUG
      using StreamWriter logStream = new(this.configDir + "GetBattleTalkLog.txt", append: true);
#endif

      try
      {
#if DEBUG
        PluginLog.Debug(sender.TextValue + ": " + message.TextValue);
#endif

        string senderToTranslate = !sender.TextValue.IsNullOrEmpty() ? sender.TextValue : "System Message";
        string battleTextToTranslate = message.TextValue;

        BattleTalkMessage battleTalkMessage = this.FormatBattleTalkMessage(senderToTranslate, battleTextToTranslate);

#if DEBUG
        PluginLog.Debug($"Before DB Query attempt: {battleTalkMessage}");
#endif
        bool findings = FindBattleTalkMessage(battleTalkMessage);
#if DEBUG
        PluginLog.Debug(
          $"After DB Query attempt: {(findings ? "Message found in Db." : "Message not found in Db")}");
#endif

        // If the dialogue is not saved
        if (!findings)
        {
          if (!this.configuration.UseImGuiForBattleTalk)
          {
            string translatedBattleTalkMessage = this.Translate(battleTextToTranslate);
            string senderTranslation = this.Translate(senderToTranslate);
#if DEBUG
            PluginLog.Debug(translatedBattleTalkMessage);
#endif
            if (this.configuration.TranslateNpcNames)
            {
              sender = senderTranslation == string.Empty ? sender : senderTranslation;
              message = translatedBattleTalkMessage;

              BattleTalkMessage translatedBattleTalkData = new BattleTalkMessage(senderToTranslate, battleTextToTranslate,
                LangIdentify(battleTextToTranslate),
                LangIdentify(senderToTranslate), senderTranslation, translatedBattleTalkMessage, langDict[languageInt].Code,
                this.configuration.ChosenTransEngine, DateTime.Now, DateTime.Now);
#if DEBUG
              logStream.WriteLineAsync($"Before Talk Messages table data insertion:  {translatedBattleTalkData}");
#endif
              string result = InsertBattleTalkData(translatedBattleTalkData);
#if DEBUG
              PluginLog.Debug($"BattleTalk Message DB Insert operation result: {result}");
#endif
            }
            else
            {
              message = translatedBattleTalkMessage;

              BattleTalkMessage translatedBattleTalkData = new BattleTalkMessage(senderToTranslate, battleTextToTranslate,
                LangIdentify(battleTextToTranslate),
                LangIdentify(senderToTranslate), string.Empty, translatedBattleTalkMessage, langDict[languageInt].Code,
                this.configuration.ChosenTransEngine, DateTime.Now, DateTime.Now);

              string result = InsertBattleTalkData(translatedBattleTalkData);
#if DEBUG
              PluginLog.Debug($"Using BattleTalk Overlay - BattleTalk Message DB Insert operation result: {result}");
#endif
            }
#if DEBUG
            PluginLog.Debug($"Using BattleTalk Replace - {sender.TextValue}: {message.TextValue}");
#endif
          }
          else
          {
            if (this.configuration.TranslateNpcNames)
            {
              this.currentSenderTranslationId = Environment.TickCount;
              this.currentSenderTranslation = Resources.WaitingForTranslation;
              Task.Run(() =>
              {
                int nameId = this.currentSenderTranslationId;
                string senderTranslation = this.Translate(senderToTranslate);
                this.senderTranslationSemaphore.Wait();
                if (nameId == this.currentSenderTranslationId)
                {
                  this.currentSenderTranslation = senderTranslation;
                }

                this.senderTranslationSemaphore.Release();
              });
            }

            this.currentBattleTalkTranslationId = Environment.TickCount;
            this.currentBattleTalkTranslation = Resources.WaitingForTranslation;
            Task.Run(() =>
            {
              int id = this.currentBattleTalkTranslationId;
              string translation = this.Translate(battleTextToTranslate);
              this.battleTalkTranslationSemaphore.Wait();
              if (id == this.currentBattleTalkTranslationId)
              {
                this.currentBattleTalkTranslation = translation;
              }

              this.battleTalkTranslationSemaphore.Release();
#if DEBUG
              PluginLog.Debug($"Before if BattleTalk translation: {this.currentBattleTalkTranslation}");
#endif
              if (this.currentSenderTranslation != Resources.WaitingForTranslation && this.currentBattleTalkTranslation != Resources.WaitingForTranslation)
              {
                BattleTalkMessage translatedBattleTalkData = new BattleTalkMessage(senderToTranslate, battleTextToTranslate,
                  LangIdentify(battleTextToTranslate),
                  LangIdentify(senderToTranslate),
                  this.configuration.TranslateNpcNames ? this.currentSenderTranslation : string.Empty,
                  this.currentBattleTalkTranslation, langDict[languageInt].Code,
                  this.configuration.ChosenTransEngine, DateTime.Now, DateTime.Now);
                string result = InsertBattleTalkData(translatedBattleTalkData);
#if DEBUG
                PluginLog.Debug($"BattleTalk Message DB Insert operation result: {result}");
#endif
              }
            });
          }
        }
        else
        { // if the data is already in the DB
          if (!this.configuration.UseImGuiForBattleTalk)
          {
            string translatedBattleMessage = FoundBattleTalkMessage.TranslatedBattleTalkMessage;
            string senderTranslation = FoundBattleTalkMessage.TranslatedSenderName;
#if DEBUG
            PluginLog.Debug($"From database - Name: {senderTranslation}, Message: {translatedBattleMessage}");
#endif
            if (this.configuration.TranslateNpcNames)
            {
              sender = senderTranslation == string.Empty || senderTranslation == null || senderTranslation == string.Empty ? sender : senderTranslation;
              message = translatedBattleMessage;
            }
            else
            {
              message = translatedBattleMessage;
            }
#if DEBUG
            PluginLog.Debug(sender.TextValue + ": " + message.TextValue);
#endif
          }
          else
          {
            if (this.configuration.TranslateNpcNames)
            {
              this.currentSenderTranslationId = Environment.TickCount;
              this.currentSenderTranslation = Resources.WaitingForTranslation;
              Task.Run(() =>
              {
                int nameId = this.currentSenderTranslationId;
                string senderTranslation = FoundBattleTalkMessage.TranslatedSenderName;
                this.senderTranslationSemaphore.Wait();
                if (nameId == this.currentSenderTranslationId)
                {
                  this.currentSenderTranslation = senderTranslation;
#if DEBUG
                  PluginLog.Debug($"Using overlay - name found in DB: {senderTranslation} ");
#endif
                }

                this.senderTranslationSemaphore.Release();
              });
            }

            this.currentBattleTalkTranslationId = Environment.TickCount;
            this.currentBattleTalkTranslation = Resources.WaitingForTranslation;
            Task.Run(() =>
            {
              int id = this.currentBattleTalkTranslationId;
              string translatedBattleTalkMessage = FoundBattleTalkMessage.TranslatedBattleTalkMessage;
              this.battleTalkTranslationSemaphore.Wait();
              if (id == this.currentBattleTalkTranslationId)
              {
                this.currentBattleTalkTranslation = translatedBattleTalkMessage;
#if DEBUG
                PluginLog.Debug($"Using overlay - message found in DB: {translatedBattleTalkMessage} ");
#endif
              }

              this.battleTalkTranslationSemaphore.Release();
            });
          }
        }
      }
      catch (Exception e)
      {
        PluginLog.Debug("Exception: " + e.StackTrace);
        throw;
      }
    }*/
  }
}
