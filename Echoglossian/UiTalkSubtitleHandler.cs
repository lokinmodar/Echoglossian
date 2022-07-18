// <copyright file="UiTalkSubtitleHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Runtime.InteropServices;

using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private unsafe void TalkSubtitleHandler(string addonName, int index)
    {
      if (!this.configuration.TranslateTalkSubtitle)
      {
        return;
      }

      // Pointer: 23FDB6C9040 or 23FDB6C91C0
      string originalText;
      try
      {
        var talkSubtitlePtr = GameGui.GetAddonByName(addonName, index);
        if (talkSubtitlePtr == IntPtr.Zero)
        {
          return;
        }

        var talkSubtitleMaster = (AtkUnitBase*)talkSubtitlePtr;
        if (!talkSubtitleMaster->IsVisible)
        {
          return;
        }

        var tNode = (AtkTextNode*)talkSubtitleMaster->RootNode->ChildNode;

        if (tNode == null)
        {
          PluginLog.LogVerbose("Node Empty");
          return;
        }

        var text = tNode->NodeText;

        originalText = text.StringPtr == null || text.BufUsed == 0
          ? string.Empty
          : Marshal.PtrToStringUTF8(new IntPtr(text.StringPtr));
#if DEBUG
        PluginLog.LogVerbose($"talkSubtitleText: {originalText}");
#endif
      }
      catch (Exception e)
      {
        PluginLog.Error($"The routine has crashed: {e}");
      }
    }
  }
}