// <copyright file="UiTalkAsyncHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private void UpdateTalkOverlay(string translatedName, string translatedText, string originalName = "")
    {
      bool hasValidText = !string.IsNullOrWhiteSpace(translatedText);

      this.TalkOverlay.NameSemaphore.Wait();

      PluginLog.Debug(
        $"UpdateTalkOverlay: {translatedName}: {translatedText} - OriginalName: {originalName} - HasValidText: {hasValidText}"
      );

      if (!string.IsNullOrWhiteSpace(originalName))
      {
        this.TalkOverlay.OriginalName = originalName;
      }

      if (!string.IsNullOrWhiteSpace(translatedName))
      {
        this.TalkOverlay.CurrentName = translatedName;
      }

      this.TalkOverlay.NameSemaphore.Release();

      this.TalkOverlay.Semaphore.Wait();
      this.TalkOverlay.CurrentText = hasValidText ? translatedText : Resources.WaitingForTranslation;
      this.TalkOverlay.Display = hasValidText;
      this.TalkOverlay.Semaphore.Release();
    }

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
              foundTalkMessage.SenderName
            );
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
              langDict[languageInt].Code,
              this.configuration.ChosenTransEngine,
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

    private unsafe void TranslateTalkReplacing()
    {
      PluginLog.Debug("TranslateTalkReplacing");

      if (this.configuration.UseImGuiForTalk && !this.configuration.SwapTextsUsingImGui)
      {
        return;
      }

      try
      {
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
          this.TalkOverlay.NameSemaphore.Wait();
          var translatedName = this.TalkOverlay.CurrentName;
          this.TalkOverlay.NameSemaphore.Release();

          if (this.configuration.RemoveDiacriticsWhenUsingReplacementTalkBTalk)
          {
            translatedName = this.RemoveDiacritics(translatedName, this.SpecialCharsSupportedByGameFont);
          }

          nameNode->SetText(translatedName);
        }

        var parentNode = talkAddon->GetNodeById(10);

        this.TalkOverlay.Semaphore.Wait();
        var translatedText = this.TalkOverlay.CurrentText;
        bool shouldDisplay = this.TalkOverlay.Display;
        this.TalkOverlay.Semaphore.Release();

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
              foundTalkMessage.SenderName
            );
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
              langDict[languageInt].Code,
              this.configuration.ChosenTransEngine,
              DateTime.Now,
              DateTime.Now);

            InsertTalkData(translatedTalkData);
          }

          this.StartOverlayTracking(
            "Talk",
            this.TalkOverlay,
            () => this.configuration.UseImGuiForTalk && !string.IsNullOrWhiteSpace(this.TalkOverlay.CurrentText),
            () => !this.configuration.UseImGuiForTalk || !this.TalkOverlay.Display);

        }
        catch (Exception e)
        {
          PluginLog.Debug("TranslateTalkUsingImGuiAndSwapping Exception: " + e);
        }
      });
    }

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
              foundTalkMessage.SenderName
            );
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
              langDict[languageInt].Code,
              this.configuration.ChosenTransEngine,
              DateTime.Now,
              DateTime.Now);

            InsertTalkData(translatedTalkData);
          }

          this.StartOverlayTracking(
            "Talk",
            this.TalkOverlay,
            () => this.configuration.UseImGuiForTalk && !string.IsNullOrWhiteSpace(this.TalkOverlay.CurrentText),
            () => !this.configuration.UseImGuiForTalk || !this.TalkOverlay.Display);

        }
        catch (Exception e)
        {
          PluginLog.Debug("TranslateTalkUsingImGuiWithoutSwapping Exception: " + e);
        }
      });
    }

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
