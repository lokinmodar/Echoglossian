// <copyright file="UiTalkHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Echoglossian
{
  /*  public partial class Echoglossian
    {
      private readonly int delayBetweenVisibilityCheckForOverlay = 100;

      private unsafe void TalkHandler(string addonName, int index)
      {
        IntPtr talk = GameGuiInterface.GetAddonByName(addonName, index);
        if (talk != IntPtr.Zero)
        {
          AtkUnitBase* talkMaster = (AtkUnitBase*)talk;
          while (talkMaster->IsVisible)
          {
            this.talkDisplayTranslation = true;
            this.talkTextDimensions.X = talkMaster->RootNode->Width * talkMaster->Scale;
            this.talkTextDimensions.Y = talkMaster->RootNode->Height * talkMaster->Scale;
            this.talkTextPosition.X = talkMaster->RootNode->X;
            this.talkTextPosition.Y = talkMaster->RootNode->Y;

            Thread.Sleep(this.delayBetweenVisibilityCheckForOverlay);
  #if DEBUG
            // PluginLog.Debug("Inside Talk Handler.");
  #endif
          }

          this.talkDisplayTranslation = false;
        }
      }
    }*/
}