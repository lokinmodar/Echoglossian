// <copyright file="UiTalkHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Dalamud.Game.Addon.Events;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;

using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Echoglossian
{
  public partial class Echoglossian
  {


    private Dictionary<string, string> translations = new Dictionary<string, string>();

    public void EgloAddonHandler()
    {
      AddonLifecycle.RegisterListener(AddonEvent.PreSetup, "TalkSubtitle", this.UpdateUI);
      AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "TalkSubtitle", this.UpdateUI);
      /* AddonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, "TalkSubtitle", this.UpdateUI);*/
    }

    private unsafe void UpdateUI(AddonEvent type, AddonArgs args)
    {
      switch (args)
      {
        case AddonSetupArgs setupArgs:
          var setupAtkValues = (AtkValue*)setupArgs.AtkValues;
          var addonInfo = (AtkUnitBase*)setupArgs.Addon;
          PluginLog.Information($"Addon Info: {addonInfo->ToString}");
          var addonDetails = addonInfo->GetTextNodeById(2);
          PluginLog.Information($"Addon Details----------------: {addonDetails->NodeText} -> {addonDetails->NodeText.BufUsed}");

          var originalText = Marshal.PtrToStringUTF8(new IntPtr(setupAtkValues[0].String));
          var translatedText = Translate(originalText);
          this.translations[originalText] = translatedText;
          PluginLog.Information($"AddonSetup-----------: {originalText} -> {translatedText}");

          var currentText = Marshal.PtrToStringUTF8(new IntPtr(setupAtkValues[0].String));
          PluginLog.Information($"AddonSetup current text============: {currentText}");

          if (this.translations.TryGetValue(currentText, out var storedTranslation) && !string.IsNullOrEmpty(storedTranslation))
          {
            PluginLog.Information($"Setting new translation: {storedTranslation}");
            /*var tsWindow = new SimpleWindow("TsWindow", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoDecoration, false, storedTranslation, true, 
            */
            setupAtkValues[0].SetString(storedTranslation);
          }

          break;

          /*case AddonRefreshArgs refreshArgs:
            try
            {
              var refreshAtkValues = (AtkValue*)refreshArgs.AtkValues;
              var currentText = Marshal.PtrToStringUTF8(new IntPtr(refreshAtkValues[0].String));
              PluginLog.Information($"AddonRefresh: {currentText}");

              if (this.translations.TryGetValue(currentText, out var storedTranslation) && !string.IsNullOrEmpty(storedTranslation))
              {
                PluginLog.Information($"Setting new translation: {storedTranslation}");
                refreshAtkValues[0].SetString(storedTranslation);
              }
              else
              {
                refreshAtkValues[0].SetString("cu");
                PluginLog.Warning($"No translation found for: {currentText}");
              }
            }
            catch (Exception e)
            {
              PluginLog.Error($"The routine has crashed: {e}");
            }

            break;

          case AddonRequestedUpdateArgs refreshArgs:
            try
            {
              IntPtr talkSubtitlePtr = GameGui.GetAddonByName(refreshArgs.AddonName);
              if (talkSubtitlePtr == IntPtr.Zero)
              {
                return;
              }

              AtkUnitBase* talkSubtitleMaster = (AtkUnitBase*)talkSubtitlePtr;
              if (!talkSubtitleMaster->IsVisible)
              {
                return;
              }

              var tNode = (AtkTextNode*)talkSubtitleMaster->RootNode->ChildNode;

              if (tNode == null)
              {
                PluginLog.Warning("Node Empty");
              }
              else
              {
                if (this.translations.TryGetValue(tNode->NodeText.ToString(), out var storedTranslation) && !string.IsNullOrEmpty(storedTranslation))
                {
                  PluginLog.Information($"Setting new translation: {storedTranslation}");
                  tNode->SetText(storedTranslation);
                }
              }
            }
            catch (Exception e)
            {
              PluginLog.Error($"The routine has crashed: {e}");
            }

            break;*/
      }
    }

    /*PluginLog.Verbose($"AddonEvent: {type}");
var addon = (AtkUnitBase*)args.Addon;

// PluginLog.Verbose($"Addon: {addon->ToString}");

var targetNode = addon->GetNodeById(2);

// PluginLog.Verbose($"targetNode: {targetNode->ToString}");

targetNode->NodeFlags |= NodeFlags.EmitsEvents | NodeFlags.Enabled;

this.TalkSubtitleHandler((IntPtr)addon, (IntPtr)targetNode);

*//*EventManager.AddEvent((nint)addon, (nint)targetNode, AddonEventType., this.TalkSubtitleHandler);*/

    /*private unsafe void TalkSubtitleHandler(IntPtr addon, IntPtr node)
    {

      try
      {
        // PluginLog.Verbose(messageTemplate: $"Addon in TS: {addon}");
        var addonInfo = (AtkUnitBase*)addon;

        // PluginLog.Verbose($"AddonInfo: {addonInfo->ToString}");

        var nodeText = ((AtkTextNode*)node)->NodeText;

        var text = nodeText.StringPtr == null || nodeText.BufUsed == 0
          ? string.Empty
          : Marshal.PtrToStringUTF8(new IntPtr(nodeText.StringPtr));

        if (text == string.Empty)
        {
          PluginLog.Verbose("Text Empty");
          return;
        }

        // Check if the addon IntPtr has been processed
        if (this.processedAddons.Contains(addon))
        {
          // PluginLog.Verbose("Addon already processed.");
          return;
        }

        // Add the addon IntPtr to the HashSet

        PluginLog.Verbose($"talkSubtitleText: {text}");

        var translatedText = Translate(text);

        PluginLog.Verbose($"translatedText: {translatedText}");

        this.processedAddons.Add(addon);
      }
      catch (Exception e)
      {
        PluginLog.Error($"The TalkSubtitle routine has crashed: {e}");
      }*/

    /*// Pointer: 23FDB6C9040 or 23FDB6C91C0
    string originalText;
    try
    {
      IntPtr talkSubtitlePtr = GameGui.GetAddonByName(addonName, index);
      if (talkSubtitlePtr == IntPtr.Zero)
      {
        return;
      }

      AtkUnitBase* talkSubtitleMaster = (AtkUnitBase*)talkSubtitlePtr;
      if (!talkSubtitleMaster->IsVisible)
      {
        return;
      }

      var tNode = (AtkTextNode*)talkSubtitleMaster->RootNode->ChildNode;

      if (tNode == null)
      {
        PluginLog.Warning("Node Empty");
      }
      else
      {
        var text = tNode->NodeText;

        originalText = text.StringPtr == null || text.BufUsed == 0
          ? string.Empty
          : Marshal.PtrToStringUTF8(new IntPtr(text.StringPtr));
#if DEBUG
        PluginLog.Verbose($"talkSubtitleText: {originalText}");
#endif
      }
  }
    catch (Exception e)
    {
      PluginLog.Error($"The routine has crashed: {e}");
    }
  }*/
  }
}
