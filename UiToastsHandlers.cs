// <copyright file="UiToastsHandlers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Logging;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Properties;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private unsafe void ToastHandler(string toastName, int index)
    {
      var toastByName = this.gameGui.GetAddonByName(toastName, index);
      if (toastByName != IntPtr.Zero)
      {
        var toastByNameMaster = (AtkUnitBase*)toastByName;
        if (toastByNameMaster->IsVisible)
        {
          this.toastDisplayTranslation = true;
          this.toastTranslationTextDimensions.X = toastByNameMaster->RootNode->Width * toastByNameMaster->Scale * 2;
          this.toastTranslationTextDimensions.Y = toastByNameMaster->RootNode->Height * toastByNameMaster->Scale;
          this.toastTranslationTextPosition.X = toastByNameMaster->RootNode->X;
          this.toastTranslationTextPosition.Y = toastByNameMaster->RootNode->Y;
        }
        else
        {
          this.toastDisplayTranslation = false;
        }
      }
      else
      {
        this.toastDisplayTranslation = false;
      }
    }

    private unsafe void QuestToastHandler(string questToastName, int index)
    {
      var questToastByName = this.gameGui.GetAddonByName(questToastName, index);
      if (questToastByName != IntPtr.Zero)
      {
        var questToastByNameMaster = (AtkUnitBase*)questToastByName;
        if (questToastByNameMaster->IsVisible)
        {
          this.questToastDisplayTranslation = true;
          this.questToastTranslationTextDimensions.X = questToastByNameMaster->RootNode->Width * questToastByNameMaster->Scale * 2;
          this.questToastTranslationTextDimensions.Y = questToastByNameMaster->RootNode->Height * questToastByNameMaster->Scale;
          this.questToastTranslationTextPosition.X = questToastByNameMaster->RootNode->X;
          this.questToastTranslationTextPosition.Y = questToastByNameMaster->RootNode->Y;
        }
        else
        {
          this.questToastDisplayTranslation = false;
        }
      }
      else
      {
        this.questToastDisplayTranslation = false;
      }
    }

    private unsafe void ClassChangeToastHandler(string classChangeToastName, int index)
    {
      var classChangeToastByName = this.gameGui.GetAddonByName(classChangeToastName, index);
      if (classChangeToastByName != IntPtr.Zero)
      {
        var classChangeToastByNameMaster = (AtkUnitBase*)classChangeToastByName;
        if (classChangeToastByNameMaster->IsVisible)
        {
          this.classChangeToastDisplayTranslation = true;
          this.classChangeToastTranslationTextDimensions.X = classChangeToastByNameMaster->RootNode->Width * classChangeToastByNameMaster->Scale * 2;
          this.classChangeToastTranslationTextDimensions.Y = classChangeToastByNameMaster->RootNode->Height * classChangeToastByNameMaster->Scale;
          this.classChangeToastTranslationTextPosition.X = classChangeToastByNameMaster->RootNode->X;
          this.classChangeToastTranslationTextPosition.Y = classChangeToastByNameMaster->RootNode->Y;
        }
        else
        {
          this.classChangeToastDisplayTranslation = false;
        }
      }
      else
      {
        this.classChangeToastDisplayTranslation = false;
      }
    }

    private unsafe void ErrorToastHandler(string toastName, int index)
    {
      var errorToastByName = this.gameGui.GetAddonByName(toastName, index);
      if (errorToastByName != IntPtr.Zero)
      {
        var errorToastByNameMaster = (AtkUnitBase*)errorToastByName;
        if (errorToastByNameMaster->IsVisible)
        {
          this.errorToastDisplayTranslation = true;
          this.errorToastTranslationTextDimensions.X = errorToastByNameMaster->RootNode->Width * errorToastByNameMaster->Scale * 2;
          this.errorToastTranslationTextDimensions.Y = errorToastByNameMaster->RootNode->Height * errorToastByNameMaster->Scale * 2;
          this.errorToastTranslationTextPosition.X = errorToastByNameMaster->RootNode->X;
          this.errorToastTranslationTextPosition.Y = errorToastByNameMaster->RootNode->Y;
        }
        else
        {
          this.errorToastDisplayTranslation = false;
        }
      }
      else
      {
        this.errorToastDisplayTranslation = false;
      }
    }

    private unsafe void AreaToastHandler(string areaToastName, int index)
    {
      var areaToastByName = this.gameGui.GetAddonByName(areaToastName, index);
      if (areaToastByName != IntPtr.Zero)
      {
        var areaToastByNameMaster = (AtkUnitBase*)areaToastByName;
        if (areaToastByNameMaster->IsVisible)
        {
          this.areaToastDisplayTranslation = true;
          this.areaToastTranslationTextDimensions.X = areaToastByNameMaster->RootNode->Width * areaToastByNameMaster->Scale * 2;
          this.areaToastTranslationTextDimensions.Y = areaToastByNameMaster->RootNode->Height * areaToastByNameMaster->Scale;
          this.areaToastTranslationTextPosition.X = areaToastByNameMaster->RootNode->X;
          this.areaToastTranslationTextPosition.Y = areaToastByNameMaster->RootNode->Y;
        }
        else
        {
          this.areaToastDisplayTranslation = false;
        }
      }
      else
      {
        this.areaToastDisplayTranslation = false;
      }
    }

    private unsafe void WideTextToastHandler(string wideTextToastName, int index)
    {
      var wideTextToastByName = this.gameGui.GetAddonByName(wideTextToastName, index);
      if (wideTextToastByName != IntPtr.Zero)
      {
        var wideTextToastByNameMaster = (AtkUnitBase*)wideTextToastByName;
        if (wideTextToastByNameMaster->IsVisible)
        {
          this.wideTextToastDisplayTranslation = true;
          this.wideTextToastTranslationTextDimensions.X = wideTextToastByNameMaster->RootNode->Width * wideTextToastByNameMaster->Scale * 2;
          this.wideTextToastTranslationTextDimensions.Y = wideTextToastByNameMaster->RootNode->Height * wideTextToastByNameMaster->Scale;
          this.wideTextToastTranslationTextPosition.X = wideTextToastByNameMaster->RootNode->X;
          this.wideTextToastTranslationTextPosition.Y = wideTextToastByNameMaster->RootNode->Y;
        }
        else
        {
          this.wideTextToastDisplayTranslation = false;
        }
      }
      else
      {
        this.wideTextToastDisplayTranslation = false;
      }
    }

    private unsafe void AddonHandlers(string addonName, int index)
    {
      var addonByName = this.gameGui.GetAddonByName(addonName, index);
      if (addonByName != IntPtr.Zero)
      {
        var addonByNameMaster = (AtkUnitBase*)addonByName;
        if (addonByNameMaster->IsVisible)
        {
          this.addonDisplayTranslation = true;
          this.addonTranslationTextDimensions.X = addonByNameMaster->RootNode->Width * addonByNameMaster->Scale * 2;
          this.addonTranslationTextDimensions.Y = addonByNameMaster->RootNode->Height * addonByNameMaster->Scale;
          this.addonTranslationTextPosition.X = addonByNameMaster->RootNode->X;
          this.addonTranslationTextPosition.Y = addonByNameMaster->RootNode->Y;
        }
        else
        {
          this.addonDisplayTranslation = false;
        }
      }
      else
      {
        this.addonDisplayTranslation = false;
      }
    }

    private void OnQuestToast(ref SeString message, ref QuestToastOptions options, ref bool ishandled)
    {
      if (!this.configuration.TranslateToast || !this.configuration.TranslateQuestToast || !this.configuration.TranslateWideTextToast)
      {
        return;
      }

      try
      {
        var messageTextToTranslate = message.TextValue;

        if (this.configuration.DoNotUseImGuiForToasts && this.configuration.TranslateQuestToast && this.configuration.TranslateWideTextToast)
        {
          var messageTranslatedText = Translate(messageTextToTranslate);

          message = messageTranslatedText;
        }
        else
        {
          this.currentQuestToastTranslationId = Environment.TickCount;
          this.currentQuestToastTranslation = Resources.WaitingForTranslation;
          Task.Run(() =>
          {
            var messageId = this.currentQuestToastTranslationId;
            var messageTranslation = Translate(messageTextToTranslate);
            this.questToastTranslationSemaphore.Wait();
            if (messageId == this.currentQuestToastTranslationId)
            {
              var encoded = new UTF8Encoding();
              var utf8MessageTranslationBytes = encoded.GetBytes(messageTranslation);
              var utf8MessageTranslationString = encoded.GetString(utf8MessageTranslationBytes);
              this.currentQuestToastTranslation = utf8MessageTranslationString;
            }

            this.questToastTranslationSemaphore.Release();
          });
        }
      }
      catch (Exception e)
      {
        PluginLog.Log("Exception: " + e.StackTrace);
        throw;
      }
    }

    private void OnErrorToast(ref SeString message, ref bool ishandled)
    {
      if (!this.configuration.TranslateErrorToast)
      {
        return;
      }
#if DEBUG
      using StreamWriter logStream = new(this.ConfigDir + "GetToastLog.txt", append: true);
#endif
      var messageTextToTranslate = message.TextValue;
      ToastMessage toastToSave = this.FormatToastMessage("Error", message.TextValue);

#if DEBUG
      PluginLog.LogFatal($"Before DB Query attempt: {toastToSave}");
#endif
      var findings = this.FindToastMessage(toastToSave);
#if DEBUG
      PluginLog.LogFatal(
        $"After DB Query attempt: {(findings ? "ErrorToastMessage found in Db." : "ErrorToastMessage not found in Db")}");
#endif

      // if the toast isn't saved
      if (!findings)
      {
        try
        {
          if (this.configuration.DoNotUseImGuiForToasts)
          {
            var translatedToastMessage = Translate(message.TextValue);
            message = translatedToastMessage;
            var translatedToastData = new ToastMessage("Error", message.TextValue, LangIdentify(message.TextValue),
              translatedToastMessage, Codes[languageInt], this.configuration.ChosenTransEngine, DateTime.Now,
              DateTime.Now);
#if DEBUG
            logStream.WriteLineAsync($"Before Toast Messages table data insertion:  {translatedToastData}");
#endif
            var result = this.InsertToastMessageData(translatedToastData);
#if DEBUG
            PluginLog.LogError($"Toast Message DB Insert operation result: {result}");
#endif
          }
          else
          {
            this.currentErrorToastTranslationId = Environment.TickCount;
            this.currentErrorToastTranslation = Resources.WaitingForTranslation;
            Task.Run(() =>
            {
              var messageId = this.currentErrorToastTranslationId;
              var messageTranslation = Translate(messageTextToTranslate);
              this.errorToastTranslationSemaphore.Wait();
              if (messageId == this.currentErrorToastTranslationId)
              {
                this.currentErrorToastTranslation = messageTranslation;
              }

              this.errorToastTranslationSemaphore.Release();
#if DEBUG
              PluginLog.LogError($"Before if ErrorToast translation: {this.currentErrorToastTranslation}");
#endif
              if (this.currentErrorToastTranslation != Resources.WaitingForTranslation)
              {
                var translatedErrorToastData = new ToastMessage("Error", messageTextToTranslate,
                  LangIdentify(messageTextToTranslate), this.currentErrorToastTranslation,
                  Codes[languageInt], this.configuration.ChosenTransEngine, DateTime.Now, DateTime.Now);
                var result = this.InsertToastMessageData(translatedErrorToastData);
#if DEBUG
                PluginLog.LogError($"ToastMessage DB Insert operation result: {result}");
#endif
              }
            });
          }
        }
        catch (Exception e)
        {
          PluginLog.Log("Exception: " + e.StackTrace);
          throw;
        }
      }
      else
      {
        if (this.configuration.DoNotUseImGuiForToasts)
        {
          message = this.FoundToastMessage.TranslatedToastMessage;
#if DEBUG
          PluginLog.Error($"Text replacement - message found in DB: {message.TextValue} ");
#endif
        }
        else
        {
          this.currentErrorToastTranslationId = Environment.TickCount;
          this.currentErrorToastTranslation = Resources.WaitingForTranslation;
          Task.Run(() =>
          {
            var messageId = this.currentErrorToastTranslationId;
            var messageTranslation = this.FoundToastMessage.TranslatedToastMessage;
            this.errorToastTranslationSemaphore.Wait();
            if (messageId == this.currentErrorToastTranslationId)
            {
              this.currentErrorToastTranslation = messageTranslation;
#if DEBUG
              PluginLog.Error($"Using overlay - message found in DB: {messageTranslation} ");
#endif
            }

            this.errorToastTranslationSemaphore.Release();
          });
        }
      }
    }

    private void OnToast(ref SeString message, ref ToastOptions options, ref bool ishandled)
    {
#if DEBUG
      using StreamWriter logStream = new(this.ConfigDir + "GetToastLog.txt", append: true);
#endif
      if (!this.configuration.TranslateAreaToast ||
                                                 !this.configuration.TranslateClassChangeToast ||
                                                 !this.configuration.TranslateWideTextToast)
      {
        return;
      }

      try
      {
        var messageTextToTranslate = message.TextValue;

        if (this.configuration.DoNotUseImGuiForToasts)
        {
          var messageTranslatedText = Translate(messageTextToTranslate);

          message = messageTranslatedText;
        }
        else
        {
          this.currentToastTranslationId = Environment.TickCount;
          this.currentToastTranslation = Resources.WaitingForTranslation;
          Task.Run(() =>
          {
            var messageId = this.currentToastTranslationId;
            var messageTranslation = Translate(messageTextToTranslate);
            this.toastTranslationSemaphore.Wait();
            if (messageId == this.currentToastTranslationId)
            {
              this.currentToastTranslation = messageTranslation;
            }

            this.toastTranslationSemaphore.Release();
          });
        }
      }
      catch (Exception e)
      {
        PluginLog.Log("Exception: " + e.StackTrace);
        throw;
      }
    }
  }
}
