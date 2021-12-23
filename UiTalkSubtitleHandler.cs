// <copyright file="UiTalkHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Logging;
using Dalamud.Utility;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Properties;
using FFXIVClientStructs.FFXIV.Component.GUI;
using XivCommon.Functions;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private unsafe void TalkSubtitleHandler(string addonName, int index)
    {
      // Pointer: 23FDB6C9040 or 23FDB6C91C0
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
          PluginLog.LogWarning("Node Empty");
        }
        else
        {
          var text = tNode->NodeText;

          originalText = text.StringPtr == null || text.BufUsed == 0
            ? string.Empty
            : Marshal.PtrToStringUTF8(new IntPtr(text.StringPtr));
#if DEBUG
          PluginLog.LogVerbose($"talkSubtitleText: {originalText}");
#endif
        }
      }
      catch (Exception e)
      {
        PluginLog.Error($"The routine has crashed: {e}");
      }
    }
  }
}
