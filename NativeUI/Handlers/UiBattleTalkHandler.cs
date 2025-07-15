// <copyright file="UiTalkHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

public partial class Echoglossian
{
    private unsafe void BattleTalkHandler(string addonName, int index)
    {
        var battleTalk = GameGuiInterface.GetAddonByName(addonName, index);
        if (battleTalk != IntPtr.Zero)
        {
            var battleTalkMaster = (AtkUnitBase*)battleTalk;
            while (battleTalkMaster->IsVisible)
            {
                this.battleTalkDisplayTranslation = true;
                this.battleTalkTextDimensions.X =
                    battleTalkMaster->RootNode->Width * battleTalkMaster->Scale;
                this.battleTalkTextDimensions.Y =
                    battleTalkMaster->RootNode->Height *
                    battleTalkMaster->Scale;
                this.battleTalkTextPosition.X = battleTalkMaster->RootNode->X;
                this.battleTalkTextPosition.Y = battleTalkMaster->RootNode->Y;

                Thread.Sleep(this.delayBetweenVisibilityCheckForOverlay);
            }

            this.battleTalkDisplayTranslation = false;
        }

        this.battleTalkDisplayTranslation = false;
    }
}