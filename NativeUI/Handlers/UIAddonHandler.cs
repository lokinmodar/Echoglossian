// <copyright file="UIAddonHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Concurrent;

using Dalamud.Game;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Memory;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Translators;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Humanizer;
using ImGuiNET;

using static Echoglossian.Echoglossian;

using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Echoglossian.NativeUI.Handlers
{
  internal class UiAddonHandler : IDisposable
  {
    private bool disposedValue;
    private CancellationTokenSource cts;
    private Task translationTask;

    private Config configuration;
    private ImFontPtr uiFont;
    private bool fontLoaded;
    private ClientLanguage clientLanguage;
    private TranslationService translationService;
    private ConcurrentDictionary<int, TranslationEntry> translations;
    private string langToTranslateTo;
    private string addonName = string.Empty;
    private bool isAddonVisible = false;
    private Dictionary<int, TextFlags> addonNodesFlags;
    private AddonCharacteristicsInfo addonCharacteristicsInfo;
    private string configDir;
    private HashSet<string> translatedTexts = new HashSet<string>();
    private const string TranslationMarker = "\u0020\u0020\u0020\u0020\u0020"; // 5 spaces
    private static readonly Dictionary<string, bool> ProcessedAddons = new Dictionary<string, bool>();

    private AddonReceiveEventArgs addonReceiveEventArgs = null;
    private AddonSetupArgs addonSetupArgs = null;
    private AddonUpdateArgs addonUpdateArgs = null;
    private AddonDrawArgs addonDrawArgs = null;
    private AddonFinalizeArgs addonFinalizeArgs = null;
    private AddonRequestedUpdateArgs addonRequestedUpdateArgs = null;
    private AddonRefreshArgs addonRefreshArgs = null;

    public UiAddonHandler(
        Config configuration = default,
        ImFontPtr uiFont = default,
        bool fontLoaded = default,
        string langToTranslateTo = default)
    {
      this.configuration = configuration;
      this.uiFont = uiFont;
      this.fontLoaded = fontLoaded;
      this.langToTranslateTo = langToTranslateTo;
      clientLanguage = ClientStateInterface.ClientLanguage;
      translationService = new TranslationService(configuration, PluginLog, new Sanitizer(clientLanguage));
      translations = new ConcurrentDictionary<int, TranslationEntry>();
      configDir = PluginInterface.GetPluginConfigDirectory() + Path.DirectorySeparatorChar;
      cts = new CancellationTokenSource();
      translationTask = Task.Run(async () => await ProcessTranslations(cts.Token));
    }

#nullable enable
    public void EgloAddonHandler(string addonName, AddonSetupArgs? setupArgs = null)
    {
      this.addonName = addonName;
      if (setupArgs != null)
      {
        addonSetupArgs = setupArgs;
      }

      HandleCommonLogic();
    }

    public void EgloAddonHandler(string addonName, AddonReceiveEventArgs? receiveEventArgs = null)
    {
      this.addonName = addonName;
      if (receiveEventArgs != null)
      {
        addonReceiveEventArgs = receiveEventArgs;
      }

      HandleCommonLogic();
    }

    public void EgloAddonHandler(string addonName, AddonUpdateArgs? updateArgs = null)
    {
      this.addonName = addonName;
      if (updateArgs != null)
      {
        addonUpdateArgs = updateArgs;
      }

      HandleCommonLogic();
    }

    public void EgloAddonHandler(string addonName, AddonDrawArgs? drawArgs = null)
    {
      this.addonName = addonName;
      if (drawArgs != null)
      {
        addonDrawArgs = drawArgs;
      }

      HandleCommonLogic();
    }

    public void EgloAddonHandler(string addonName, AddonFinalizeArgs? finalizeArgs = null)
    {
      this.addonName = addonName;
      if (finalizeArgs != null)
      {
        addonFinalizeArgs = finalizeArgs;
      }

      HandleCommonLogic();
    }

    public void EgloAddonHandler(string addonName, AddonRequestedUpdateArgs? requestedUpdateArgs = null)
    {
      this.addonName = addonName;
      if (requestedUpdateArgs != null)
      {
        addonRequestedUpdateArgs = requestedUpdateArgs;
      }

      HandleCommonLogic();
    }

    public void EgloAddonHandler(string addonName, AddonRefreshArgs? refreshArgs = null)
    {
      this.addonName = addonName;
      if (refreshArgs != null)
      {
        addonRefreshArgs = refreshArgs;
      }

      HandleCommonLogic();
    }

    private void HandleCommonLogic()
    {
      if (string.IsNullOrEmpty(addonName))
      {
        return;
      }

      DetermineAddonCharacteristics();
      // this.AdjustAddonNodesFlags();
      ExploreAddon();
    }

    private void DetermineAddonCharacteristics()
    {
      switch (addonName)
      {
        case "Talk":
          addonCharacteristicsInfo = new()
          {
            AddonName = addonName,
            IsComplexAddon = false,
            NameNodeId = 2,
            MessageNodeId = 3,
            AtkValuesNameStringIndex = 1,
            AtkValuesMessageStringIndex = 0,
            TalkMessage = new TalkMessage(
                  senderName: string.Empty,
                  originalTalkMessage: string.Empty,
                  originalSenderNameLang: clientLanguage.Humanize(),
                  translatedTalkMessage: string.Empty,
                  originalTalkMessageLang: clientLanguage.Humanize(),
                  translationLang: langToTranslateTo,
                  translationEngine: configuration.ChosenTransEngine,
                  translatedSenderName: string.Empty,
                  createdDate: DateTime.Now,
                  updatedDate: DateTime.Now),
          };
          break;
        case "_BattleTalk":
          addonCharacteristicsInfo = new()
          {
            AddonName = addonName,
            IsComplexAddon = false,
            NameNodeId = 4,
            MessageNodeId = 6,
            BattleTalkMessage = new BattleTalkMessage(
                  senderName: string.Empty,
                  originalBattleTalkMessage: string.Empty,
                  originalSenderNameLang: clientLanguage.Humanize(),
                  translatedBattleTalkMessage: string.Empty,
                  originalBattleTalkMessageLang: clientLanguage.Humanize(),
                  translationLang: langToTranslateTo,
                  translationEngine: configuration.ChosenTransEngine,
                  translatedSenderName: string.Empty,
                  createdDate: DateTime.Now,
                  updatedDate: DateTime.Now),
          };
          break;
        case "TalkSubtitle":
          addonCharacteristicsInfo = new()
          {
            AddonName = addonName,
            IsComplexAddon = false,
            AtkValuesMessageStringIndex = 0,
            TalkSubtitleMessage = new TalkSubtitleMessage(
                  originalTalkSubtitleMessage: string.Empty,
                  translatedTalkSubtitleMessage: string.Empty,
                  originalTalkSubtitleMessageLang: clientLanguage.Humanize(),
                  translationLang: langToTranslateTo,
                  translationEngine: configuration.ChosenTransEngine,
                  createdDate: DateTime.Now,
                  updatedDate: DateTime.Now),
          };
          break;
        default:
          break;
      }
    }

    private void AdjustAddonNodesFlags()
    {
      addonNodesFlags = new Dictionary<int, TextFlags>();

      switch (addonName)
      {
        case "Talk":
          addonNodesFlags.Add(3, (TextFlags)((byte)TextFlags.WordWrap | (byte)TextFlags.MultiLine));
          break;
        case "_BattleTalk":
          addonNodesFlags.Add(6, (TextFlags)((byte)TextFlags.WordWrap | (byte)TextFlags.MultiLine));
          break;
        case "TalkSubtitle":
          addonNodesFlags.Add(3, (TextFlags)((byte)TextFlags.WordWrap | (byte)TextFlags.MultiLine));
          break;
        default:
          break;
      }
    }

    private unsafe void ExploreAddon()
    {
      AtkUnitBase* foundAddon = null;

      try
      {
        var addon = GameGuiInterface.GetAddonByName(addonName, 1);
        foundAddon = (AtkUnitBase*)addon;
        if (foundAddon == null)
        {
          PluginLog.Debug($"Addon {addonName} not found in ExploreAddon.");
          return;
        }
      }
      catch (Exception ex)
      {
        PluginLog.Error($"Error retrieving addon: {ex}");
        return;
      }

      try
      {
        isAddonVisible = foundAddon->IsVisible;
        if (!isAddonVisible)
        {
          return;
        }
      }
      catch (Exception ex)
      {
        PluginLog.Error($"Error checking addon visibility: {ex}");
        return;
      }

      AtkTextNode* nameNodeAsTextNode = null;
      AtkTextNode* messageNodeAsTextNode = null;

      try
      {
        var nameNode = foundAddon->GetNodeById((uint)addonCharacteristicsInfo.NameNodeId);

        if (nameNode == null)
        {
          return;
        }

        nameNodeAsTextNode = nameNode->GetAsAtkTextNode();

        if (nameNodeAsTextNode == null)
        {
          return;
        }

        PluginLog.Debug($"Addon {addonName} name node found in ExploreAddon.");
        PluginLog.Debug($"Addon {addonName} name node text in ExploreAddon: {MemoryHelper.ReadSeStringAsString(out _, (nint)nameNodeAsTextNode->NodeText.StringPtr.Value)}");
      }
      catch (Exception ex)
      {
        PluginLog.Error($"Error retrieving name node: {ex}");
      }

      try
      {
        var messageNode = foundAddon->GetNodeById((uint)addonCharacteristicsInfo.MessageNodeId);

        if (messageNode == null)
        {
          return;
        }

        messageNodeAsTextNode = messageNode->GetAsAtkTextNode();

        if (messageNodeAsTextNode == null)
        {
          return;
        }

        PluginLog.Debug($"Addon {addonName} message node found in ExploreAddon.");
        PluginLog.Debug($"Addon {addonName} message node text in ExploreAddon: {MemoryHelper.ReadSeStringAsString(out _, (nint)messageNodeAsTextNode->NodeText.StringPtr.Value)}");
      }
      catch (Exception ex)
      {
        PluginLog.Error($"Error retrieving message node: {ex}");
      }

      if (nameNodeAsTextNode != null)
      {
        var nameText = CleanString(MemoryHelper.ReadSeStringAsString(out _, (nint)nameNodeAsTextNode->NodeText.StringPtr.Value));

        PluginLog.Debug($"Addon {addonName} name node text in ExploreAddon: {nameText}");

        if (string.IsNullOrEmpty(nameText) || nameText.Contains(TranslationMarker))
        {
          PluginLog.Debug($"Addon {addonName} name node has already been processed.");
          return;
        }

        if (addonName == "Talk")
        {
          addonCharacteristicsInfo.TalkMessage.SenderName = nameText;
        }

        if (addonName == "_BattleTalk")
        {
          addonCharacteristicsInfo.BattleTalkMessage.SenderName = nameText;
        }

        if (!configuration.TranslateNpcNames)
        {
          if (addonName == "Talk")
          {
            addonCharacteristicsInfo.TalkMessage.TranslatedSenderName = nameText;
          }

          if (addonName == "_BattleTalk")
          {
            addonCharacteristicsInfo.BattleTalkMessage.TranslatedSenderName = nameText;
          }
        }
      }

      if (messageNodeAsTextNode != null)
      {
        var messageNodeText = MemoryHelper.ReadSeStringAsString(out _, (nint)messageNodeAsTextNode->NodeText.StringPtr.Value);

        PluginLog.Debug($"Addon {addonName} message node text in ExploreAddon: {messageNodeText}");

        var messageText = CleanString(messageNodeText);

        PluginLog.Debug($"Addon {addonName} clean message node text in ExploreAddon: {messageText}");

        if (!string.IsNullOrEmpty(messageText) && messageText.Contains(TranslationMarker))
        {
          PluginLog.Debug($"Addon {addonName} message node has already been processed.");
          return;
        }

        if (addonName == "Talk")
        {
          addonCharacteristicsInfo.TalkMessage.OriginalTalkMessage = messageText;
        }

        if (addonName == "_BattleTalk")
        {
          addonCharacteristicsInfo.BattleTalkMessage.OriginalBattleTalkMessage = messageText;
        }
      }

      CheckDatabaseForTranslation();
    }

    private void CheckDatabaseForTranslation()
    {
      if (addonName == "Talk")
      {
        var talkMessage = addonCharacteristicsInfo.TalkMessage;

        PluginLog.Debug($"Checking database for: {addonCharacteristicsInfo.TalkMessage}");

        if (talkMessage != null && !string.IsNullOrEmpty(talkMessage.SenderName) && !string.IsNullOrEmpty(talkMessage.OriginalTalkMessage))
        {
          if (FindTalkMessage(talkMessage))
          {
            addonCharacteristicsInfo.TalkMessage.TranslatedTalkMessage = FoundTalkMessage.TranslatedTalkMessage + TranslationMarker;

            PluginLog.Debug($"Addon {addonName} message node text found in database is {addonCharacteristicsInfo.TalkMessage.TranslatedTalkMessage}");
            if (configuration.TranslateNpcNames)
            {
              addonCharacteristicsInfo.TalkMessage.TranslatedSenderName = FoundTalkMessage.TranslatedSenderName + TranslationMarker;
              PluginLog.Debug($"Addon {addonName} sender name node text found in database is {addonCharacteristicsInfo.TalkMessage.TranslatedSenderName}");
            }
            else
            {
              addonCharacteristicsInfo.TalkMessage.TranslatedSenderName = FoundTalkMessage.SenderName + TranslationMarker;
            }

            //this.SetTranslationToAddon();
          }
          else
          {
            PluginLog.Debug($"Current addon {addonName} not found in database. Sending to translate!");
            TranslateTexts(talkMessage.OriginalTalkMessage, "Talk");
          }
        }
      }
      else if (addonName == "_BattleTalk")
      {
        var battleTalkMessage = addonCharacteristicsInfo.BattleTalkMessage;

        PluginLog.Debug($"Checking database for: {addonCharacteristicsInfo.BattleTalkMessage}");

        if (battleTalkMessage != null && !string.IsNullOrEmpty(battleTalkMessage.SenderName) && !string.IsNullOrEmpty(battleTalkMessage.OriginalBattleTalkMessage))
        {
          if (FindBattleTalkMessage(battleTalkMessage))
          {
            addonCharacteristicsInfo.BattleTalkMessage.TranslatedBattleTalkMessage = FoundBattleTalkMessage.TranslatedBattleTalkMessage + TranslationMarker;

            PluginLog.Debug($"Addon {addonName} message node text found in database is {addonCharacteristicsInfo.BattleTalkMessage.TranslatedBattleTalkMessage}");
            if (configuration.TranslateNpcNames)
            {
              addonCharacteristicsInfo.BattleTalkMessage.TranslatedSenderName = FoundBattleTalkMessage.TranslatedSenderName + TranslationMarker;
              PluginLog.Debug($"Addon {addonName} sender name node text found in database is {addonCharacteristicsInfo.BattleTalkMessage.TranslatedSenderName}");
            }
            else
            {
              addonCharacteristicsInfo.BattleTalkMessage.TranslatedSenderName = FoundBattleTalkMessage.SenderName + TranslationMarker;
            }

            //this.SetTranslationToAddon();
          }
          else
          {
            PluginLog.Debug($"Current addon {addonName} not found in database. Sending to translate!");
            TranslateTexts(battleTalkMessage.OriginalBattleTalkMessage, "_BattleTalk");
          }
        }
      }
    }

    private void TranslateTexts(string originalText, string addonType)
    {
      Task.Run(async () =>
      {
        var translation = await translationService.TranslateAsync(originalText, clientLanguage.Humanize(), langToTranslateTo);
        if (addonType == "Talk")
        {
          addonCharacteristicsInfo.TalkMessage.TranslatedTalkMessage = translation;
          if (configuration.TranslateNpcNames)
          {
            addonCharacteristicsInfo.TalkMessage.TranslatedSenderName = await translationService.TranslateAsync(addonCharacteristicsInfo.TalkMessage.SenderName, clientLanguage.Humanize(), langToTranslateTo);
          }

          SaveTranslationToDatabase(originalText, translation, addonType);
        }
        else if (addonType == "_BattleTalk")
        {
          addonCharacteristicsInfo.BattleTalkMessage.TranslatedBattleTalkMessage = translation;
          if (configuration.TranslateNpcNames)
          {
            addonCharacteristicsInfo.BattleTalkMessage.TranslatedSenderName = await translationService.TranslateAsync(addonCharacteristicsInfo.BattleTalkMessage.SenderName, clientLanguage.Humanize(), langToTranslateTo);
          }

          SaveTranslationToDatabase(originalText, translation, addonType);
        }

        translatedTexts.Add(translation);

        // this.SetTranslationToAddon();
      });
    }

    private void SaveTranslationToDatabase(string originalText, string translatedText, string addonType)
    {
      if (addonType == "Talk")
      {
        var talkMessage = addonCharacteristicsInfo.TalkMessage;

        if (talkMessage.TranslatedTalkMessage != translatedText)
        {
          talkMessage.OriginalTalkMessage = originalText;
          talkMessage.TranslatedTalkMessage = translatedText + TranslationMarker;
          if (!translatedTexts.Contains(talkMessage.TranslatedTalkMessage))
          {
            InsertTalkData(talkMessage);
            translatedTexts.Add(talkMessage.TranslatedTalkMessage);
          }
        }
      }
      else if (addonType == "_BattleTalk")
      {
        var battleTalkMessage = addonCharacteristicsInfo.BattleTalkMessage;

        if (battleTalkMessage.TranslatedBattleTalkMessage != translatedText)
        {
          battleTalkMessage.OriginalBattleTalkMessage = originalText;
          battleTalkMessage.TranslatedBattleTalkMessage = translatedText + TranslationMarker;
          if (!translatedTexts.Contains(battleTalkMessage.TranslatedBattleTalkMessage))
          {
            InsertBattleTalkData(battleTalkMessage);
            translatedTexts.Add(battleTalkMessage.TranslatedBattleTalkMessage);
          }
        }
      }
    }

    private async Task ProcessTranslations(CancellationToken token)
    {
      while (!token.IsCancellationRequested)
      {
        foreach (var key in translations.Keys)
        {
          if (translations.TryGetValue(key, out var entry) && !entry.IsTranslated && isAddonVisible)
          {
            await TranslateText(key, entry.OriginalText);
          }
        }

        try
        {
          await Task.Delay(100, token); // tried using Task.Yeld() but I saw no difference
        }
        catch (TaskCanceledException)
        {
          break;
        }
      }
    }

    private async Task TranslateText(int id, string text)
    {
      try
      {
        var translation = await translationService.TranslateAsync(text, clientLanguage.Humanize(), langToTranslateTo);
        if (translations.TryGetValue(id, out var entry))
        {
          entry.TranslatedText = translation;
          entry.IsTranslated = true;
          translatedTexts.Add(translation);

          await Task.Run(() => SaveTranslationToDatabase(text, translation, addonName));
        }
      }
      catch (Exception e)
      {
        PluginLog.Error($"Error in TranslateText method: {e}");
      }
    }

    public unsafe void SetTranslationToAddon()
    {
      PluginLog.Debug($"Called SetTranslationToAddon for addon {addonName}.");
      AtkUnitBase* foundAddon = null;

      try
      {
        var addon = GameGuiInterface.GetAddonByName(addonName, 1);

        PluginLog.Debug($"Addon {addonName} found in SetTranslationToAddon.");
        foundAddon = (AtkUnitBase*)addon;

        if (foundAddon == null)
        {
          return;
        }
      }
      catch (Exception ex)
      {
        PluginLog.Error($"Error retrieving addon: {ex}");
        return;
      }

      try
      {
        isAddonVisible = foundAddon->IsVisible;

        PluginLog.Debug($"Addon {addonName} is visible: {isAddonVisible} in SetTranslationToAddon.");
        if (!isAddonVisible)
        {
          PluginLog.Debug($"Addon {addonName} is not visible in SetTranslationToAddon.");
          return;
        }
      }
      catch (Exception ex)
      {
        PluginLog.Error($"Error checking addon visibility in SetTranslationToAddon: {ex}");
        return;
      }

      AtkTextNode* nameNodeAsTextNode = null;
      AtkTextNode* messageNodeAsTextNode = null;

      try
      {
        var nameNode = foundAddon->GetNodeById((uint)addonCharacteristicsInfo.NameNodeId);

        if (nameNode == null)
        {
          return;
        }

        nameNodeAsTextNode = nameNode->GetAsAtkTextNode();

        if (nameNodeAsTextNode == null)
        {
          return;
        }

        PluginLog.Debug($"Addon {addonName} name node found in SetTranslationToAddon.");
      }
      catch (Exception ex)
      {
        PluginLog.Error($"Error retrieving name node in SetTranslationToAddon: {ex}");
      }

      try
      {
        var messageNode = foundAddon->GetNodeById((uint)addonCharacteristicsInfo.MessageNodeId);

        if (messageNode == null)
        {
          return;
        }

        messageNodeAsTextNode = messageNode->GetAsAtkTextNode();

        if (messageNodeAsTextNode == null)
        {
          return;
        }

        PluginLog.Debug($"Addon {addonName} message node found in SetTranslationToAddon.");
      }
      catch (Exception ex)
      {
        PluginLog.Error($"Error retrieving message node in SetTranslationToAddon: {ex}");
      }

      // this.AdjustAddonNodesFlags();
      if (nameNodeAsTextNode != null)
      {
        var nameTextFromNode = CleanString(MemoryHelper.ReadSeStringAsString(out _, (nint)nameNodeAsTextNode->NodeText.StringPtr.Value));

        PluginLog.Debug($"Addon {addonName} name node text in SetTranslationToAddon: {nameTextFromNode}");
        try
        {
          var translatedName = string.Empty;

          if (addonName == "Talk")
          {
            translatedName = addonCharacteristicsInfo.TalkMessage.TranslatedSenderName + TranslationMarker;

            PluginLog.Debug($"Addon {addonName} translatedName node text in SetTranslationToAddon: {translatedName}");

            PluginLog.Debug($"Comparison to SetTranslationToAddon: 'this.configuration.TranslateNpcNames' is {configuration.TranslateNpcNames} and '!translatedName.Contains(TranslationMarker)' is {nameTextFromNode.Contains(TranslationMarker)} and the result is {configuration.TranslateNpcNames && nameTextFromNode.Contains(TranslationMarker)}");
            if (nameTextFromNode.Contains(TranslationMarker))
            {
              PluginLog.Debug($"Name node text in SetTranslationToAddon has already been processed.");
              return;
            }

            if (configuration.TranslateNpcNames)
            {
              PluginLog.Debug($"Setting name node text in SetTranslationToAddon.");
              nameNodeAsTextNode->SetText(translatedName);
              nameNodeAsTextNode->ResizeNodeForCurrentText();
            }
            else
            {
              PluginLog.Debug($"Setting name node text in SetTranslationToAddon.");
              nameNodeAsTextNode->SetText(addonCharacteristicsInfo.TalkMessage.SenderName + TranslationMarker);
              nameNodeAsTextNode->ResizeNodeForCurrentText();
            }
          }

          if (addonName == "_BattleTalk")
          {
            translatedName = addonCharacteristicsInfo.BattleTalkMessage.TranslatedSenderName + TranslationMarker;

            PluginLog.Debug($"Addon {addonName} translatedName node text in SetTranslationToAddon: {translatedName}");

            PluginLog.Debug($"Comparison to SetTranslationToAddon: 'this.configuration.TranslateNpcNames' is {configuration.TranslateNpcNames} and '!translatedName.Contains(TranslationMarker)' is {nameTextFromNode.Contains(TranslationMarker)} and the result is {configuration.TranslateNpcNames && nameTextFromNode.Contains(TranslationMarker)}");
            if (nameTextFromNode.Contains(TranslationMarker))
            {
              PluginLog.Debug($"Name node text in SetTranslationToAddon has already been processed.");
              return;
            }

            if (configuration.TranslateNpcNames)
            {
              PluginLog.Debug($"Setting name node text in SetTranslationToAddon.");
              nameNodeAsTextNode->SetText(translatedName);
              nameNodeAsTextNode->ResizeNodeForCurrentText();
            }
            else
            {
              PluginLog.Debug($"Setting name node text in SetTranslationToAddon.");
              nameNodeAsTextNode->SetText(addonCharacteristicsInfo.BattleTalkMessage.SenderName + TranslationMarker);
              nameNodeAsTextNode->ResizeNodeForCurrentText();
            }
          }
        }
        catch (Exception ex)
        {
          PluginLog.Error($"Error setting name node text in SetTranslationToAddon: {ex}");
        }
      }

      // Handle message node translation
      if (messageNodeAsTextNode != null)
      {
        var messageTextFromNode = MemoryHelper.ReadSeStringAsString(out _, (nint)messageNodeAsTextNode->NodeText.StringPtr.Value);

        PluginLog.Debug($"Addon {addonName} message node text in SetTranslationToAddon: {messageTextFromNode}");

        var cleanMessageTextFromNode = CleanString(messageTextFromNode);

        PluginLog.Debug($"Addon {addonName} clean message node text in SetTranslationToAddon: {cleanMessageTextFromNode}");
        try
        {
          var translatedMessage = string.Empty;

          if (addonName == "Talk")
          {
            translatedMessage = addonCharacteristicsInfo.TalkMessage.TranslatedTalkMessage + TranslationMarker;
          }

          if (addonName == "_BattleTalk")
          {
            translatedMessage = addonCharacteristicsInfo.BattleTalkMessage.TranslatedBattleTalkMessage + TranslationMarker;
          }

          PluginLog.Debug($"Addon {addonName} translatedMessage node text in SetTranslationToAddon: {translatedMessage}");

          PluginLog.Debug($"Comparison to SetTranslationToAddon: '!translatedMessage.Contains(TranslationMarker)' is {!messageTextFromNode.Contains(TranslationMarker)} and the result is {!messageTextFromNode.Contains(TranslationMarker)}");
          if (!cleanMessageTextFromNode.Contains(TranslationMarker))
          {
            PluginLog.Debug($"Setting message node text in SetTranslationToAddon.");

            // messageNodeAsTextNode->TextFlags = (byte)this.addonNodesFlags[this.addonCharacteristicsInfo.MessageNodeId];
            var parentNode = foundAddon->GetNodeById(1);
            var nineGridNode = foundAddon->GetNodeById(7);
            messageNodeAsTextNode->TextFlags = (byte)(TextFlags)((byte)TextFlags.WordWrap | (byte)TextFlags.MultiLine | (byte)TextFlags.AutoAdjustNodeSize);
            messageNodeAsTextNode->SetWidth((ushort)(parentNode->GetWidth() + 18));
            nineGridNode->SetWidth((ushort)(parentNode->GetWidth() + 36));
            messageNodeAsTextNode->SetText(translatedMessage);
            messageNodeAsTextNode->ResizeNodeForCurrentText();
          }
          else
          {
            PluginLog.Debug($"Message node text in SetTranslationToAddon has already been processed.");
            return;
          }
        }
        catch (Exception ex)
        {
          PluginLog.Error($"Error setting message node text in SetTranslationToAddon: {ex}");
        }
      }
    }

    public unsafe void SetTranslationToAddonViaAddonRefreshArgs(AddonRefreshArgs addonRefreshArgs)
    {
      PluginLog.Debug($"Called SetTranslationToAddon for addon {addonName}.");
      AtkUnitBase* foundAddon = null;

      if (addonRefreshArgs == null)
      {
        PluginLog.Debug($"AddonRefreshArgs is null in SetTranslationToAddon.");
        return;
      }

      AtkValue* addonAtkValues = (AtkValue*)addonRefreshArgs.AtkValues;

      try
      {
        var addon = GameGuiInterface.GetAddonByName(addonName, 1);

        PluginLog.Debug($"Addon {addonName} found in SetTranslationToAddon.");
        foundAddon = (AtkUnitBase*)addon;

        if (foundAddon == null)
        {
          return;
        }
      }
      catch (Exception ex)
      {
        PluginLog.Error($"Error retrieving addon: {ex}");
        return;
      }

      try
      {
        isAddonVisible = foundAddon->IsVisible;

        PluginLog.Debug($"Addon {addonName} is visible: {isAddonVisible} in SetTranslationToAddon.");
        if (!isAddonVisible)
        {
          PluginLog.Debug($"Addon {addonName} is not visible in SetTranslationToAddon.");
          return;
        }
      }
      catch (Exception ex)
      {
        PluginLog.Error($"Error checking addon visibility in SetTranslationToAddon: {ex}");
        return;
      }

      AtkTextNode* nameNodeAsTextNode = null;
      AtkTextNode* messageNodeAsTextNode = null;

      try
      {
        var nameNode = foundAddon->GetNodeById((uint)addonCharacteristicsInfo.NameNodeId);

        if (nameNode == null)
        {
          return;
        }

        nameNodeAsTextNode = nameNode->GetAsAtkTextNode();

        if (nameNodeAsTextNode == null)
        {
          return;
        }

        PluginLog.Debug($"Addon {addonName} name node found in SetTranslationToAddon.");
      }
      catch (Exception ex)
      {
        PluginLog.Error($"Error retrieving name node in SetTranslationToAddon: {ex}");
      }

      try
      {
        var messageNode = foundAddon->GetNodeById((uint)addonCharacteristicsInfo.MessageNodeId);

        if (messageNode == null)
        {
          return;
        }

        messageNodeAsTextNode = messageNode->GetAsAtkTextNode();

        if (messageNodeAsTextNode == null)
        {
          return;
        }

        PluginLog.Debug($"Addon {addonName} message node found in SetTranslationToAddon.");
      }
      catch (Exception ex)
      {
        PluginLog.Error($"Error retrieving message node in SetTranslationToAddon: {ex}");
      }

      // this.AdjustAddonNodesFlags();
      if (nameNodeAsTextNode != null)
      {
        var nameTextFromNode = CleanString(MemoryHelper.ReadSeStringAsString(out _, (nint)nameNodeAsTextNode->NodeText.StringPtr.Value));

        PluginLog.Debug($"Addon {addonName} name node text in SetTranslationToAddon: {nameTextFromNode}");
        try
        {
          var translatedName = string.Empty;

          if (addonName == "Talk")
          {
            translatedName = addonCharacteristicsInfo.TalkMessage.TranslatedSenderName + TranslationMarker;

            PluginLog.Debug($"Addon {addonName} translatedName node text in SetTranslationToAddon: {translatedName}");

            PluginLog.Debug($"Comparison to SetTranslationToAddon: 'this.configuration.TranslateNpcNames' is {configuration.TranslateNpcNames} and '!translatedName.Contains(TranslationMarker)' is {nameTextFromNode.Contains(TranslationMarker)} and the result is {configuration.TranslateNpcNames && nameTextFromNode.Contains(TranslationMarker)}");
            if (nameTextFromNode.Contains(TranslationMarker))
            {
              PluginLog.Debug($"Name node text in SetTranslationToAddon has already been processed.");
              return;
            }

            if (configuration.TranslateNpcNames)
            {
              PluginLog.Debug($"Setting name node text in SetTranslationToAddon using RefeshArgs.");

              if (addonAtkValues != null)
              {
                addonAtkValues[addonCharacteristicsInfo.AtkValuesNameStringIndex].SetManagedString(translatedName);
              }
            }
            else
            {
              PluginLog.Debug($"Setting name node text in SetTranslationToAddon using RefeshArgs");

              addonAtkValues[addonCharacteristicsInfo.AtkValuesNameStringIndex].SetManagedString(addonCharacteristicsInfo.TalkMessage.SenderName + TranslationMarker);
            }
          }

          if (addonName == "_BattleTalk")
          {
            translatedName = addonCharacteristicsInfo.BattleTalkMessage.TranslatedSenderName + TranslationMarker;

            PluginLog.Debug($"Addon {addonName} translatedName node text in SetTranslationToAddon: {translatedName}");

            PluginLog.Debug($"Comparison to SetTranslationToAddon: 'this.configuration.TranslateNpcNames' is {configuration.TranslateNpcNames} and '!translatedName.Contains(TranslationMarker)' is {nameTextFromNode.Contains(TranslationMarker)} and the result is {configuration.TranslateNpcNames && nameTextFromNode.Contains(TranslationMarker)}");
            if (nameTextFromNode.Contains(TranslationMarker))
            {
              PluginLog.Debug($"Name node text in SetTranslationToAddon has already been processed.");
              return;
            }

            if (configuration.TranslateNpcNames)
            {
              PluginLog.Debug($"Setting name node text in SetTranslationToAddon using RefeshArgs");

              if (addonAtkValues != null)
              {
                for (var i = 0; i < addonRefreshArgs.AtkValueCount; i++)
                {
                  if (addonAtkValues != null)
                  {
                    if (addonAtkValues[i].Type == ValueType.String && addonAtkValues[i].String != null)
                    {
                      var text = MemoryHelper.ReadSeStringAsString(out _, (nint)addonAtkValues[i].String.Value);
                      PluginLog.Debug($"Text from {addonRefreshArgs.AddonName} in pos {i} in HandleRefreshArgs: {text}");
                      if (i == 1)
                      {
                        addonAtkValues[i].SetManagedString(translatedName);
                      }
                    }
                  }
                }
              }
            }
            else
            {
              PluginLog.Debug($"Setting name node text in SetTranslationToAddon using RefeshArgs");

              addonAtkValues[addonCharacteristicsInfo.AtkValuesNameStringIndex].SetManagedString(addonCharacteristicsInfo.BattleTalkMessage.SenderName + TranslationMarker);
            }
          }
        }
        catch (Exception ex)
        {
          PluginLog.Error($"Error setting name node text in SetTranslationToAddon: {ex}");
        }
      }

      // Handle message node translation
      if (messageNodeAsTextNode != null)
      {
        var messageTextFromNode = MemoryHelper.ReadSeStringAsString(out _, (nint)messageNodeAsTextNode->NodeText.StringPtr.Value);

        PluginLog.Debug($"Addon {addonName} message node text in SetTranslationToAddon: {messageTextFromNode}");

        var cleanMessageTextFromNode = CleanString(messageTextFromNode);

        PluginLog.Debug($"Addon {addonName} clean message node text in SetTranslationToAddon: {cleanMessageTextFromNode}");
        try
        {
          var translatedMessage = string.Empty;

          if (addonName == "Talk")
          {
            translatedMessage = addonCharacteristicsInfo.TalkMessage.TranslatedTalkMessage + TranslationMarker;
          }

          if (addonName == "_BattleTalk")
          {
            translatedMessage = addonCharacteristicsInfo.BattleTalkMessage.TranslatedBattleTalkMessage + TranslationMarker;
          }

          PluginLog.Debug($"Addon {addonName} translatedMessage node text in SetTranslationToAddon: {translatedMessage}");

          PluginLog.Debug($"Comparison to SetTranslationToAddon: '!translatedMessage.Contains(TranslationMarker)' is {!messageTextFromNode.Contains(TranslationMarker)} and the result is {!messageTextFromNode.Contains(TranslationMarker)}");
          if (!cleanMessageTextFromNode.Contains(TranslationMarker))
          {
            PluginLog.Debug($"Setting message node text in SetTranslationToAddon.");

            // messageNodeAsTextNode->TextFlags = (byte)this.addonNodesFlags[this.addonCharacteristicsInfo.MessageNodeId];
            if (addonAtkValues != null)
            {
              for (var i = 0; i < addonRefreshArgs.AtkValueCount; i++)
              {
                if (addonAtkValues != null)
                {
                  if (addonAtkValues[i].Type == ValueType.String && addonAtkValues[i].String != null)
                  {
                    var text = MemoryHelper.ReadSeStringAsString(out _, (nint)addonAtkValues[i].String.Value);
                    PluginLog.Debug($"Text from {addonRefreshArgs.AddonName} in pos {i} in HandleRefreshArgs: {text}");

                    if (i == 0)
                    {
                      addonAtkValues[i].SetManagedString(translatedMessage);
                    }
                  }
                }
              }
            }
          }
          else
          {
            PluginLog.Debug($"Message node text in SetTranslationToAddon has already been processed.");
            return;
          }
        }
        catch (Exception ex)
        {
          PluginLog.Error($"Error setting message node text in SetTranslationToAddon: {ex}");
        }
      }
    }

    protected virtual void Dispose(bool disposing)
    {
      if (!disposedValue)
      {
        if (disposing)
        {
          cts.Cancel();

          try
          {
            // Wait for the task to complete within a reasonable time frame.
            translationTask.Wait(5000); // Adjust timeout as needed.
          }
          catch (AggregateException ae)
          {
            // Handle or log exceptions that may occur when waiting for the task.
            foreach (var ex in ae.InnerExceptions)
            {
              PluginLog.Error($"Exception in Dispose method: {ex}");
            }
          }
          finally
          {
            // Dispose of the task and the cancellation token source.
            translationTask.Dispose();
            cts.Dispose();
          }
        }

        disposedValue = true;
      }
    }

    public void Dispose()
    {
      Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }

    private class TranslationEntry
    {
      public string OriginalText { get; set; }

      public string TranslatedText { get; set; }

      public bool IsTranslated { get; set; }

      public override string ToString()
      {
        return $"OriginalText: {OriginalText}, TranslatedText: {TranslatedText}, IsTranslated: {IsTranslated}";
      }
    }

    private class AddonCharacteristicsInfo
    {
      public string AddonName { get; set; }

      public bool IsComplexAddon { get; set; }

      public int NameNodeId { get; set; }

      public int MessageNodeId { get; set; }

      public int AtkValuesNameStringIndex { get; set; }

      public int AtkValuesMessageStringIndex { get; set; }

      public NodeFlags NameNodeFlags { get; set; }

      public NodeFlags MessageNodeFlags { get; set; }

      public string ComplexStructure { get; set; }

      public TalkMessage TalkMessage { get; set; }

      public BattleTalkMessage BattleTalkMessage { get; set; }

      public TalkSubtitleMessage TalkSubtitleMessage { get; set; }

      public override string ToString()
      {
        return $"AddonName: {AddonName}, IsComplexAddon: {IsComplexAddon}, NameNodeId: {NameNodeId}, MessageNodeId: {MessageNodeId}, ComplexStructure: {ComplexStructure}, TalkMessage: {TalkMessage}, BattleTalkMessage: {BattleTalkMessage}, TalkSubtitleMessage: {TalkSubtitleMessage}";
      }
    }
  }
}
