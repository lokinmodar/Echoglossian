// <copyright file="UiTalkAsyncHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian
{
  public partial class Echoglossian
  {
    /// <summary>
    /// Handles the translation of the Talk addon messages asynchronously.
    /// </summary>
    /// <param name="translatedName"></param>
    /// <param name="translatedText"></param>
    /// <param name="originalName"></param>
    private void UpdateTalkOverlay(string translatedName, string translatedText, string originalName = "")
    {
      bool hasValidText = !string.IsNullOrWhiteSpace(translatedText);

      this.talkOverlay.NameSemaphore.Wait();

      PluginLog.Debug(
        $"UpdateTalkOverlay: {translatedName}: {translatedText} - OriginalName: {originalName} - HasValidText: {hasValidText}");

      if (!string.IsNullOrWhiteSpace(originalName))
      {
        this.talkOverlay.OriginalName = originalName;
      }

      if (!string.IsNullOrWhiteSpace(translatedName))
      {
        this.talkOverlay.CurrentName = translatedName;
      }

      this.talkOverlay.NameSemaphore.Release();

      this.talkOverlay.Semaphore.Wait();
      this.talkOverlay.CurrentText = hasValidText ? translatedText : Resources.WaitingForTranslation;
      this.talkOverlay.Display = hasValidText;
      this.talkOverlay.Semaphore.Release();
    }

    /// <summary>
    /// Formats the talk message for translation.
    /// </summary>
    /// <param name="nameToTranslate"></param>
    /// <param name="textToTranslate"></param>
    private unsafe void TranslateTalk(string nameToTranslate, string textToTranslate)
    {
      PluginLog.Debug($"TranslateTalk: {nameToTranslate}: {textToTranslate}");

      Task.Run(() =>
      {
        try
        {
          TalkMessage talkMessage = this.FormatTalkMessage(nameToTranslate, textToTranslate);
          TalkMessage foundTalkMessage = this.FindAndReturnTalkMessage(talkMessage);

          if (foundTalkMessage != null)
          {
            this.UpdateTalkOverlay(
              foundTalkMessage.TranslatedSenderName,
              foundTalkMessage.TranslatedTalkMessage,
              foundTalkMessage.SenderName);
            PluginLog.Debug($"From database - Name: {foundTalkMessage.TranslatedSenderName}, Message: {foundTalkMessage.TranslatedTalkMessage}");
          }
          else
          {
            string textTranslation = this.Translate(textToTranslate);
            string nameTranslation = nameToTranslate.IsNullOrEmpty() ? string.Empty : this.Translate(nameToTranslate);

            this.UpdateTalkOverlay(nameTranslation, textTranslation, nameToTranslate);

            TalkMessage translatedTalkData = new TalkMessage(
              nameToTranslate,
              textToTranslate,
              ClientStateInterface.ClientLanguage.Humanize(),
              ClientStateInterface.ClientLanguage.Humanize(),
              nameTranslation,
              textTranslation,
              LangDict[LanguageInt].Code,
              this.configuration.ChosenTransEngine,
              rtlLangTranslationImageData: null,
              DateTime.Now,
              DateTime.Now);

            string result = InsertTalkData(translatedTalkData).Result;
            PluginLog.Debug($"TranslateTalk - Talk Message DB Insert operation result: {result}");
          }
        }
        catch (Exception e)
        {
          PluginLog.Debug("TranslateTalk Exception: " + e);
        }
      });
    }

    /// <summary>
    /// Replaces the text in the Talk addon with translated text.
    /// </summary>
    private unsafe void TranslateTalkReplacing()
    {
      PluginLog.Debug("TranslateTalkReplacing");

      if (this.configuration.UseImGuiForTalk && !this.configuration.SwapTextsUsingImGui)
      {
        return;
      }

      try
      {
        // TODO: adapt the structure to use AtkValues instead of nodes for better performance

        var addon = GameGuiInterface.GetAddonByName("Talk");
        var talkAddon = (AtkUnitBase*)addon;
        if (talkAddon == null || !talkAddon->IsVisible)
        {
          return;
        }

        var nameNode = talkAddon->GetTextNodeById(2);
        var textNode = talkAddon->GetTextNodeById(3);
        if (textNode == null || textNode->NodeText.IsEmpty)
        {
          return;
        }

        if (this.configuration.TranslateNpcNames && nameNode != null && !nameNode->NodeText.IsEmpty)
        {
          this.talkOverlay.NameSemaphore.Wait();
          var translatedName = this.talkOverlay.CurrentName;
          this.talkOverlay.NameSemaphore.Release();

          if (this.configuration.RemoveDiacriticsWhenUsingReplacementTalkBTalk)
          {
            translatedName = this.RemoveDiacritics(translatedName, this.SpecialCharsSupportedByGameFont);
          }

          nameNode->SetText(translatedName);
        }

        var parentNode = talkAddon->GetNodeById(10);

        this.talkOverlay.Semaphore.Wait();
        var translatedText = this.talkOverlay.CurrentText;
        bool shouldDisplay = this.talkOverlay.Display;
        this.talkOverlay.Semaphore.Release();

        if (!shouldDisplay)
        {
          return;
        }

        if (this.configuration.RemoveDiacriticsWhenUsingReplacementTalkBTalk)
        {
          translatedText = this.RemoveDiacritics(translatedText, this.SpecialCharsSupportedByGameFont);
        }

        textNode->TextFlags = (byte)(TextFlags.WordWrap | TextFlags.MultiLine | TextFlags.AutoAdjustNodeSize);
        textNode->FontSize = (byte)(translatedText.Length >= 350 ? 11 : (translatedText.Length >= 256 ? 12 : 14));
        textNode->SetWidth(parentNode->GetWidth());
        textNode->SetText(translatedText);
        textNode->ResizeNodeForCurrentText();
      }
      catch (Exception e)
      {
        PluginLog.Debug("TranslateTalkReplacing Exception: " + e);
      }
    }

    /// <summary>
    /// Translates the talk message using ImGui, either swapping texts or not based on configuration.
    /// </summary>
    /// <param name="nameToTranslate"></param>
    /// <param name="textToTranslate"></param>
    private unsafe void TranslateTalkUsingImGui(string nameToTranslate, string textToTranslate)
    {
      PluginLog.Debug($"TranslateTalkUsingImGui: {nameToTranslate}: {textToTranslate}");

      if (this.configuration.SwapTextsUsingImGui)
      {
        this.TranslateTalkUsingImGuiAndSwapping(nameToTranslate, textToTranslate);
      }
      else
      {
        this.TranslateTalkUsingImGuiWithoutSwapping(nameToTranslate, textToTranslate);
      }
    }

    /// <summary>
    /// Translates the talk message using ImGui and swaps the texts in the overlay.
    /// </summary>
    /// <param name="nameToTranslate"></param>
    /// <param name="textToTranslate"></param>
    private unsafe void TranslateTalkUsingImGuiAndSwapping(string nameToTranslate, string textToTranslate)
    {
      PluginLog.Debug($"TranslateTalkUsingImGuiAndSwapping: {nameToTranslate}: {textToTranslate}");

      Task.Run(() =>
      {
        try
        {
          TalkMessage talkMessage = this.FormatTalkMessage(nameToTranslate, textToTranslate);
          TalkMessage foundTalkMessage = this.FindAndReturnTalkMessage(talkMessage);

          if (foundTalkMessage != null)
          {
            this.UpdateTalkOverlay(
              foundTalkMessage.TranslatedSenderName,
              foundTalkMessage.TranslatedTalkMessage,
              foundTalkMessage.SenderName);
          }
          else
          {
            string textTranslation = this.Translate(textToTranslate);
            string nameTranslation = nameToTranslate.IsNullOrEmpty() ? string.Empty : this.Translate(nameToTranslate);

            this.UpdateTalkOverlay(nameTranslation, textTranslation, nameToTranslate);

            TalkMessage translatedTalkData = new TalkMessage(
              nameToTranslate,
              textToTranslate,
              ClientStateInterface.ClientLanguage.Humanize(),
              ClientStateInterface.ClientLanguage.Humanize(),
              nameTranslation,
              textTranslation,
              LangDict[LanguageInt].Code,
              this.configuration.ChosenTransEngine,
              rtlLangTranslationImageData: null,
              DateTime.Now,
              DateTime.Now);

            InsertTalkData(translatedTalkData);
          }

          this.StartOverlayTracking(
            "Talk",
            this.talkOverlay,
            () => this.configuration.UseImGuiForTalk && !string.IsNullOrWhiteSpace(this.talkOverlay.CurrentText),
            () => !this.configuration.UseImGuiForTalk || !this.talkOverlay.Display);
        }
        catch (Exception e)
        {
          PluginLog.Debug("TranslateTalkUsingImGuiAndSwapping Exception: " + e);
        }
      });
    }

    /// <summary>
    /// Translates the talk message using ImGui without swapping the texts in the overlay.
    /// </summary>
    /// <param name="nameToTranslate"></param>
    /// <param name="textToTranslate"></param>
    private unsafe void TranslateTalkUsingImGuiWithoutSwapping(string nameToTranslate, string textToTranslate)
    {
      PluginLog.Debug($"TranslateTalkUsingImGuiWithoutSwapping: {nameToTranslate}: {textToTranslate}");

      Task.Run(() =>
      {
        PluginLog.Debug("TranslateTalkUsingImGuiWithoutSwapping Task started");
        try
        {
          TalkMessage talkMessage = this.FormatTalkMessage(nameToTranslate, textToTranslate);
          TalkMessage foundTalkMessage = this.FindAndReturnTalkMessage(talkMessage);

          if (foundTalkMessage != null)
          {
            this.UpdateTalkOverlay(
              foundTalkMessage.TranslatedSenderName,
              foundTalkMessage.TranslatedTalkMessage,
              foundTalkMessage.SenderName);
          }
          else
          {
            string textTranslation = this.Translate(textToTranslate);
            string nameTranslation = nameToTranslate.IsNullOrEmpty() ? string.Empty : this.Translate(nameToTranslate);

            this.UpdateTalkOverlay(nameTranslation, textTranslation, nameToTranslate);

            TalkMessage translatedTalkData = new TalkMessage(
              nameToTranslate,
              textToTranslate,
              ClientStateInterface.ClientLanguage.Humanize(),
              ClientStateInterface.ClientLanguage.Humanize(),
              nameTranslation,
              textTranslation,
              LangDict[LanguageInt].Code,
              this.configuration.ChosenTransEngine,
              rtlLangTranslationImageData: null,
              DateTime.Now,
              DateTime.Now);

            InsertTalkData(translatedTalkData);
          }

          this.StartOverlayTracking(
            "Talk",
            this.talkOverlay,
            () => this.configuration.UseImGuiForTalk && !string.IsNullOrWhiteSpace(this.talkOverlay.CurrentText),
            () => !this.configuration.UseImGuiForTalk || !this.talkOverlay.Display);
        }
        catch (Exception e)
        {
          PluginLog.Debug("TranslateTalkUsingImGuiWithoutSwapping Exception: " + e);
        }
      });
    }

    /// <summary>
    /// Handles the UI Talk events asynchronously, translating the talk messages as needed.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="args"></param>
    private unsafe void UiTalkAsyncHandler(AddonEvent type, AddonArgs args)
    {
      PluginLog.Debug($"UiTalkAsyncHandler: {type} {args.AddonName}");

      if (!this.configuration.TranslateTalk)
      {
        return;
      }

      switch (type)
      {
        case AddonEvent.PreReceiveEvent:
          return;
        case AddonEvent.PreDraw:
          this.TranslateTalkReplacing();
          return;
      }

      if (args is not AddonRefreshArgs refreshArgs)
      {
        return;
      }

      var updateAtkValues = (AtkValue*)refreshArgs.AtkValues;
      if (updateAtkValues == null)
      {
        return;
      }

      try
      {
        string nameToTranslate = updateAtkValues[1].String != null ? MemoryHelper.ReadSeStringAsString(out _, (nint)updateAtkValues[1].String.Value) : string.Empty;
        string textToTranslate = MemoryHelper.ReadSeStringAsString(out _, (nint)updateAtkValues[0].String.Value);

        PluginLog.Debug($"Talk to translate: {nameToTranslate}: {textToTranslate}");

        if (this.configuration.UseImGuiForTalk)
        {
          this.TranslateTalkUsingImGui(nameToTranslate, textToTranslate);
        }
        else
        {
          this.TranslateTalk(nameToTranslate, textToTranslate);
        }
      }
      catch (Exception e)
      {
        PluginLog.Debug("UiTalkAsyncHandler Exception: " + e);
      }
    }
  }
}




/*
this handles setting the text to the addon talk in a better way by using AtkValues instead of node text

public void OnEnable()
{
    AddonLifecycle.RegisterListener(AddonEvent.PreRefresh, "Talk", OnPreRefresh);
}
public void OnDisable()
{
    AddonLifecycle.UnregisterListener(AddonEvent.PreRefresh, "Talk", OnPreRefresh);
}

private void OnPreRefresh(AddonEvent type, AddonArgs args)
{
    if (args is not AddonRefreshArgs refreshArgs || refreshArgs.AtkValueCount < 3)
        return;

    var values = new Span<AtkValue>((void*)refreshArgs.AtkValues, (int)refreshArgs.AtkValueCount); // you can probably use refreshArgs.AtkValueSpan directly here
    values.GetPointer(0)->SetManagedString("It's-a me, Mario!"); // Text
    values.GetPointer(1)->SetManagedString("Mario"); // Name
    values.GetPointer(3)->SetUInt(6); // Style
}
*/
