// <copyright file="ScenarioTreeHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Handles the ScenarioTree quest addon runtime inside the standalone
///     quest-handler model.
/// </summary>
internal sealed class ScenarioTreeHandler : QuestAddonHandlerBase
{
  private const string ScenarioTreeAddonName = "ScenarioTree";

  private const string ScenarioTreeHoverPrefix = "ScenarioTree-";

  private readonly Dictionary<int, ScenarioTreeHoverEntry> scenarioTreeHoverEntries = [];

  /// <summary>
  ///     Initializes a new instance of the <see cref="ScenarioTreeHandler" /> class.
  /// </summary>
  /// <param name="dependencies">The shared quest-handler dependencies.</param>
  public ScenarioTreeHandler(QuestAddonHandlerDependencies dependencies)
      : base(dependencies)
  {
    this.RegisterHandler(AddonEvent.PreRefresh, this.OnScenarioTreeEvent);
    this.RegisterHandler(AddonEvent.PreRequestedUpdate, this.OnScenarioTreeEvent);
    this.RegisterHandler(
        AddonEvent.PreDraw,
        this.OnScenarioTreeHoverRefreshEvent);
    this.RegisterHandler(AddonEvent.PreHide, this.OnScenarioTreeCleanupEvent);
    this.RegisterHandler(
        AddonEvent.PreFinalize,
        this.OnScenarioTreeCleanupEvent);
  }

  /// <summary>
  ///     Gets whether the ScenarioTree family should use hover tooltips.
  /// </summary>
  private bool ScenarioTreeUsesHoverTooltips =>
      QuestAddonModeHelpers.UsesHoverTooltips(
          this.Config.ScenarioTreeTranslationDisplayMode);

  /// <summary>
  ///     Gets whether the ScenarioTree family should write translated text into
  ///     the native addon.
  /// </summary>
  private bool ScenarioTreeWritesNativeTranslation =>
      QuestAddonModeHelpers.WritesNativeTranslation(
          this.Config.ScenarioTreeTranslationDisplayMode);

  /// <summary>
  ///     Gets whether the ScenarioTree family hover tooltips should show the
  ///     original text.
  /// </summary>
  private bool ScenarioTreeHoverShowsOriginal =>
      QuestAddonModeHelpers.ShowsOriginalTooltips(
          this.Config.ScenarioTreeTranslationDisplayMode);

  /// <summary>
  ///     Gets whether translated ScenarioTree text should be normalized before
  ///     being written into the native UI.
  /// </summary>
  private bool ScenarioTreeShouldRemoveDiacritics =>
      QuestAddonModeHelpers.ShouldRemoveDiacritics(
          this.Config.ScenarioTreeTranslationDisplayMode,
          this.Config.RemoveDiacriticsWhenUsingReplacementQuest);

  /// <summary>
  ///     Translates a single ScenarioTree quest entry.
  /// </summary>
  /// <param name="setupAtkValues">The addon payload.</param>
  /// <param name="valueIndex">The quest-name value index to translate.</param>
  private unsafe void TranslateQuestOnScenarioTree(
      AtkValue* setupAtkValues,
      int valueIndex)
  {
    if (setupAtkValues[valueIndex].Type != ValueType.String ||
        setupAtkValues[valueIndex].String == null)
    {
      return;
    }

    var questNameText = MemoryHelper.ReadSeStringAsString(
        out _,
        (nint)setupAtkValues[valueIndex].String.Value);
    if (questNameText == null || questNameText.Length == 0)
    {
      return;
    }

    QuestTodoProgressSnapshot? questTodoProgressSnapshot = null;
    if (QuestTodoProgressResolver.TryResolveQuestTodoProgress(
            questNameText,
            out var resolvedTodoProgressSnapshot))
    {
      questTodoProgressSnapshot = resolvedTodoProgressSnapshot;
    }

    var questTodoProgressKey = questTodoProgressSnapshot?.CacheKey ?? questNameText;

    if (QuestUiTranslationCache.TryGetAppliedSnapshot(
            questTodoProgressKey + "|" + questNameText,
            out var cachedScenarioSnap))
    {
      this.RememberScenarioTreeHoverEntry(
          valueIndex,
          questTodoProgressKey,
          questNameText,
          cachedScenarioSnap.AppliedText);

      if (this.ScenarioTreeUsesHoverTooltips)
      {
        var addon = AtkStage.Instance()->RaptureAtkUnitManager
            ->GetAddonByName(ScenarioTreeAddonName);
        this.RegisterTranslatedHoverTooltip(
            $"ScenarioTree-{(nint)addon:X}-{valueIndex}-{questTodoProgressKey}",
            addon,
            questNameText,
            cachedScenarioSnap.AppliedText,
            swapEnabled: this.ScenarioTreeHoverShowsOriginal,
            forceEnabled: true,
            denseHitbox: true);
      }

      return;
    }

    var questPlate = this.CreateQuestPlate(questNameText, string.Empty);
    var foundQuestPlate = this.FindQuestPlateByName(questPlate);
    var cacheKey =
        $"ScenarioTree|{valueIndex}|{questTodoProgressKey}|{questNameText}";
    if (foundQuestPlate != null)
    {
#if DEBUG
      // PluginLog.Debug(
      //     $"Name from database: {questNameText} -> {foundQuestPlate.TranslatedQuestName}");
#endif
      var translatedQuestName = foundQuestPlate.TranslatedQuestName;

      if (this.ScenarioTreeShouldRemoveDiacritics)
      {
        translatedQuestName = this.NormalizeQuestText(
            translatedQuestName ?? string.Empty);
      }

      this.RememberScenarioTreeHoverEntry(
          valueIndex,
          questTodoProgressKey,
          questNameText,
          translatedQuestName);

      if (this.ScenarioTreeWritesNativeTranslation)
      {
        setupAtkValues[valueIndex].SetManagedString(
            translatedQuestName);
      }

      QuestUiTranslationCache.Remember(
          questTodoProgressKey + "|" + questNameText,
          translatedQuestName);

      if (this.ScenarioTreeUsesHoverTooltips)
      {
        var addon = AtkStage.Instance()->RaptureAtkUnitManager
            ->GetAddonByName(ScenarioTreeAddonName);
        this.RegisterTranslatedHoverTooltip(
            $"ScenarioTree-{(nint)addon:X}-{valueIndex}-{questTodoProgressKey}",
            addon,
            questNameText,
            translatedQuestName,
            swapEnabled: this.ScenarioTreeHoverShowsOriginal,
            forceEnabled: true,
            denseHitbox: true);
      }

      return;
    }

    if (this.TryGetQueuedTranslation(cacheKey, out var cachedTranslatedName))
    {
      var translatedNameText = cachedTranslatedName;
#if DEBUG
      // PluginLog.Debug(
      //     $"Name translated: {questNameText} -> {translatedNameText}");
#endif
      if (this.ScenarioTreeShouldRemoveDiacritics)
      {
        translatedNameText = this.NormalizeQuestText(
            translatedNameText ?? string.Empty);
      }

      this.RememberScenarioTreeHoverEntry(
          valueIndex,
          questTodoProgressKey,
          questNameText,
          translatedNameText);

      if (this.ScenarioTreeWritesNativeTranslation)
      {
        setupAtkValues[valueIndex].SetManagedString(
            translatedNameText);
      }

      QuestUiTranslationCache.Remember(
          questTodoProgressKey + "|" + questNameText,
          translatedNameText);

      if (this.ScenarioTreeUsesHoverTooltips)
      {
        var addon = AtkStage.Instance()->RaptureAtkUnitManager
            ->GetAddonByName(ScenarioTreeAddonName);
        this.RegisterTranslatedHoverTooltip(
            $"ScenarioTree-{(nint)addon:X}-{valueIndex}-{questTodoProgressKey}",
            addon,
            questNameText,
            translatedNameText,
            swapEnabled: this.ScenarioTreeHoverShowsOriginal,
            forceEnabled: true,
            denseHitbox: true);
      }

      return;
    }

    this.RememberScenarioTreeHoverEntry(
        valueIndex,
        questTodoProgressKey,
        questNameText,
        questNameText);

    this.QueueTranslation(
        cacheKey,
        () => this.Translate(questNameText),
        translatedNameText =>
        {
          var translatedQuestPlate = this.CreateTranslatedQuestPlate(
              questNameText,
              string.Empty,
              translatedNameText,
              string.Empty,
              string.Empty);

          var result = this.InsertQuestPlate(translatedQuestPlate);
#if DEBUG
          // PluginLog.Debug(
          //     $"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
#endif
        });
  }

  /// <summary>
  ///     Handles ScenarioTree refresh and requested-update events.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private unsafe void OnScenarioTreeEvent(AddonEvent type, AddonArgs args)
  {
#if DEBUG
    // PluginLog.Debug($"UiScenarioTreeHandler AddonEvent: {type} {args.AddonName}");
#endif

    if (!this.Config.TranslateScenarioTree)
    {
      return;
    }

    if (!this.TryResolveScenarioTreeAtkValues(args, out var setupAtkValues))
    {
      return;
    }

    try
    {
      // Translate MSQ
      this.TranslateQuestOnScenarioTree(setupAtkValues, 7);

      // Translate SubQuest
      this.TranslateQuestOnScenarioTree(setupAtkValues, 2);
    }
    catch (Exception e)
    {
      PluginLog.Error(
          "Exception at UiScenarioTreeHandler: " + e.StackTrace);
    }
  }

  /// <summary>
  ///     Resolves the live ScenarioTree ATK value array for refresh and
  ///     requested-update events.
  /// </summary>
  /// <param name="args">The lifecycle arguments for the current event.</param>
  /// <param name="atkValues">The resolved ATK value pointer.</param>
  /// <returns>True when a usable ATK value array was found.</returns>
  private unsafe bool TryResolveScenarioTreeAtkValues(
      AddonArgs args,
      out AtkValue* atkValues)
  {
    atkValues = null;

    if (!string.Equals(args.AddonName, ScenarioTreeAddonName, StringComparison.Ordinal))
    {
      return false;
    }

    if (args is AddonRefreshArgs refreshArgs)
    {
      atkValues = (AtkValue*)refreshArgs.AtkValues;
      return atkValues != null;
    }

    var addon = AtkStage.Instance()->RaptureAtkUnitManager
        ->GetAddonByName(ScenarioTreeAddonName);
    if (addon == null || !addon->IsVisible || addon->AtkValues == null)
    {
      return false;
    }

    atkValues = addon->AtkValues;
    return true;
  }

  /// <summary>
  ///     Refreshes the ScenarioTree hover target every draw using the most
  ///     recently resolved quest names without queueing new translations.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private unsafe void OnScenarioTreeHoverRefreshEvent(
      AddonEvent type,
      AddonArgs args)
  {
    if (!this.Config.TranslateScenarioTree || !this.ScenarioTreeUsesHoverTooltips)
    {
      return;
    }

    if (this.scenarioTreeHoverEntries.Count == 0)
    {
      return;
    }

    var addon = AtkStage.Instance()->RaptureAtkUnitManager
        ->GetAddonByName(ScenarioTreeAddonName);
    if (addon == null || !addon->IsVisible)
    {
      return;
    }

    var orderedEntries = this.scenarioTreeHoverEntries
        .OrderByDescending(entry => entry.Key)
        .Select(entry => entry.Value)
        .ToList();
    var originalText = string.Join(
        $"{Environment.NewLine}{Environment.NewLine}",
        orderedEntries
            .Select(entry => entry.OriginalText)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.Ordinal));
    var translatedText = string.Join(
        $"{Environment.NewLine}{Environment.NewLine}",
        orderedEntries
            .Select(entry => entry.TranslatedText)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.Ordinal));
    if (string.IsNullOrWhiteSpace(originalText) &&
        string.IsNullOrWhiteSpace(translatedText))
    {
      return;
    }

    this.RegisterTranslatedHoverTooltip(
        $"ScenarioTree-{(nint)addon:X}",
        addon,
        originalText,
        translatedText,
        swapEnabled: this.ScenarioTreeHoverShowsOriginal,
        forceEnabled: true,
        denseHitbox: true);
  }

  /// <summary>
  ///     Remembers the latest ScenarioTree hover payload for one visible quest
  ///     slot so the combined tooltip can be refreshed on draw.
  /// </summary>
  /// <param name="valueIndex">The addon value index that produced the text.</param>
  /// <param name="progressKey">The resolved quest-progress key.</param>
  /// <param name="originalText">The current original quest name.</param>
  /// <param name="translatedText">The current translated quest name.</param>
  private void RememberScenarioTreeHoverEntry(
      int valueIndex,
      string progressKey,
      string originalText,
      string translatedText)
  {
    this.scenarioTreeHoverEntries[valueIndex] = new ScenarioTreeHoverEntry(
        progressKey,
        originalText ?? string.Empty,
        translatedText ?? string.Empty);
  }

  /// <summary>
  ///     Clears ScenarioTree hover registrations when the addon closes.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private void OnScenarioTreeCleanupEvent(AddonEvent type, AddonArgs args)
  {
    if (string.Equals(args.AddonName, ScenarioTreeAddonName, StringComparison.Ordinal))
    {
      this.scenarioTreeHoverEntries.Clear();
      this.RemoveHoverTooltipsByPrefix(ScenarioTreeHoverPrefix);
    }
  }

  /// <summary>
  ///     Captures the latest hover payload for a visible ScenarioTree quest
  ///     slot.
  /// </summary>
  /// <param name="ProgressKey">The stable quest-progress key.</param>
  /// <param name="OriginalText">The original quest name.</param>
  /// <param name="TranslatedText">The translated quest name.</param>
  private sealed record ScenarioTreeHoverEntry(
      string ProgressKey,
      string OriginalText,
      string TranslatedText);
}
