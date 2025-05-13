// <copyright file="UIAddonHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite.Models;

using static Echoglossian.Echoglossian;

using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Echoglossian.NativeUI.Handlers
{
  internal class UiAddonHandler : IDisposable
  {
    private bool disposedValue;
    private CancellationTokenSource cancellationTokenSource;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="UiAddonHandler"/> class.
    /// </summary>
    /// <param name="configuration"></param>
    /// <param name="uiFont"></param>
    /// <param name="fontLoaded"></param>
    /// <param name="langToTranslateTo"></param>
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
      this.clientLanguage = ClientStateInterface.ClientLanguage;
      this.translationService = new TranslationService(configuration, PluginLog, new Sanitizer(this.clientLanguage));
      this.translations = new ConcurrentDictionary<int, TranslationEntry>();
      this.configDir = PluginInterface.GetPluginConfigDirectory() + Path.DirectorySeparatorChar;
      this.cancellationTokenSource = new CancellationTokenSource();
      this.translationTask = Task.Run(async () => await this.ProcessTranslations(this.cancellationTokenSource.Token));
    }

#nullable enable
    public void EgloAddonHandler(string addonName, AddonSetupArgs? setupArgs = null)
    {
      this.addonName = addonName;
      if (setupArgs != null)
      {
        this.addonSetupArgs = setupArgs;
      }

      this.HandleCommonLogic();
    }

    public void EgloAddonHandler(string addonName, AddonReceiveEventArgs? receiveEventArgs = null)
    {
      this.addonName = addonName;
      if (receiveEventArgs != null)
      {
        this.addonReceiveEventArgs = receiveEventArgs;
      }

      this.HandleCommonLogic();
    }

    public void EgloAddonHandler(string addonName, AddonUpdateArgs? updateArgs = null)
    {
      this.addonName = addonName;
      if (updateArgs != null)
      {
        this.addonUpdateArgs = updateArgs;
      }

      this.HandleCommonLogic();
    }

    public void EgloAddonHandler(string addonName, AddonDrawArgs? drawArgs = null)
    {
      this.addonName = addonName;
      if (drawArgs != null)
      {
        this.addonDrawArgs = drawArgs;
      }

      this.HandleCommonLogic();
    }

    public void EgloAddonHandler(string addonName, AddonFinalizeArgs? finalizeArgs = null)
    {
      this.addonName = addonName;
      if (finalizeArgs != null)
      {
        this.addonFinalizeArgs = finalizeArgs;
      }

      this.HandleCommonLogic();
    }

    public void EgloAddonHandler(string addonName, AddonRequestedUpdateArgs? requestedUpdateArgs = null)
    {
      this.addonName = addonName;
      if (requestedUpdateArgs != null)
      {
        this.addonRequestedUpdateArgs = requestedUpdateArgs;
      }

      this.HandleCommonLogic();
    }

    public void EgloAddonHandler(string addonName, AddonRefreshArgs? refreshArgs = null)
    {
      this.addonName = addonName;
      if (refreshArgs != null)
      {
        this.addonRefreshArgs = refreshArgs;
      }

      this.HandleCommonLogic();
    }

    private void HandleCommonLogic()
    {
      if (string.IsNullOrEmpty(this.addonName))
      {
        return;
      }

      this.DetermineAddonCharacteristics();
      // this.AdjustAddonNodesFlags();
      this.ExploreAddon();
    }

    private void DetermineAddonCharacteristics()
    {
      switch (this.addonName)
      {
        case "Talk":
          this.addonCharacteristicsInfo = new()
          {
            AddonName = this.addonName,
            IsComplexAddon = false,
            NameNodeId = 2,
            MessageNodeId = 3,
            AtkValuesNameStringIndex = 1,
            AtkValuesMessageStringIndex = 0,
            TalkMessage = new TalkMessage(
                  senderName: string.Empty,
                  originalTalkMessage: string.Empty,
                  originalSenderNameLang: this.clientLanguage.Humanize(),
                  translatedTalkMessage: string.Empty,
                  originalTalkMessageLang: this.clientLanguage.Humanize(),
                  translationLang: this.langToTranslateTo,
                  translationEngine: this.configuration.ChosenTransEngine,
                  translatedSenderName: string.Empty,
                  createdDate: DateTime.Now,
                  updatedDate: DateTime.Now),
          };
          break;
        case "_BattleTalk":
          this.addonCharacteristicsInfo = new()
          {
            AddonName = this.addonName,
            IsComplexAddon = false,
            NameNodeId = 4,
            MessageNodeId = 6,
            BattleTalkMessage = new BattleTalkMessage(
                  senderName: string.Empty,
                  originalBattleTalkMessage: string.Empty,
                  originalSenderNameLang: this.clientLanguage.Humanize(),
                  translatedBattleTalkMessage: string.Empty,
                  originalBattleTalkMessageLang: this.clientLanguage.Humanize(),
                  translationLang: this.langToTranslateTo,
                  translationEngine: this.configuration.ChosenTransEngine,
                  translatedSenderName: string.Empty,
                  createdDate: DateTime.Now,
                  updatedDate: DateTime.Now),
          };
          break;
        case "TalkSubtitle":
          this.addonCharacteristicsInfo = new()
          {
            AddonName = this.addonName,
            IsComplexAddon = false,
            AtkValuesMessageStringIndex = 0,
            TalkSubtitleMessage = new TalkSubtitleMessage(
                  originalTalkSubtitleMessage: string.Empty,
                  translatedTalkSubtitleMessage: string.Empty,
                  originalTalkSubtitleMessageLang: this.clientLanguage.Humanize(),
                  translationLang: this.langToTranslateTo,
                  translationEngine: this.configuration.ChosenTransEngine,
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
      this.addonNodesFlags = new Dictionary<int, TextFlags>();

      switch (this.addonName)
      {
        case "Talk":
          this.addonNodesFlags.Add(3, (TextFlags)((byte)TextFlags.WordWrap | (byte)TextFlags.MultiLine));
          break;
        case "_BattleTalk":
          this.addonNodesFlags.Add(6, (TextFlags)((byte)TextFlags.WordWrap | (byte)TextFlags.MultiLine));
          break;
        case "TalkSubtitle":
          this.addonNodesFlags.Add(3, (TextFlags)((byte)TextFlags.WordWrap | (byte)TextFlags.MultiLine));
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
        var addon = GameGuiInterface.GetAddonByName(this.addonName, 1);
        foundAddon = (AtkUnitBase*)addon;
        if (foundAddon == null)
        {
          PluginLog.Debug($"Addon {this.addonName} not found in ExploreAddon.");
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
        this.isAddonVisible = foundAddon->IsVisible;
        if (!this.isAddonVisible)
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
        var nameNode = foundAddon->GetNodeById((uint)this.addonCharacteristicsInfo.NameNodeId);

        if (nameNode == null)
        {
          return;
        }

        nameNodeAsTextNode = nameNode->GetAsAtkTextNode();

        if (nameNodeAsTextNode == null)
        {
          return;
        }

        PluginLog.Debug($"Addon {this.addonName} name node found in ExploreAddon.");
        PluginLog.Debug($"Addon {this.addonName} name node text in ExploreAddon: {MemoryHelper.ReadSeStringAsString(out _, (nint)nameNodeAsTextNode->NodeText.StringPtr.Value)}");
      }
      catch (Exception ex)
      {
        PluginLog.Error($"Error retrieving name node: {ex}");
      }

      try
      {
        var messageNode = foundAddon->GetNodeById((uint)this.addonCharacteristicsInfo.MessageNodeId);

        if (messageNode == null)
        {
          return;
        }

        messageNodeAsTextNode = messageNode->GetAsAtkTextNode();

        if (messageNodeAsTextNode == null)
        {
          return;
        }

        PluginLog.Debug($"Addon {this.addonName} message node found in ExploreAddon.");
        PluginLog.Debug($"Addon {this.addonName} message node text in ExploreAddon: {MemoryHelper.ReadSeStringAsString(out _, (nint)messageNodeAsTextNode->NodeText.StringPtr.Value)}");
      }
      catch (Exception ex)
      {
        PluginLog.Error($"Error retrieving message node: {ex}");
      }

      if (nameNodeAsTextNode != null)
      {
        var nameText = CleanString(MemoryHelper.ReadSeStringAsString(out _, (nint)nameNodeAsTextNode->NodeText.StringPtr.Value));

        PluginLog.Debug($"Addon {this.addonName} name node text in ExploreAddon: {nameText}");

        if (string.IsNullOrEmpty(nameText) || nameText.Contains(TranslationMarker))
        {
          PluginLog.Debug($"Addon {this.addonName} name node has already been processed.");
          return;
        }

        if (this.addonName == "Talk")
        {
          this.addonCharacteristicsInfo.TalkMessage.SenderName = nameText;
        }

        if (this.addonName == "_BattleTalk")
        {
          this.addonCharacteristicsInfo.BattleTalkMessage.SenderName = nameText;
        }

        if (!this.configuration.TranslateNpcNames)
        {
          if (this.addonName == "Talk")
          {
            this.addonCharacteristicsInfo.TalkMessage.TranslatedSenderName = nameText;
          }

          if (this.addonName == "_BattleTalk")
          {
            this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedSenderName = nameText;
          }
        }
      }

      if (messageNodeAsTextNode != null)
      {
        var messageNodeText = MemoryHelper.ReadSeStringAsString(out _, (nint)messageNodeAsTextNode->NodeText.StringPtr.Value);

        PluginLog.Debug($"Addon {this.addonName} message node text in ExploreAddon: {messageNodeText}");

        var messageText = CleanString(messageNodeText);

        PluginLog.Debug($"Addon {this.addonName} clean message node text in ExploreAddon: {messageText}");

        if (!string.IsNullOrEmpty(messageText) && messageText.Contains(TranslationMarker))
        {
          PluginLog.Debug($"Addon {this.addonName} message node has already been processed.");
          return;
        }

        if (this.addonName == "Talk")
        {
          this.addonCharacteristicsInfo.TalkMessage.OriginalTalkMessage = messageText;
        }

        if (this.addonName == "_BattleTalk")
        {
          this.addonCharacteristicsInfo.BattleTalkMessage.OriginalBattleTalkMessage = messageText;
        }
      }

      this.CheckDatabaseForTranslation();
    }

    private void CheckDatabaseForTranslation()
    {
      if (this.addonName == "Talk")
      {
        var talkMessage = this.addonCharacteristicsInfo.TalkMessage;

        PluginLog.Debug($"Checking database for: {this.addonCharacteristicsInfo.TalkMessage}");

        if (talkMessage != null && !string.IsNullOrEmpty(talkMessage.SenderName) && !string.IsNullOrEmpty(talkMessage.OriginalTalkMessage))
        {
          if (FindTalkMessage(talkMessage))
          {
            this.addonCharacteristicsInfo.TalkMessage.TranslatedTalkMessage = FoundTalkMessage.TranslatedTalkMessage + TranslationMarker;

            PluginLog.Debug($"Addon {this.addonName} message node text found in database is {this.addonCharacteristicsInfo.TalkMessage.TranslatedTalkMessage}");
            if (this.configuration.TranslateNpcNames)
            {
              this.addonCharacteristicsInfo.TalkMessage.TranslatedSenderName = FoundTalkMessage.TranslatedSenderName + TranslationMarker;
              PluginLog.Debug($"Addon {this.addonName} sender name node text found in database is {this.addonCharacteristicsInfo.TalkMessage.TranslatedSenderName}");
            }
            else
            {
              this.addonCharacteristicsInfo.TalkMessage.TranslatedSenderName = FoundTalkMessage.SenderName + TranslationMarker;
            }

            //this.SetTranslationToAddon();
          }
          else
          {
            PluginLog.Debug($"Current addon {this.addonName} not found in database. Sending to translate!");
            this.TranslateTexts(talkMessage.OriginalTalkMessage, "Talk");
          }
        }
      }
      else if (this.addonName == "_BattleTalk")
      {
        var battleTalkMessage = this.addonCharacteristicsInfo.BattleTalkMessage;

        PluginLog.Debug($"Checking database for: {this.addonCharacteristicsInfo.BattleTalkMessage}");

        if (battleTalkMessage != null && !string.IsNullOrEmpty(battleTalkMessage.SenderName) && !string.IsNullOrEmpty(battleTalkMessage.OriginalBattleTalkMessage))
        {
          if (FindBattleTalkMessage(battleTalkMessage))
          {
            this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedBattleTalkMessage = FoundBattleTalkMessage.TranslatedBattleTalkMessage + TranslationMarker;

            PluginLog.Debug($"Addon {this.addonName} message node text found in database is {this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedBattleTalkMessage}");
            if (this.configuration.TranslateNpcNames)
            {
              this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedSenderName = FoundBattleTalkMessage.TranslatedSenderName + TranslationMarker;
              PluginLog.Debug($"Addon {this.addonName} sender name node text found in database is {this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedSenderName}");
            }
            else
            {
              this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedSenderName = FoundBattleTalkMessage.SenderName + TranslationMarker;
            }

            //this.SetTranslationToAddon();
          }
          else
          {
            PluginLog.Debug($"Current addon {this.addonName} not found in database. Sending to translate!");
            this.TranslateTexts(battleTalkMessage.OriginalBattleTalkMessage, "_BattleTalk");
          }
        }
      }
    }

    private void TranslateTexts(string originalText, string addonType)
    {
      Task.Run(async () =>
      {
        var translation = await this.translationService.TranslateAsync(originalText, this.clientLanguage.Humanize(), this.langToTranslateTo);
        if (addonType == "Talk")
        {
          this.addonCharacteristicsInfo.TalkMessage.TranslatedTalkMessage = translation;
          if (this.configuration.TranslateNpcNames)
          {
            this.addonCharacteristicsInfo.TalkMessage.TranslatedSenderName = await this.translationService.TranslateAsync(this.addonCharacteristicsInfo.TalkMessage.SenderName, this.clientLanguage.Humanize(), this.langToTranslateTo);
          }

          this.SaveTranslationToDatabase(originalText, translation, addonType);
        }
        else if (addonType == "_BattleTalk")
        {
          this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedBattleTalkMessage = translation;
          if (this.configuration.TranslateNpcNames)
          {
            this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedSenderName = await this.translationService.TranslateAsync(this.addonCharacteristicsInfo.BattleTalkMessage.SenderName, this.clientLanguage.Humanize(), this.langToTranslateTo);
          }

          this.SaveTranslationToDatabase(originalText, translation, addonType);
        }

        this.translatedTexts.Add(translation);

        // this.SetTranslationToAddon();
      });
    }

    private void SaveTranslationToDatabase(string originalText, string translatedText, string addonType)
    {
      if (addonType == "Talk")
      {
        var talkMessage = this.addonCharacteristicsInfo.TalkMessage;

        if (talkMessage.TranslatedTalkMessage != translatedText)
        {
          talkMessage.OriginalTalkMessage = originalText;
          talkMessage.TranslatedTalkMessage = translatedText + TranslationMarker;
          if (!this.translatedTexts.Contains(talkMessage.TranslatedTalkMessage))
          {
            InsertTalkData(talkMessage);
            this.translatedTexts.Add(talkMessage.TranslatedTalkMessage);
          }
        }
      }
      else if (addonType == "_BattleTalk")
      {
        var battleTalkMessage = this.addonCharacteristicsInfo.BattleTalkMessage;

        if (battleTalkMessage.TranslatedBattleTalkMessage != translatedText)
        {
          battleTalkMessage.OriginalBattleTalkMessage = originalText;
          battleTalkMessage.TranslatedBattleTalkMessage = translatedText + TranslationMarker;
          if (!this.translatedTexts.Contains(battleTalkMessage.TranslatedBattleTalkMessage))
          {
            InsertBattleTalkData(battleTalkMessage);
            this.translatedTexts.Add(battleTalkMessage.TranslatedBattleTalkMessage);
          }
        }
      }
    }

    private async Task ProcessTranslations(CancellationToken token)
    {
      while (!token.IsCancellationRequested)
      {
        foreach (var key in this.translations.Keys)
        {
          if (this.translations.TryGetValue(key, out var entry) && !entry.IsTranslated && this.isAddonVisible)
          {
            await this.TranslateText(key, entry.OriginalText);
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
        var translation = await this.translationService.TranslateAsync(text, this.clientLanguage.Humanize(), this.langToTranslateTo);
        if (this.translations.TryGetValue(id, out var entry))
        {
          entry.TranslatedText = translation;
          entry.IsTranslated = true;
          this.translatedTexts.Add(translation);

          await Task.Run(() => this.SaveTranslationToDatabase(text, translation, this.addonName));
        }
      }
      catch (Exception e)
      {
        PluginLog.Error($"Error in TranslateText method: {e}");
      }
    }

    public unsafe void SetTranslationToAddon()
    {
      PluginLog.Debug($"Called SetTranslationToAddon for addon {this.addonName}.");
      AtkUnitBase* foundAddon = null;

      try
      {
        var addon = GameGuiInterface.GetAddonByName(this.addonName, 1);

        PluginLog.Debug($"Addon {this.addonName} found in SetTranslationToAddon.");
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
        this.isAddonVisible = foundAddon->IsVisible;

        PluginLog.Debug($"Addon {this.addonName} is visible: {this.isAddonVisible} in SetTranslationToAddon.");
        if (!this.isAddonVisible)
        {
          PluginLog.Debug($"Addon {this.addonName} is not visible in SetTranslationToAddon.");
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
        var nameNode = foundAddon->GetNodeById((uint)this.addonCharacteristicsInfo.NameNodeId);

        if (nameNode == null)
        {
          return;
        }

        nameNodeAsTextNode = nameNode->GetAsAtkTextNode();

        if (nameNodeAsTextNode == null)
        {
          return;
        }

        PluginLog.Debug($"Addon {this.addonName} name node found in SetTranslationToAddon.");
      }
      catch (Exception ex)
      {
        PluginLog.Error($"Error retrieving name node in SetTranslationToAddon: {ex}");
      }

      try
      {
        var messageNode = foundAddon->GetNodeById((uint)this.addonCharacteristicsInfo.MessageNodeId);

        if (messageNode == null)
        {
          return;
        }

        messageNodeAsTextNode = messageNode->GetAsAtkTextNode();

        if (messageNodeAsTextNode == null)
        {
          return;
        }

        PluginLog.Debug($"Addon {this.addonName} message node found in SetTranslationToAddon.");
      }
      catch (Exception ex)
      {
        PluginLog.Error($"Error retrieving message node in SetTranslationToAddon: {ex}");
      }

      // this.AdjustAddonNodesFlags();
      if (nameNodeAsTextNode != null)
      {
        var nameTextFromNode = CleanString(MemoryHelper.ReadSeStringAsString(out _, (nint)nameNodeAsTextNode->NodeText.StringPtr.Value));

        PluginLog.Debug($"Addon {this.addonName} name node text in SetTranslationToAddon: {nameTextFromNode}");
        try
        {
          var translatedName = string.Empty;

          if (this.addonName == "Talk")
          {
            translatedName = this.addonCharacteristicsInfo.TalkMessage.TranslatedSenderName + TranslationMarker;

            PluginLog.Debug($"Addon {this.addonName} translatedName node text in SetTranslationToAddon: {translatedName}");

            PluginLog.Debug($"Comparison to SetTranslationToAddon: 'this.configuration.TranslateNpcNames' is {this.configuration.TranslateNpcNames} and '!translatedName.Contains(TranslationMarker)' is {nameTextFromNode.Contains(TranslationMarker)} and the result is {this.configuration.TranslateNpcNames && nameTextFromNode.Contains(TranslationMarker)}");
            if (nameTextFromNode.Contains(TranslationMarker))
            {
              PluginLog.Debug($"Name node text in SetTranslationToAddon has already been processed.");
              return;
            }

            if (this.configuration.TranslateNpcNames)
            {
              PluginLog.Debug($"Setting name node text in SetTranslationToAddon.");
              nameNodeAsTextNode->SetText(translatedName);
              nameNodeAsTextNode->ResizeNodeForCurrentText();
            }
            else
            {
              PluginLog.Debug($"Setting name node text in SetTranslationToAddon.");
              nameNodeAsTextNode->SetText(this.addonCharacteristicsInfo.TalkMessage.SenderName + TranslationMarker);
              nameNodeAsTextNode->ResizeNodeForCurrentText();
            }
          }

          if (this.addonName == "_BattleTalk")
          {
            translatedName = this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedSenderName + TranslationMarker;

            PluginLog.Debug($"Addon {this.addonName} translatedName node text in SetTranslationToAddon: {translatedName}");

            PluginLog.Debug($"Comparison to SetTranslationToAddon: 'this.configuration.TranslateNpcNames' is {this.configuration.TranslateNpcNames} and '!translatedName.Contains(TranslationMarker)' is {nameTextFromNode.Contains(TranslationMarker)} and the result is {this.configuration.TranslateNpcNames && nameTextFromNode.Contains(TranslationMarker)}");
            if (nameTextFromNode.Contains(TranslationMarker))
            {
              PluginLog.Debug($"Name node text in SetTranslationToAddon has already been processed.");
              return;
            }

            if (this.configuration.TranslateNpcNames)
            {
              PluginLog.Debug($"Setting name node text in SetTranslationToAddon.");
              nameNodeAsTextNode->SetText(translatedName);
              nameNodeAsTextNode->ResizeNodeForCurrentText();
            }
            else
            {
              PluginLog.Debug($"Setting name node text in SetTranslationToAddon.");
              nameNodeAsTextNode->SetText(this.addonCharacteristicsInfo.BattleTalkMessage.SenderName + TranslationMarker);
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

        PluginLog.Debug($"Addon {this.addonName} message node text in SetTranslationToAddon: {messageTextFromNode}");

        var cleanMessageTextFromNode = CleanString(messageTextFromNode);

        PluginLog.Debug($"Addon {this.addonName} clean message node text in SetTranslationToAddon: {cleanMessageTextFromNode}");
        try
        {
          var translatedMessage = string.Empty;

          if (this.addonName == "Talk")
          {
            translatedMessage = this.addonCharacteristicsInfo.TalkMessage.TranslatedTalkMessage + TranslationMarker;
          }

          if (this.addonName == "_BattleTalk")
          {
            translatedMessage = this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedBattleTalkMessage + TranslationMarker;
          }

          PluginLog.Debug($"Addon {this.addonName} translatedMessage node text in SetTranslationToAddon: {translatedMessage}");

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
      PluginLog.Debug($"Called SetTranslationToAddon for addon {this.addonName}.");
      AtkUnitBase* foundAddon = null;

      if (addonRefreshArgs == null)
      {
        PluginLog.Debug($"AddonRefreshArgs is null in SetTranslationToAddon.");
        return;
      }

      AtkValue* addonAtkValues = (AtkValue*)addonRefreshArgs.AtkValues;

      try
      {
        var addon = GameGuiInterface.GetAddonByName(this.addonName, 1);

        PluginLog.Debug($"Addon {this.addonName} found in SetTranslationToAddon.");
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
        this.isAddonVisible = foundAddon->IsVisible;

        PluginLog.Debug($"Addon {this.addonName} is visible: {this.isAddonVisible} in SetTranslationToAddon.");
        if (!this.isAddonVisible)
        {
          PluginLog.Debug($"Addon {this.addonName} is not visible in SetTranslationToAddon.");
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
        var nameNode = foundAddon->GetNodeById((uint)this.addonCharacteristicsInfo.NameNodeId);

        if (nameNode == null)
        {
          return;
        }

        nameNodeAsTextNode = nameNode->GetAsAtkTextNode();

        if (nameNodeAsTextNode == null)
        {
          return;
        }

        PluginLog.Debug($"Addon {this.addonName} name node found in SetTranslationToAddon.");
      }
      catch (Exception ex)
      {
        PluginLog.Error($"Error retrieving name node in SetTranslationToAddon: {ex}");
      }

      try
      {
        var messageNode = foundAddon->GetNodeById((uint)this.addonCharacteristicsInfo.MessageNodeId);

        if (messageNode == null)
        {
          return;
        }

        messageNodeAsTextNode = messageNode->GetAsAtkTextNode();

        if (messageNodeAsTextNode == null)
        {
          return;
        }

        PluginLog.Debug($"Addon {this.addonName} message node found in SetTranslationToAddon.");
      }
      catch (Exception ex)
      {
        PluginLog.Error($"Error retrieving message node in SetTranslationToAddon: {ex}");
      }

      // this.AdjustAddonNodesFlags();
      if (nameNodeAsTextNode != null)
      {
        var nameTextFromNode = CleanString(MemoryHelper.ReadSeStringAsString(out _, (nint)nameNodeAsTextNode->NodeText.StringPtr.Value));

        PluginLog.Debug($"Addon {this.addonName} name node text in SetTranslationToAddon: {nameTextFromNode}");
        try
        {
          var translatedName = string.Empty;

          if (this.addonName == "Talk")
          {
            translatedName = this.addonCharacteristicsInfo.TalkMessage.TranslatedSenderName + TranslationMarker;

            PluginLog.Debug($"Addon {this.addonName} translatedName node text in SetTranslationToAddon: {translatedName}");

            PluginLog.Debug($"Comparison to SetTranslationToAddon: 'this.configuration.TranslateNpcNames' is {this.configuration.TranslateNpcNames} and '!translatedName.Contains(TranslationMarker)' is {nameTextFromNode.Contains(TranslationMarker)} and the result is {this.configuration.TranslateNpcNames && nameTextFromNode.Contains(TranslationMarker)}");
            if (nameTextFromNode.Contains(TranslationMarker))
            {
              PluginLog.Debug($"Name node text in SetTranslationToAddon has already been processed.");
              return;
            }

            if (this.configuration.TranslateNpcNames)
            {
              PluginLog.Debug($"Setting name node text in SetTranslationToAddon using RefeshArgs.");

              if (addonAtkValues != null)
              {
                addonAtkValues[this.addonCharacteristicsInfo.AtkValuesNameStringIndex].SetManagedString(translatedName);
              }
            }
            else
            {
              PluginLog.Debug($"Setting name node text in SetTranslationToAddon using RefeshArgs");

              addonAtkValues[this.addonCharacteristicsInfo.AtkValuesNameStringIndex].SetManagedString(this.addonCharacteristicsInfo.TalkMessage.SenderName + TranslationMarker);
            }
          }

          if (this.addonName == "_BattleTalk")
          {
            translatedName = this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedSenderName + TranslationMarker;

            PluginLog.Debug($"Addon {this.addonName} translatedName node text in SetTranslationToAddon: {translatedName}");

            PluginLog.Debug($"Comparison to SetTranslationToAddon: 'this.configuration.TranslateNpcNames' is {this.configuration.TranslateNpcNames} and '!translatedName.Contains(TranslationMarker)' is {nameTextFromNode.Contains(TranslationMarker)} and the result is {this.configuration.TranslateNpcNames && nameTextFromNode.Contains(TranslationMarker)}");
            if (nameTextFromNode.Contains(TranslationMarker))
            {
              PluginLog.Debug($"Name node text in SetTranslationToAddon has already been processed.");
              return;
            }

            if (this.configuration.TranslateNpcNames)
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

              addonAtkValues[this.addonCharacteristicsInfo.AtkValuesNameStringIndex].SetManagedString(this.addonCharacteristicsInfo.BattleTalkMessage.SenderName + TranslationMarker);
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

        PluginLog.Debug($"Addon {this.addonName} message node text in SetTranslationToAddon: {messageTextFromNode}");

        var cleanMessageTextFromNode = CleanString(messageTextFromNode);

        PluginLog.Debug($"Addon {this.addonName} clean message node text in SetTranslationToAddon: {cleanMessageTextFromNode}");
        try
        {
          var translatedMessage = string.Empty;

          if (this.addonName == "Talk")
          {
            translatedMessage = this.addonCharacteristicsInfo.TalkMessage.TranslatedTalkMessage + TranslationMarker;
          }

          if (this.addonName == "_BattleTalk")
          {
            translatedMessage = this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedBattleTalkMessage + TranslationMarker;
          }

          PluginLog.Debug($"Addon {this.addonName} translatedMessage node text in SetTranslationToAddon: {translatedMessage}");

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
      if (!this.disposedValue)
      {
        if (disposing)
        {
          this.cancellationTokenSource.Cancel();

          try
          {
            // Wait for the task to complete within a reasonable time frame.
            this.translationTask.Wait(5000); // Adjust timeout as needed.
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
            this.translationTask.Dispose();
            this.cancellationTokenSource.Dispose();
          }
        }

        this.disposedValue = true;
      }
    }

    public void Dispose()
    {
      this.Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }

    private class TranslationEntry
    {
      public string OriginalText { get; set; }

      public string TranslatedText { get; set; }

      public bool IsTranslated { get; set; }

      public override string ToString()
      {
        return $"OriginalText: {this.OriginalText}, TranslatedText: {this.TranslatedText}, IsTranslated: {this.IsTranslated}";
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
        return $"AddonName: {this.AddonName}, IsComplexAddon: {this.IsComplexAddon}, NameNodeId: {this.NameNodeId}, MessageNodeId: {this.MessageNodeId}, ComplexStructure: {this.ComplexStructure}, TalkMessage: {this.TalkMessage}, BattleTalkMessage: {this.BattleTalkMessage}, TalkSubtitleMessage: {this.TalkSubtitleMessage}";
      }
    }
  }
}
