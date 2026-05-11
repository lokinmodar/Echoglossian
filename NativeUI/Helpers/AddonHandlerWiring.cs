// <copyright file="AddonHandlerWiring.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

public partial class Echoglossian
{
  /// <summary>
  /// Handles the registration of addon handlers.
  /// </summary>
  private unsafe void EgloAddonHandler()
  {
    PluginRuntimeLog.Debug("Echoglossian", "EgloAddonHandler called.");

    this.registeredAddonHandlers =
        [
        ];

    if (this.configuration.TranslateOperationGuideWindow)
    {
      this.registeredAddonHandlers.Add(
          (AddonName: "OperationGuide",
              Handler: new OperationGuideHandler(
                  this.configuration,
                  this.hoverTooltipManager,
                  TranslationService)));
    }

    if (this.configuration.TranslateHudWindow)
    {
      this.registeredAddonHandlers.Add(
          (AddonName: "Hud",
              Handler: new HudWindowHandler(
                  this.configuration,
                  this.hoverTooltipManager,
                  TranslationService)));
      this.registeredAddonHandlers.Add(
          (AddonName: "Hud2",
              Handler: new Hud2WindowHandler(
                  this.configuration,
                  this.hoverTooltipManager,
                  TranslationService)));
    }

    if (this.configuration.TranslateGameMainMenu)
    {
      this.registeredAddonHandlers.Add(
          (AddonName: "AddonContextMenuTitle",
              Handler: new AddonContextMenuTitleHandler(
                  this.configuration,
                  this.hoverTooltipManager,
                  TranslationService)));
      this.registeredAddonHandlers.Add(
          (AddonName: "_MainCommand",
              Handler: new MainCommandHandler(
                  this.configuration,
                  this.hoverTooltipManager,
                  TranslationService)));
    }

    if (this.configuration.TranslateActionMenuWindow)
    {
      this.registeredAddonHandlers.Add(
          (AddonName: "ActionMenu",
              Handler: new ActionMenuWindowHandler(
                  this.configuration,
                  this.hoverTooltipManager,
                  TranslationService)));
    }

    if (this.configuration.TranslateCharacterWindow)
    {
      this.registeredAddonHandlers.Add(
          (AddonName: "Character",
              Handler: new CharacterWindowHandler(
                  this.configuration,
                  this.hoverTooltipManager,
                  TranslationService)));
      this.registeredAddonHandlers.Add(
          (AddonName: "CharacterClass",
              Handler: new CharacterClassSubWindowHandler(
                  this.configuration,
                  this.hoverTooltipManager,
                  TranslationService)));
      this.registeredAddonHandlers.Add(
          (AddonName: "CharacterRepute",
              Handler: new CharacterReputeSubWindowHandler(
                  this.configuration,
                  this.hoverTooltipManager,
                  TranslationService)));
      this.registeredAddonHandlers.Add(
          (AddonName: "CharacterProfile",
              Handler: new CharacterProfileSubWindowHandler(
                  this.configuration,
                  this.hoverTooltipManager,
                  TranslationService)));
      this.registeredAddonHandlers.Add(
          (AddonName: "CharacterStatus",
              Handler: new CharacterStatusSubWindowHandler(
                  this.configuration,
                  this.hoverTooltipManager,
                  TranslationService)));
    }

    if (this.configuration.TranslateTalk)
    {
      this.registeredAddonHandlers.Add(
          (AddonName: "Talk",
              Handler: new TalkHandler(
                  this.configuration,
                  TranslationService,
                  this.FindAndReturnTalkMessage,
                  InsertTalkData,
                  (translatedName, translatedText, originalName) =>
                      this.UpdateOverlayContent(
                          this.talkOverlay,
                          translatedName,
                          translatedText,
                          originalName),
                  () => this.ClearOverlay(this.talkOverlay, clearText: true),
                  text => this.RemoveDiacritics(
                      text,
                      this.SpecialCharsSupportedByGameFont))));
    }

    if (this.configuration.TranslateBattleTalk)
    {
      this.registeredAddonHandlers.Add(
          (AddonName: "_BattleTalk",
              Handler: new BattleTalkHandler(
                  this.configuration,
                  TranslationService,
                  this.FindAndReturnBattleTalkMessage,
                  battleTalkMessage => Task.FromResult(
                      InsertBattleTalkData(battleTalkMessage)),
                  (translatedName, translatedText, originalName) =>
                      this.UpdateOverlayContent(
                          this.battleTalkOverlay,
                          translatedName,
                          translatedText,
                          originalName),
                  () => this.ClearOverlay(
                      this.battleTalkOverlay,
                      clearText: true),
                  text => this.RemoveDiacritics(
                      text,
                      this.SpecialCharsSupportedByGameFont))));
    }

    if (this.configuration.TranslateTalkSubtitle)
    {
      this.registeredAddonHandlers.Add(
          (AddonName: "TalkSubtitle",
              Handler: new TalkSubtitleHandler(
                  this.configuration,
                  TranslationService,
                  this.FindAndReturnTalkSubtitleMessage,
                  talkSubtitleMessage => Task.Run(
                      () => InsertTalkSubtitleData(talkSubtitleMessage)),
                  (translatedName, translatedText, originalName) =>
                      this.UpdateOverlayContent(
                          this.talkSubtitleOverlay,
                          translatedName,
                          translatedText,
                          originalName),
                  () => this.ClearOverlay(
                      this.talkSubtitleOverlay,
                      clearText: true),
                  text => this.RemoveDiacritics(
                      text,
                      this.SpecialCharsSupportedByGameFont))));
    }

    if (this.configuration.TranslateMiniTalk)
    {
      this.registeredAddonHandlers.Add(
          (AddonName: "_MiniTalk",
               Handler: new MiniTalkHandler(
                  this.configuration,
                  TranslationService,
                  this.FindAndReturnMiniTalkMessage,
                  miniTalkMessage => Task.Run(
                      () => InsertMiniTalkData(miniTalkMessage)),
                  (bubbleKey, translatedName, translatedText, originalName) =>
                      this.UpdateMiniTalkBubbleOverlayContent(
                          bubbleKey,
                          translatedName,
                          translatedText,
                          originalName),
                  (bubbleKey, clearText) => this.ClearMiniTalkBubbleOverlay(
                      bubbleKey,
                      clearText),
                  this.SyncMiniTalkBubbleOverlayBounds,
                  AddonTextNodeResolvers.ResolveMiniTalkBubbleTextNodes,
                  text => this.RemoveDiacritics(
                      text,
                      this.SpecialCharsSupportedByGameFont))));
    }

    if (this.configuration.TranslateCutSceneSelectString)
    {
      PluginRuntimeLog.Debug(
          "Echoglossian",
          "Registering CutSceneSelectString handler " +
          $"overlay={this.configuration.UseImGuiForCutSceneSelectString} " +
          $"swap={this.configuration.SwapTextsUsingImGui}");
      this.registeredAddonHandlers.Add(
          (AddonName: "CutSceneSelectString",
              Handler: new CutSceneSelectStringHandler(
                  this.configuration,
                  TranslationService,
                  this.FindAndReturnCutSceneSelectStringMessage,
                  selectString => Task.Run(
                      () => InsertCutSceneSelectStringData(selectString)),
                  (translatedQuestion, translatedOptions, originalQuestion) =>
                      this.UpdateOverlayContent(
                          this.cutSceneSelectStringOverlay,
                          translatedQuestion,
                          translatedOptions,
                          originalQuestion),
                  () => this.ClearOverlay(
                      this.cutSceneSelectStringOverlay,
                      clearText: true),
                  addon => this.UpdateOverlayBounds(
                      this.cutSceneSelectStringOverlay,
                      addon),
                  text => this.RemoveDiacritics(
                      text,
                      this.SpecialCharsSupportedByGameFont))));
      PluginRuntimeLog.Debug("Echoglossian", "CutSceneSelectString handler registered");
    }

    var questAddonDependencies = this.CreateQuestAddonHandlerDependencies();

    var journalHandler = new JournalHandler(questAddonDependencies);
    var journalDetailHandler = new JournalDetailHandler(questAddonDependencies);

    this.registeredAddonHandlers.Add(
        (AddonName: "Journal",
            Handler: journalHandler));
    this.registeredAddonHandlers.Add(
        (AddonName: "Journal",
            Handler: journalDetailHandler));
    this.registeredAddonHandlers.Add(
        (AddonName: "JournalDetail",
            Handler: journalDetailHandler));

    // Quest-family stabilization pass:
    // keep Journal / JournalDetail / ToDoList / ScenarioTree active while each
    // quest addon is isolated onto the canonical DB-first runtime. The other
    // quest handlers remain in the repo but are intentionally not registered
    // for now.
    if (this.configuration.TranslateToDoList)
    {
      this.registeredAddonHandlers.Add(
          (AddonName: "_ToDoList",
              Handler: new ToDoListHandler(questAddonDependencies)));
    }

    if (this.configuration.TranslateScenarioTree)
    {
      this.registeredAddonHandlers.Add(
          (AddonName: "ScenarioTree",
              Handler: new ScenarioTreeHandler(questAddonDependencies)));
    }

    if (this.configuration.TranslateToast &&
        this.configuration.TranslateWideTextToast)
    {
      this.registeredAddonHandlers.Add(
          (AddonName: "_WideText",
              Handler: new WideTextToastHandler(
                  this.configuration,
                  TranslationService,
                  this.FindAndReturnToastMessage,
                  toastMessage => Task.Run(
                      () => this.InsertToastMessageData(toastMessage)),
                  (translatedName, translatedText, originalName) =>
                      this.UpdateOverlayContent(
                          this.toastOverlay,
                          translatedName,
                          translatedText,
                          originalName),
                  () => this.ClearOverlay(this.toastOverlay, clearText: true),
                  this.SyncWideTextToastOverlayBounds,
                  text => this.RemoveDiacritics(
                      text,
                      this.SpecialCharsSupportedByGameFont))));
    }

    if (this.configuration.TranslateToast &&
        this.configuration.TranslateErrorToast)
    {
      this.registeredAddonHandlers.Add(
          (AddonName: "_TextError",
              Handler: new ErrorToastHandler(
                  this.configuration,
                  TranslationService,
                  this.FindAndReturnToastMessage,
                  toastMessage => Task.Run(
                      () => this.InsertToastMessageData(toastMessage)),
                  (translatedName, translatedText, originalName) =>
                      this.UpdateOverlayContent(
                          this.errorToastOverlay,
                          translatedName,
                          translatedText,
                          originalName),
                  () => this.ClearOverlay(
                      this.errorToastOverlay,
                      clearText: true),
                  this.SyncErrorToastOverlayBounds,
                  text => this.RemoveDiacritics(
                      text,
                      this.SpecialCharsSupportedByGameFont))));
    }

    if (this.configuration.TranslateToast &&
        this.configuration.TranslateAreaToast)
    {
      this.registeredAddonHandlers.Add(
          (AddonName: "_AreaText",
              Handler: new AreaToastHandler(
                  this.configuration,
                  TranslationService,
                  this.FindAndReturnToastMessage,
                  toastMessage => Task.Run(
                      () => this.InsertToastMessageData(toastMessage)),
                  (translatedName, translatedText, originalName) =>
                      this.UpdateOverlayContent(
                          this.areaToastOverlay,
                          translatedName,
                          translatedText,
                          originalName),
                  () => this.ClearOverlay(
                      this.areaToastOverlay,
                      clearText: true),
                  this.SyncAreaToastOverlayBounds,
                  text => this.RemoveDiacritics(
                      text,
                      this.SpecialCharsSupportedByGameFont))));
    }

    if (this.configuration.TranslateToast &&
        this.configuration.TranslateClassChangeToast)
    {
      this.registeredAddonHandlers.Add(
          (AddonName: "_TextClassChange",
              Handler: new ClassChangeToastHandler(
                  this.configuration,
                  TranslationService,
                  this.FindAndReturnToastMessage,
                  toastMessage => Task.Run(
                      () => this.InsertToastMessageData(toastMessage)),
                  (translatedName, translatedText, originalName) =>
                      this.UpdateOverlayContent(
                          this.classChangeToastOverlay,
                          translatedName,
                          translatedText,
                          originalName),
                  () => this.ClearOverlay(
                      this.classChangeToastOverlay,
                      clearText: true),
                  this.SyncClassChangeToastOverlayBounds,
                  text => this.RemoveDiacritics(
                      text,
                      this.SpecialCharsSupportedByGameFont))));
    }

    if (this.configuration.TranslateTextGimmickHint)
    {
      this.registeredAddonHandlers.Add(
          (AddonName: "_TextGimmickHint",
              Handler: new TextGimmickHintHandler(
                  this.configuration,
                  TranslationService,
                  this.FindAndReturnTextGimmickHintMessage,
                  textGimmickHintMessage => InsertTextGimmickHintData(
                      textGimmickHintMessage),
                  (translatedName, translatedText, originalName) =>
                      this.UpdateOverlayContent(
                          this.textGimmickHintOverlay,
                          translatedName,
                          translatedText,
                          originalName),
                  () => this.ClearOverlay(
                      this.textGimmickHintOverlay,
                      clearText: true),
                  this.SyncTextGimmickHintToastOverlayBounds,
                  text => this.RemoveDiacritics(
                      text,
                      this.SpecialCharsSupportedByGameFont))));
    }

    AddonHandlerRegistrar.RegisterMany(
        this.registeredAddonHandlers,
        AddonLifecycle);

    /*"PreSetup","PostSetup", "PreUpdate", "PostUpdate", "PreDraw", "PostDraw", "PreFinalize", "PreReceiveEvent", "PostReceiveEvent", "PreRequestedUpdate", "PostRequestedUpdate", "PreRefresh", "PostRefresh" */

    // tracking addon lifecycle for debug
    AddonEvent[] lifecycleLogEventsWithoutUpdatesAndDraws =
    [
      AddonEvent.PreSetup,
      AddonEvent.PreFinalize,
      AddonEvent.PreRequestedUpdate,
      AddonEvent.PreRefresh,
      AddonEvent.PreReceiveEvent,
      AddonEvent.PreOpen,
      AddonEvent.PreClose,
      AddonEvent.PreShow,
      AddonEvent.PreHide,
      AddonEvent.PreMove,
      AddonEvent.PreMouseOver,
      AddonEvent.PreMouseOut,
      AddonEvent.PreFocus,
      AddonEvent.PostSetup,
      AddonEvent.PostRequestedUpdate,
      AddonEvent.PostRefresh,
      AddonEvent.PostReceiveEvent,
      AddonEvent.PostOpen,
      AddonEvent.PostClose,
      AddonEvent.PostShow,
      AddonEvent.PostHide,
      AddonEvent.PostMove,
      AddonEvent.PostMouseOver,
      AddonEvent.PostMouseOut,
      AddonEvent.PostFocus,
    ];

    AddonEvent[] lifecycleLogEventsWithoutUpdates =
    [
      AddonEvent.PreSetup,
      AddonEvent.PreDraw,
      AddonEvent.PreFinalize,
      AddonEvent.PreRequestedUpdate,
      AddonEvent.PreRefresh,
      AddonEvent.PreReceiveEvent,
      AddonEvent.PreOpen,
      AddonEvent.PreClose,
      AddonEvent.PreShow,
      AddonEvent.PreHide,
      AddonEvent.PreMove,
      AddonEvent.PreMouseOver,
      AddonEvent.PreMouseOut,
      AddonEvent.PreFocus,
      AddonEvent.PostSetup,
      AddonEvent.PostDraw,
      AddonEvent.PostRequestedUpdate,
      AddonEvent.PostRefresh,
      AddonEvent.PostReceiveEvent,
      AddonEvent.PostOpen,
      AddonEvent.PostClose,
      AddonEvent.PostShow,
      AddonEvent.PostHide,
      AddonEvent.PostMove,
      AddonEvent.PostMouseOver,
      AddonEvent.PostMouseOut,
      AddonEvent.PostFocus,
    ];

    // AddonLifecycleExtensions.LogAddon(
    //     AddonLifecycle,
    //     "Talk",
    //     lifecycleLogEventsWithoutUpdatesAndDraws);
      // AddonLifecycleExtensions.LogAddon(
      //   AddonLifecycle,
      //   "_BattleTalk",
      //   lifecycleLogEventsWithoutUpdatesAndDraws);

  }
}


