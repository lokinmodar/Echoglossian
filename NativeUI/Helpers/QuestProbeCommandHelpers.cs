// <copyright file="QuestProbeCommandHelpers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;

using Echoglossian.EFCoreSqlite.Models.Journal;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

namespace Echoglossian;

#if DEBUG
/// <summary>
///     Handles the quest probe command used to inspect the complete quest data
///     shape from Lumina, the live quest progress resolver, and the current
///     QuestPlate database rows.
/// </summary>
public partial class Echoglossian
{
  /// <summary>
  ///     Handles the quest probe command.
  /// </summary>
  /// <param name="command">Command name.</param>
  /// <param name="args">Command arguments.</param>
  private void OnEgloQuestProbeCommand(string command, string args)
  {
    var trimmedArgs = args.Trim();
    if (trimmedArgs.Length == 0)
    {
      ChatGuiInterface.Print(Resources.QuestProbeHelpMessage);
      return;
    }

    if (!this.TryResolveQuestProbeTarget(
            trimmedArgs,
            out var questIdText,
            out var questName,
            out var questRow))
    {
      ChatGuiInterface.Print(
          $"Quest probe could not resolve '{trimmedArgs}'. Check the Dalamud log for details.");
      return;
    }

    this.LogQuestProbe(questIdText, questName, questRow);
  }

  /// <summary>
  ///     Tries to resolve the quest probe target from either a quest id or a
  ///     quest name.
  /// </summary>
  /// <param name="input">The command argument.</param>
  /// <param name="questIdText">The resolved quest id as text.</param>
  /// <param name="questName">The resolved quest name.</param>
  /// <param name="questRow">The resolved Lumina quest row.</param>
  /// <returns>True when the quest could be resolved.</returns>
  private bool TryResolveQuestProbeTarget(
      string input,
      out string questIdText,
      out string questName,
      out Quest questRow)
  {
    questIdText = string.Empty;
    questName = string.Empty;
    questRow = default;

    var resolvedQuestId = input;
    if (!uint.TryParse(
            input,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsedQuestId))
    {
      if (!QuestLuminaResolver.TryResolveQuestId(input, out resolvedQuestId) ||
          !uint.TryParse(
              resolvedQuestId,
              NumberStyles.Integer,
              CultureInfo.InvariantCulture,
              out parsedQuestId))
      {
        return false;
      }
    }

    var dataManager = DManager;
    if (dataManager == null)
    {
      return false;
    }

    var questSheet =
        dataManager.GetExcelSheet<Quest>(ClientStateInterface.ClientLanguage);
    if (questSheet == null || !questSheet.TryGetRow(parsedQuestId, out var row))
    {
      return false;
    }

    questIdText = parsedQuestId.ToString(CultureInfo.InvariantCulture);
    questName = this.GetQuestName(row) ?? input;
    questRow = row;
    return true;
  }

  /// <summary>
  ///     Logs the quest data structure for inspection.
  /// </summary>
  /// <param name="questIdText">The quest id as text.</param>
  /// <param name="questName">The quest name.</param>
  /// <param name="questRow">The resolved Lumina quest row.</param>
  private void LogQuestProbe(
      string questIdText,
      string questName,
      Quest questRow)
  {
    PluginRuntimeLog.Information(
        $"[QuestProbe] start questId={questIdText} questName='{questName}' language={ClientStateInterface.ClientLanguage.Humanize()} gameVersion={GetGameVersion()}");

    this.LogQuestRow(questRow);
    this.LogQuestTextSheet(questRow);
    this.LogQuestPlateRows(questIdText, questName);

    if (QuestProgressResolver.TryResolveQuestProgress(
            questIdText,
            out var questProgressSnapshot))
    {
      this.LogQuestProgressSnapshot(questProgressSnapshot);
    }
    else
    {
      PluginRuntimeLog.Information(
          $"[QuestProbe] progress snapshot unavailable questId={questIdText}");
    }

    if (QuestTodoProgressResolver.TryResolveQuestTodoProgress(
            questIdText,
            out var questTodoProgressSnapshot))
    {
      PluginRuntimeLog.Information(
          $"[QuestProbe] todo progress questId={questIdText} sequence={questTodoProgressSnapshot.QuestProgress.QuestSequence} objectiveProgress={questTodoProgressSnapshot.ObjectiveProgress} objectiveCount={questTodoProgressSnapshot.ObjectiveCount} questCount={questTodoProgressSnapshot.QuestCount}");
    }
  }

  /// <summary>
  ///     Logs the raw Lumina quest row properties.
  /// </summary>
  /// <param name="questRow">The Lumina quest row.</param>
  private void LogQuestRow(Quest questRow)
  {
    var questType = questRow.GetType();
    var properties = questType.GetProperties(
        BindingFlags.Instance | BindingFlags.Public);

    PluginRuntimeLog.Information(
        $"[QuestProbe] quest row type={questType.Name} propertyCount={properties.Length}");

    foreach (var property in properties)
    {
      if (property.GetIndexParameters().Length != 0)
      {
        continue;
      }

      object? value;
      try
      {
        value = property.GetValue(questRow);
      }
      catch (Exception ex)
      {
        PluginRuntimeLog.Debug(
            $"[QuestProbe] quest property {property.Name} read failed: {ex.Message}");
        continue;
      }

      PluginRuntimeLog.Debug(
          $"[QuestProbe] quest.{property.Name}={this.FormatProbeValue(value)}");
    }
  }

  /// <summary>
  ///     Logs all rows from the quest text sheet, not just the live todo
  ///     entries, so we can inspect the full structure of the quest sheet.
  /// </summary>
  /// <param name="questRow">The Lumina quest row.</param>
  private void LogQuestTextSheet(Quest questRow)
  {
    var questSheetName = this.BuildQuestTextSheetName(questRow);
    if (questSheetName.Length == 0)
    {
      PluginRuntimeLog.Information(
          $"[QuestProbe] quest text sheet name unavailable questRowId={this.GetQuestRowIdText(questRow)} questSheetId='{this.GetQuestSheetIdText(questRow)}'");
      return;
    }

    var questTextSheet = DManager?.GameData.GetExcelSheet<RawRow>(name: questSheetName);
    if (questTextSheet == null)
    {
      PluginRuntimeLog.Information(
          $"[QuestProbe] quest text sheet not found questSheet='{questSheetName}'");
      return;
    }

    var rowCount = Convert.ToInt32(questTextSheet.Count, CultureInfo.InvariantCulture);
    PluginRuntimeLog.Information(
        $"[QuestProbe] quest text sheet='{questSheetName}' rowCount={rowCount}");

    for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
    {
      var row = questTextSheet.GetRow((uint)rowIndex);
      ReadOnlySeString rawKey = row.ReadStringColumn(0);
      ReadOnlySeString rawValue = row.ReadStringColumn(1);
      var keyText = this.EvaluateQuestProbeText(rawKey);
      var valueText = this.EvaluateQuestProbeText(rawValue);

      PluginRuntimeLog.Debug(
          $"[QuestProbe] sheet[{rowIndex}] keyText='{keyText}' valueText='{valueText}' rawKey='{rawKey.ExtractText()}' rawValue='{rawValue.ExtractText()}'");
    }
  }

  /// <summary>
  ///     Logs the QuestPlate rows currently stored for the quest.
  /// </summary>
  /// <param name="questIdText">The quest id text.</param>
  /// <param name="questName">The quest name.</param>
  private void LogQuestPlateRows(string questIdText, string questName)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);

    var questRows = context.QuestPlate.AsNoTracking().Where(t =>
        t.QuestId == questIdText || t.QuestName == questName).OrderBy(t =>
        t.TranslationLang).ThenBy(t => t.TranslationEngine).ThenBy(t =>
        t.GameVersion).ThenBy(t => t.Id).ToList();

    PluginRuntimeLog.Information(
        $"[QuestProbe] questplate rows matched={questRows.Count} questId={questIdText} questName='{questName}'");

    foreach (var questPlate in questRows)
    {
      PluginRuntimeLog.Debug(
          $"[QuestProbe] db id={questPlate.Id} questId='{questPlate.QuestId}' questName='{questPlate.QuestName}' originalLang='{questPlate.OriginalLang}' translationLang='{questPlate.TranslationLang}' engine={questPlate.TranslationEngine} gameVersion='{questPlate.GameVersion}' originalNameLen={questPlate.QuestName?.Length ?? 0} originalMsgLen={questPlate.OriginalQuestMessage?.Length ?? 0} translatedNameLen={questPlate.TranslatedQuestName?.Length ?? 0} translatedMsgLen={questPlate.TranslatedQuestMessage?.Length ?? 0} objectives={questPlate.Objectives.Count} summaries={questPlate.Summaries.Count}");
    }
  }

  /// <summary>
  ///     Logs the resolved live quest progression snapshot.
  /// </summary>
  /// <param name="questProgressSnapshot">The resolved quest progress snapshot.</param>
  private void LogQuestProgressSnapshot(
      QuestProgressSnapshot questProgressSnapshot)
  {
    PluginRuntimeLog.Information(
        $"[QuestProbe] progress questId={questProgressSnapshot.QuestId} questSequence={questProgressSnapshot.QuestSequence} questName='{questProgressSnapshot.QuestName}' sheet='{questProgressSnapshot.QuestSheetName}' stepCount={questProgressSnapshot.QuestSteps.Count} seqCount={questProgressSnapshot.QuestSeqTexts.Count} systemCount={questProgressSnapshot.QuestSystemTexts.Count}");

    for (var i = 0; i < questProgressSnapshot.QuestSteps.Count; i++)
    {
      var questStep = questProgressSnapshot.QuestSteps[i];
      PluginRuntimeLog.Debug(
          $"[QuestProbe] step[{i}] keyText='{questStep.KeyText}' text='{questStep.Text}' rawKey='{questStep.OriginalKey.ExtractText()}' rawText='{questStep.OriginalText.ExtractText()}'");
    }

    for (var i = 0; i < questProgressSnapshot.QuestSeqTexts.Count; i++)
    {
      var seqEntry = questProgressSnapshot.QuestSeqTexts[i];
      PluginRuntimeLog.Debug(
          $"[QuestProbe] seq[{i}] keyText='{seqEntry.KeyText}' text='{seqEntry.Text}'");
    }

    for (var i = 0; i < questProgressSnapshot.QuestSystemTexts.Count; i++)
    {
      var sysEntry = questProgressSnapshot.QuestSystemTexts[i];
      PluginRuntimeLog.Debug(
          $"[QuestProbe] system[{i}] keyText='{sysEntry.KeyText}' text='{sysEntry.Text}'");
    }
  }

  /// <summary>
  ///     Resolves a human-readable quest name from a quest row.
  /// </summary>
  /// <param name="questRow">The quest row.</param>
  /// <returns>The resolved quest name, if available.</returns>
  private string? GetQuestName(Quest questRow)
  {
    var questType = questRow.GetType();
    var properties = new[] { "Name", "Text", "QuestName" };

    foreach (var propertyName in properties)
    {
      var property = questType.GetProperty(
          propertyName,
          BindingFlags.Instance | BindingFlags.Public);
      if (property?.GetValue(questRow) is null)
      {
        continue;
      }

      var value = property.GetValue(questRow)?.ToString();
      if (!string.IsNullOrWhiteSpace(value))
      {
        return value.Trim();
      }
    }

    return null;
  }

  /// <summary>
  ///     Converts quest text to a readable string for probe output.
  /// </summary>
  /// <param name="text">The quest text.</param>
  /// <returns>The evaluated text string.</returns>
  private string EvaluateQuestProbeText(ReadOnlySeString text)
  {
    var evaluator = Echoglossian.SeStringEvaluator;
    if (evaluator == null)
    {
      return text.ExtractText();
    }

    try
    {
      return evaluator.Evaluate(
              text,
              language: ClientStateInterface.ClientLanguage)
          .ExtractText();
    }
    catch (Exception)
    {
      return text.ExtractText();
    }
  }

  /// <summary>
  ///     Formats a probe value for log output.
  /// </summary>
  /// <param name="value">The value to format.</param>
  /// <returns>A human-readable value string.</returns>
  private string FormatProbeValue(object? value)
  {
    if (value == null)
    {
      return "null";
    }

    if (value is string text)
    {
      return $"\"{text}\"";
    }

    if (value is DateTime dateTime)
    {
      return dateTime.ToString("O", CultureInfo.InvariantCulture);
    }

    if (value is IEnumerable enumerable && value is not string)
    {
      List<string> items = new();
      foreach (var item in enumerable)
      {
        if (items.Count >= 8)
        {
          items.Add("...");
          break;
        }

        items.Add(item?.ToString() ?? "null");
      }

      return $"[{string.Join(", ", items)}]";
    }

    return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
  }

  /// <summary>
  ///     Builds the raw quest sheet name used by the game files.
  /// </summary>
  /// <param name="questRow">The resolved Lumina quest row.</param>
  /// <returns>The quest text sheet name, if it can be derived.</returns>
  private string BuildQuestTextSheetName(Quest questRow)
  {
    var questSheetId = this.GetQuestSheetIdText(questRow);
    if (questSheetId.Length < 5)
    {
      return string.Empty;
    }

    var dir = questSheetId.Substring(questSheetId.Length - 5, 3);
    return $"quest/{dir}/{questSheetId}";
  }

  /// <summary>
  ///     Resolves the quest row id as text through reflection so the probe
  ///     stays compatible with generated sheet shapes.
  /// </summary>
  /// <param name="questRow">The Lumina quest row.</param>
  /// <returns>The quest row id as text, or an empty string if unavailable.</returns>
  private string GetQuestRowIdText(Quest questRow)
  {
    var questType = questRow.GetType();
    foreach (var propertyName in new[] { "RowId", "Id" })
    {
      var property = questType.GetProperty(
          propertyName,
          BindingFlags.Instance | BindingFlags.Public);
      if (property?.GetValue(questRow) is null)
      {
        continue;
      }

      var value = property.GetValue(questRow)?.ToString();
      if (!string.IsNullOrWhiteSpace(value))
      {
        return value.Trim();
      }
    }

    return string.Empty;
  }

  /// <summary>
  ///     Resolves the quest sheet identifier used to mount the quest text
  ///     sheet path.
  /// </summary>
  /// <param name="questRow">The Lumina quest row.</param>
  /// <returns>The quest sheet identifier as text, or an empty string if unavailable.</returns>
  private string GetQuestSheetIdText(Quest questRow)
  {
    var questType = questRow.GetType();
    foreach (var propertyName in new[] { "Id" })
    {
      var property = questType.GetProperty(
          propertyName,
          BindingFlags.Instance | BindingFlags.Public);
      if (property?.GetValue(questRow) is null)
      {
        continue;
      }

      var value = property.GetValue(questRow)?.ToString();
      if (!string.IsNullOrWhiteSpace(value))
      {
        return value.Trim();
      }
    }

    return string.Empty;
  }
}
#endif


