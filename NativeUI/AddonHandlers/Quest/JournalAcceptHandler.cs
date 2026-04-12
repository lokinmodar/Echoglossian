// <copyright file="JournalAcceptHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Handles the JournalAccept quest addon runtime inside the standalone
///     quest-handler model.
/// </summary>
internal sealed class JournalAcceptHandler : QuestAddonHandlerBase
{
  private const string JournalAcceptAddonName = "JournalAccept";

  /// <summary>
  ///     Initializes a new instance of the <see cref="JournalAcceptHandler" /> class.
  /// </summary>
  /// <param name="dependencies">The shared quest-handler dependencies.</param>
  public JournalAcceptHandler(QuestAddonHandlerDependencies dependencies)
      : base(dependencies)
  {
    this.RegisterHandler(AddonEvent.PreSetup, this.OnJournalAcceptEvent);
  }

  /// <summary>
  ///     Gets whether the JournalAccept family should use hover tooltips.
  /// </summary>
  private bool JournalAcceptUsesHoverTooltips =>
      QuestAddonModeHelpers.UsesHoverTooltips(
          this.Config.JournalAcceptTranslationDisplayMode);

  /// <summary>
  ///     Gets whether the JournalAccept family should write translated text
  ///     into the native addon.
  /// </summary>
  private bool JournalAcceptWritesNativeTranslation =>
      QuestAddonModeHelpers.WritesNativeTranslation(
          this.Config.JournalAcceptTranslationDisplayMode);

  /// <summary>
  ///     Gets whether the JournalAccept family hover tooltips should show the
  ///     original text.
  /// </summary>
  private bool JournalAcceptHoverShowsOriginal =>
      QuestAddonModeHelpers.ShowsOriginalTooltips(
          this.Config.JournalAcceptTranslationDisplayMode);

  /// <summary>
  ///     Gets whether translated JournalAccept text should be normalized before
  ///     being written into the native UI.
  /// </summary>
  private bool JournalAcceptShouldRemoveDiacritics =>
      QuestAddonModeHelpers.ShouldRemoveDiacritics(
          this.Config.JournalAcceptTranslationDisplayMode,
          this.Config.RemoveDiacriticsWhenUsingReplacementQuest);

  /// <summary>
  ///     Handles JournalAccept setup events.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private unsafe void OnJournalAcceptEvent(AddonEvent type, AddonArgs args)
  {
#if DEBUG
    PluginLog.Debug($"JournalAcceptHandler AddonEvent: {type} {args.AddonName}");
#endif

    if (!this.Config.TranslateJournalAccept)
    {
      return;
    }

    if (args is not AddonSetupArgs setupArgs)
    {
      return;
    }

    var setupAtkValues = (AtkValue*)setupArgs.AtkValues;
    if (setupAtkValues == null)
    {
      return;
    }

    try
    {
      var questName = MemoryHelper.ReadSeStringAsString(
          out _,
          (nint)setupAtkValues[5].String.Value);
      var questMessage = MemoryHelper.ReadSeStringAsString(
          out _,
          (nint)setupAtkValues[12].String.Value);

#if DEBUG
      PluginLog.Debug(
          $"Language: {ClientStateInterface.ClientLanguage.Humanize()}");
      PluginLog.Debug($"Quest name: {questName}");
      PluginLog.Debug($"Quest message: {questMessage}");
#endif

      var questPlate = this.CreateQuestPlate(questName, questMessage);
      if (QuestProgressResolver.TryResolveQuestProgress(
              questPlate,
              out var resolvedAcceptSnapshot))
      {
        questPlate.SourceContentHash = resolvedAcceptSnapshot.ContentHash;
      }

      var foundQuestPlate = this.FindQuestPlate(questPlate);
      if (foundQuestPlate != null &&
          !string.Equals(
              foundQuestPlate.GameVersion,
              GetGameVersion(),
              StringComparison.Ordinal))
      {
        this.UpdateQuestPlateGameVersion(
            foundQuestPlate.Id,
            GetGameVersion());
      }

      var cacheKey = $"JournalAccept|{questName}|{questMessage}";

      if (QuestUiTranslationCache.TryGetAppliedSnapshot(
              questName,
              out var cachedNameSnapshot) &&
          QuestUiTranslationCache.TryGetAppliedSnapshot(
              questMessage,
              out var cachedMessageSnapshot))
      {
        if (this.JournalAcceptUsesHoverTooltips)
        {
          var addon = AtkStage.Instance()->RaptureAtkUnitManager
              ->GetAddonByName(JournalAcceptAddonName);
          this.RegisterTranslatedHoverTooltip(
              $"JournalAccept-{(nint)addon:X}",
              addon,
              $"{questName}\n{questMessage}",
              $"{cachedNameSnapshot.AppliedText}\n{cachedMessageSnapshot.AppliedText}",
              swapEnabled: this.JournalAcceptHoverShowsOriginal,
              forceEnabled: true,
              denseHitbox: true);
        }

        return;
      }

      string translatedQuestName;
      string translatedQuestMessage;

      if (foundQuestPlate == null)
      {
        if (this.TryGetQueuedTranslation(
                cacheKey,
                out var cachedTranslatedPayload) &&
            TryDeserializeTranslationPair(
                cachedTranslatedPayload,
                out translatedQuestName,
                out translatedQuestMessage))
        {
#if DEBUG
          PluginLog.Debug(
              $"Translated quest name: {translatedQuestName}");
          PluginLog.Debug(
              $"Translated quest message: {translatedQuestMessage}");
#endif
        }
        else
        {
          this.QueueTranslation(
              cacheKey,
              () => SerializeTranslationPair(
                  this.Translate(questName),
                  this.Translate(questMessage)),
              translatedPayload =>
              {
                if (!TryDeserializeTranslationPair(
                        translatedPayload,
                        out var resolvedQuestName,
                        out var resolvedQuestMessage))
                {
                  return;
                }

                var translatedQuestPlate = this.CreateTranslatedQuestPlate(
                    questName,
                    questMessage,
                    resolvedQuestName,
                    resolvedQuestMessage,
                    string.Empty);

                var result = this.InsertQuestPlate(translatedQuestPlate);
#if DEBUG
                PluginLog.Debug(
                    $"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
#endif
              });

          return;
        }
      }
      else
      {
        translatedQuestName = foundQuestPlate.TranslatedQuestName;
        translatedQuestMessage = foundQuestPlate.TranslatedQuestMessage;
#if DEBUG
        PluginLog.Debug(
            $"From database - Name: {translatedQuestName}, Message: {translatedQuestMessage}");
#endif
      }

#if DEBUG
      PluginLog.Debug(
          $"Using QuestPlate Replace - {translatedQuestName}: {translatedQuestMessage}");
#endif
      if (this.JournalAcceptShouldRemoveDiacritics)
      {
        translatedQuestName = this.NormalizeQuestText(
            translatedQuestName ?? string.Empty);
        translatedQuestMessage = this.NormalizeQuestText(
            translatedQuestMessage ?? string.Empty);
      }

      if (this.JournalAcceptWritesNativeTranslation)
      {
        setupAtkValues[5].SetManagedString(translatedQuestName);
        setupAtkValues[12].SetManagedString(translatedQuestMessage);
      }

      QuestUiTranslationCache.Remember(questName, translatedQuestName);
      QuestUiTranslationCache.Remember(
          questMessage,
          translatedQuestMessage);

      if (this.JournalAcceptUsesHoverTooltips)
      {
        var addon = AtkStage.Instance()->RaptureAtkUnitManager
            ->GetAddonByName(JournalAcceptAddonName);
        this.RegisterTranslatedHoverTooltip(
            $"JournalAccept-{(nint)addon:X}",
            addon,
            $"{questName}\n{questMessage}",
            $"{translatedQuestName}\n{translatedQuestMessage}",
            swapEnabled: this.JournalAcceptHoverShowsOriginal,
            forceEnabled: true,
            denseHitbox: true);
      }
    }
    catch (Exception e)
    {
      PluginLog.Error("Exception at JournalAcceptHandler: " + e);
    }
  }
}