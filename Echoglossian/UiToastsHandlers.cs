﻿// <copyright file="UiToastsHandlers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.IO;
using System.Runtime.InteropServices;
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
      var toastByName = GameGui.GetAddonByName(toastName, index);
      if (toastByName != IntPtr.Zero)
      {
        var toastByNameMaster = (AtkUnitBase*)toastByName;
        if (toastByNameMaster->IsVisible)
        {
          this.toastDisplayTranslation = true;
          this.toastTranslationTextDimensions.X =
            toastByNameMaster->RootNode->Width * toastByNameMaster->Scale * 2;
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
      var questToastByName = GameGui.GetAddonByName(questToastName, index);
      if (questToastByName != IntPtr.Zero)
      {
        var questToastByNameMaster = (AtkUnitBase*)questToastByName;
        if (questToastByNameMaster->IsVisible)
        {
          this.questToastDisplayTranslation = true;
          this.questToastTranslationTextDimensions.X =
            questToastByNameMaster->RootNode->Width * questToastByNameMaster->Scale * 2;
          this.questToastTranslationTextDimensions.Y =
            questToastByNameMaster->RootNode->Height * questToastByNameMaster->Scale;
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
      // TODO: Rework translation code to async
      if (!this.configuration.TranslateClassChangeToast)
      {
        return;
      }

      var classChangeToastByName = GameGui.GetAddonByName(classChangeToastName, index);

      if (classChangeToastByName != IntPtr.Zero)
      {
        var classChangeToastByNameMaster = (AtkUnitBase*)classChangeToastByName;
        if (classChangeToastByNameMaster->IsVisible)
        {
          AtkTextNode* textNode = null;
          for (var i = 0; i < classChangeToastByNameMaster->UldManager.NodeListCount; i++)
          {
            if (classChangeToastByNameMaster->UldManager.NodeList[i]->Type != NodeType.Text)
            {
              continue;
            }

            textNode = (AtkTextNode*)classChangeToastByNameMaster->UldManager.NodeList[i];
            break;
          }

          if (textNode == null)
          {
            return;
          }

          try
          {
            var messageToTranslate = Marshal.PtrToStringUTF8(new IntPtr(textNode->NodeText.StringPtr));

            if (!this.configuration.UseImGuiForToasts)
            {
#if DEBUG
              PluginLog.LogVerbose("Not Using Imgui - Translate ClassChange toast");
#endif
              this.currentClassChangeToastTranslationId = Environment.TickCount;
              this.currentClassChangeToastTranslation = Resources.WaitingForTranslation;
#if DEBUG
              PluginLog.LogVerbose("Not Using Imgui - Translate ClassChange toast 1");
#endif
              textNode->SetText(Resources.WaitingForTranslation);
#if DEBUG
              PluginLog.LogVerbose("Not Using Imgui - Translate ClassChange toast - 2");
#endif

              Task.Run(
                () =>
                {
                  var messageId = this.currentClassChangeToastTranslationId;

#if DEBUG
                  PluginLog.LogVerbose("Not Using Imgui - Translate ClassChange toast - 3");
#endif

                  textNode->SetText(this.currentClassChangeToastTranslation);
                  this.classChangeToastTranslationSemaphore.Wait();
                  if (messageId == this.currentClassChangeToastTranslationId)
                  {
                    var messageTranslation = Translate(messageToTranslate);
#if DEBUG
                    PluginLog.LogVerbose("Not Using Imgui - Translate ClassChange toast - 4");
#endif
                    textNode->SetText(messageTranslation);
                  }

                  textNode->SetText(Resources.WaitingForTranslation);
#if DEBUG
                  PluginLog.LogVerbose("Not Using Imgui - Translate ClassChange toast - 5");
#endif
                  this.classChangeToastTranslationSemaphore.Release();
                });
            }
            else
            {
#if DEBUG
              PluginLog.LogVerbose("Using Imgui - Translate ClassChange toast");
#endif
              this.classChangeToastDisplayTranslation = true;
              this.currentClassChangeToastTranslationId = Environment.TickCount;
              this.currentClassChangeToastTranslation = Resources.WaitingForTranslation;
              Task.Run(
                () =>
                {
                  var messageId = this.currentToastTranslationId;
                  var messageTranslation = Translate(textNode->NodeText.ToString());
                  this.classChangeToastTranslationSemaphore.Wait();
                  if (messageId == this.currentClassChangeToastTranslationId)
                  {
                    this.currentClassChangeToastTranslation = messageTranslation;
                  }

                  this.classChangeToastTranslationSemaphore.Release();
                });

              this.classChangeToastTranslationTextDimensions.X =
                classChangeToastByNameMaster->RootNode->Width * classChangeToastByNameMaster->Scale * 2;
              this.classChangeToastTranslationTextDimensions.Y =
                classChangeToastByNameMaster->RootNode->Height * classChangeToastByNameMaster->Scale;
              this.classChangeToastTranslationTextPosition.X = classChangeToastByNameMaster->RootNode->X;
              this.classChangeToastTranslationTextPosition.Y = classChangeToastByNameMaster->RootNode->Y;
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
          this.classChangeToastDisplayTranslation = false;
        }
      }
      else
      {
        this.classChangeToastDisplayTranslation = false;
      }
    }

    private unsafe void TextErrorToastHandler(string toastName, int index)
    {
      if (!this.configuration.TranslateErrorToast)
      {
        return;
      }

      var errorToastByName = GameGui.GetAddonByName(toastName, index);

      if (errorToastByName != IntPtr.Zero)
      {
        var errorToastByNameMaster = (AtkUnitBase*)errorToastByName;

        // 2729DE6EDE0
        if (errorToastByNameMaster->IsVisible)
        {
          this.errorToastDisplayTranslation = true;

          // TODO: convert all to this approach + async
          /*var errorToastId = errorToastByNameMaster->RootNode->ChildNode->NodeID;
          PluginLog.LogVerbose($"error toast id: {errorToastId}");
          var textNode = (AtkTextNode*)errorToastByNameMaster->UldManager.SearchNodeById(errorToastId);
          //var nodeText = MemoryHelper.ReadString((IntPtr)textNode->NodeText.StringPtr, (int)textNode->NodeText.StringLength);
          PluginLog.LogVerbose(textNode->NodeText.ToString() ?? "sem nada...");
          textNode->SetText("What is a man? A miserable little pile of secrets. But enough talk… Have at you!");*/

          this.errorToastTranslationTextDimensions.X =
            errorToastByNameMaster->RootNode->Width * errorToastByNameMaster->Scale;
          this.errorToastTranslationTextDimensions.Y =
            errorToastByNameMaster->RootNode->Height * errorToastByNameMaster->Scale;
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
      var areaToastByName = GameGui.GetAddonByName(areaToastName, index);
      if (areaToastByName != IntPtr.Zero)
      {
        var areaToastByNameMaster = (AtkUnitBase*)areaToastByName;
        if (areaToastByNameMaster->IsVisible)
        {
          this.areaToastDisplayTranslation = true;
          this.areaToastTranslationTextDimensions.X =
            areaToastByNameMaster->RootNode->Width * areaToastByNameMaster->Scale * 2;
          this.areaToastTranslationTextDimensions.Y =
            areaToastByNameMaster->RootNode->Height * areaToastByNameMaster->Scale;
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
      var wideTextToastByName = GameGui.GetAddonByName(wideTextToastName, index);
      if (wideTextToastByName != IntPtr.Zero)
      {
        var wideTextToastByNameMaster = (AtkUnitBase*)wideTextToastByName;
        if (wideTextToastByNameMaster->IsVisible)
        {
          this.wideTextToastDisplayTranslation = true;
          this.wideTextToastTranslationTextDimensions.X = wideTextToastByNameMaster->RootNode->Width *
                                                          wideTextToastByNameMaster->Scale * 2;
          this.wideTextToastTranslationTextDimensions.Y = wideTextToastByNameMaster->RootNode->Height *
                                                          wideTextToastByNameMaster->Scale;
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
      var addonByName = GameGui.GetAddonByName(addonName, index);
      if (addonByName != IntPtr.Zero)
      {
        var addonByNameMaster = (AtkUnitBase*)addonByName;
        if (addonByNameMaster->IsVisible)
        {
          this.addonDisplayTranslation = true;
          this.addonTranslationTextDimensions.X =
            addonByNameMaster->RootNode->Width * addonByNameMaster->Scale * 2;
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
      if (!this.configuration.TranslateToast || !this.configuration.TranslateQuestToast ||
          !this.configuration.TranslateWideTextToast)
      {
        return;
      }

      try
      {
        var messageTextToTranslate = message.TextValue;

        if (!this.configuration.UseImGuiForToasts && this.configuration.TranslateQuestToast &&
            this.configuration.TranslateWideTextToast)
        {
          var messageTranslatedText = Translate(messageTextToTranslate);

          message = messageTranslatedText;
        }
        else
        {
          this.currentQuestToastTranslationId = Environment.TickCount;
          this.currentQuestToastTranslation = Resources.WaitingForTranslation;
          Task.Run(
            () =>
            {
              var messageId = this.currentQuestToastTranslationId;
              var messageTranslation = Translate(messageTextToTranslate);
              this.questToastTranslationSemaphore.Wait();
              if (messageId == this.currentQuestToastTranslationId)
              {
                this.currentQuestToastTranslation = messageTranslation;
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
      using StreamWriter logStream = new(this.configDir + "GetToastLog.txt", true);
#endif

      var messageTextToTranslate = message.TextValue;
      var errorToastToHandle = this.FormatToastMessage("Error", message.TextValue);

#if DEBUG
      PluginLog.LogVerbose($"Before DB Query attempt: {errorToastToHandle}");
#endif

      var findings = this.FindErrorToastMessage(errorToastToHandle);

#if DEBUG
      PluginLog.LogVerbose(
        $"After DB Query attempt: {(findings ? "ErrorToastMessage found in Db." : "ErrorToastMessage not found in Db")}");
#endif

      // if the toast isn't saved
      if (!findings)
      {
        try
        {
          if (!this.configuration.UseImGuiForToasts)
          {
#if DEBUG
            PluginLog.LogVerbose("if not found and if not using imgui");
#endif
            var translatedToastMessage = Translate(message.TextValue);
            message = translatedToastMessage;

            var translatedToastData = new ToastMessage(
              "Error",
              messageTextToTranslate,
              LangIdentify(messageTextToTranslate),
              translatedToastMessage,
              this.LanguagesDictionary[this.configuration.Lang].Code,
              this.configuration.ChosenTransEngine,
              DateTime.Now,
              DateTime.Now);
#if DEBUG
            logStream.WriteLineAsync($"Before Toast Messages table data insertion:  {translatedToastData}");
#endif
            var result = this.InsertErrorToastMessageData(translatedToastData);
#if DEBUG
            PluginLog.LogVerbose($"Toast Message DB Insert operation result: {result}");
#endif
          }
          else
          {
#if DEBUG
            PluginLog.LogVerbose("if not found and if using imgui");
#endif
            this.currentErrorToastTranslationId = Environment.TickCount;
            this.currentErrorToastTranslation = Resources.WaitingForTranslation;
            Task.Run(
              () =>
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
                PluginLog.LogVerbose($"Before if ErrorToast translation: {this.currentErrorToastTranslation}");
#endif
                if (this.currentErrorToastTranslation != Resources.WaitingForTranslation)
                {
                  var translatedErrorToastData = new ToastMessage(
                    "Error",
                    messageTextToTranslate,
                    errorToastToHandle.OriginalLang,
                    this.currentErrorToastTranslation,
                    this.LanguagesDictionary[this.configuration.Lang].Code,
                    this.configuration.ChosenTransEngine,
                    DateTime.Now,
                    DateTime.Now);
                  var result = this.InsertErrorToastMessageData(translatedErrorToastData);
#if DEBUG
                  PluginLog.LogVerbose($"ToastMessage DB Insert operation result: {result}");
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
        if (!this.configuration.UseImGuiForToasts)
        {
#if DEBUG
          PluginLog.LogVerbose("if found and if not using imgui");
#endif
          message = this.FoundToastMessage.TranslatedToastMessage;
#if DEBUG
          PluginLog.Error($"Text replacement - message found in DB: {message.TextValue} ");
#endif
        }
        else
        {
#if DEBUG
          PluginLog.LogVerbose("if found and if using imgui");
#endif
          this.currentErrorToastTranslationId = Environment.TickCount;
          this.currentErrorToastTranslation = Resources.WaitingForTranslation;
          Task.Run(
            () =>
            {
#if DEBUG
              PluginLog.LogVerbose("Using Toast Overlay - inside Draw task");
#endif
              var messageId = this.currentErrorToastTranslationId;
              var messageTranslation = this.FoundToastMessage.TranslatedToastMessage;
#if DEBUG
              PluginLog.LogVerbose(
                $"Using Toast Overlay - inside Draw Error toast Overlay - found toast message: {messageTranslation}");
#endif
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
      if (!this.configuration.TranslateToast)
      {
        return;
      }
#if DEBUG
      using StreamWriter logStream = new(this.configDir + "GetNonErrorToastLog.txt", true);
#endif

      var messageTextToTranslate = message.TextValue;
      var toastToHandle = this.FormatToastMessage("NonError", message.TextValue);

#if DEBUG
      PluginLog.LogVerbose($"Before DB Query attempt: {toastToHandle}");
#endif

      var findings = this.FindToastMessage(toastToHandle);

#if DEBUG
      PluginLog.LogVerbose(
        $"After DB Query attempt: {(findings ? "toastMessage found in Db." : "toastMessage not found in Db")}");
#endif

      // if the toast isn't saved
      if (!findings)
      {
        try
        {
          if (!this.configuration.UseImGuiForToasts)
          {
#if DEBUG
            PluginLog.LogVerbose("if not found and if not using imgui");
#endif
            var translatedToastMessage = Translate(message.TextValue);
            message = translatedToastMessage;

            var translatedToastData = new ToastMessage(
              "NonError",
              messageTextToTranslate,
              LangIdentify(messageTextToTranslate),
              translatedToastMessage,
              this.LanguagesDictionary[this.configuration.Lang].Code,
              this.configuration.ChosenTransEngine,
              DateTime.Now,
              DateTime.Now);
#if DEBUG
            logStream.WriteLineAsync($"Before Toast Messages table data insertion:  {translatedToastData}");
#endif
            var result = this.InsertOtherToastMessageData(translatedToastData);
#if DEBUG
            PluginLog.LogVerbose($"Toast Message DB Insert operation result: {result}");
#endif
          }
          else
          {
#if DEBUG
            PluginLog.LogVerbose("if not found and if using imgui");
#endif
            this.currentToastTranslationId = Environment.TickCount;
            this.currentToastTranslation = Resources.WaitingForTranslation;
            Task.Run(
              () =>
              {
                var messageId = this.currentToastTranslationId;
                var messageTranslation = Translate(messageTextToTranslate);
                this.toastTranslationSemaphore.Wait();
                if (messageId == this.currentToastTranslationId)
                {
                  this.currentToastTranslation = messageTranslation;
                }

                this.toastTranslationSemaphore.Release();
#if DEBUG
                PluginLog.LogVerbose($"Before if toast translation: {this.currentToastTranslation}");
#endif
                if (this.currentToastTranslation != Resources.WaitingForTranslation)
                {
                  var translatedToastData = new ToastMessage(
                    "NonError",
                    messageTextToTranslate,
                    toastToHandle.OriginalLang,
                    this.currentToastTranslation,
                    this.LanguagesDictionary[this.configuration.Lang].Code,
                    this.configuration.ChosenTransEngine,
                    DateTime.Now,
                    DateTime.Now);
                  var result = this.InsertOtherToastMessageData(translatedToastData);
#if DEBUG
                  PluginLog.LogVerbose($"ToastMessage DB Insert operation result: {result}");
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
        if (!this.configuration.UseImGuiForToasts)
        {
#if DEBUG
          PluginLog.LogVerbose("if found and if not using imgui");
#endif
          message = this.FoundToastMessage.TranslatedToastMessage;
#if DEBUG
          PluginLog.Error($"Text replacement - message found in DB: {message.TextValue} ");
#endif
        }
        else
        {
#if DEBUG
          PluginLog.LogVerbose("if found and if using imgui");
#endif
          this.currentToastTranslationId = Environment.TickCount;
          this.currentToastTranslation = Resources.WaitingForTranslation;
          Task.Run(
            () =>
            {
#if DEBUG
              PluginLog.LogVerbose("Using Toast Overlay - inside Draw task");
#endif
              var messageId = this.currentToastTranslationId;
              var messageTranslation = this.FoundToastMessage.TranslatedToastMessage;
#if DEBUG
              PluginLog.LogVerbose(
                $"Using Toast Overlay - inside Draw toast Overlay - found toast message: {messageTranslation}");
#endif
              this.toastTranslationSemaphore.Wait();
              if (messageId == this.currentToastTranslationId)
              {
                this.currentToastTranslation = messageTranslation;
#if DEBUG
                PluginLog.Error($"Using overlay - message found in DB: {messageTranslation} ");
#endif
              }

              this.toastTranslationSemaphore.Release();
            });
        }
      }
    }
  }
}