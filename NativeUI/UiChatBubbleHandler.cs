// <copyright file="UiChatBubbleHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private void ChatBubblesOnChatBubble(ref GameObject gameObject, ref SeString text)
    {
      PluginLog.Debug($"Chat Bubble text: {text.TextValue}");
    }
  }
}
