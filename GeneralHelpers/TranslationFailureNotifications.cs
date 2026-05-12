// <copyright file="TranslationFailureNotifications.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
///     Surfaces user-visible notifications for actionable runtime translation
///     failures without spamming the hot path.
/// </summary>
public partial class Echoglossian
{
  private static readonly TimeSpan RuntimeTranslationFailureNotificationCooldown =
      TimeSpan.FromSeconds(30);

  /// <summary>
  ///     Reports one runtime translation failure so the active plugin instance
  ///     can decide whether it should surface operator feedback.
  /// </summary>
  /// <param name="translationEngine">The translation engine identifier.</param>
  /// <param name="classification">The normalized failure classification.</param>
  internal static void ReportRuntimeTranslationFailure(
      int translationEngine,
      TranslationFailureClassification classification)
  {
    activeInstance?.TryShowRuntimeTranslationFailureNotification(
        translationEngine,
        classification);
  }

  /// <summary>
  ///     Shows one deduplicated runtime notification when an actionable LLM
  ///     translation failure happens.
  /// </summary>
  /// <param name="translationEngine">The translation engine identifier.</param>
  /// <param name="classification">The normalized failure classification.</param>
  private void TryShowRuntimeTranslationFailureNotification(
      int translationEngine,
      TranslationFailureClassification classification)
  {
    if (!classification.ShouldNotifyOperator ||
        !IsLlmTranslationEngine(translationEngine) ||
        string.IsNullOrWhiteSpace(classification.UserFacingMessage))
    {
      return;
    }

    var signature =
        $"{translationEngine}:{classification.FailureReason}:{classification.UserFacingMessage}";
    var now = DateTime.UtcNow;
    this.PruneRuntimeTranslationFailureNotificationTimes(now);
    if (this.runtimeTranslationFailureNotificationTimes.TryGetValue(
            signature,
            out var lastShownAt) &&
        now - lastShownAt < RuntimeTranslationFailureNotificationCooldown)
    {
      return;
    }

    this.runtimeTranslationFailureNotificationTimes[signature] = now;

    var engineName =
        Enum.IsDefined(typeof(TransEngines), translationEngine)
            ? ((TransEngines)translationEngine).ToString()
            : translationEngine.ToString(CultureInfo.InvariantCulture);
    var notification = new Notification
    {
      Title = Resources.Name,
      Content = $"{engineName}: {classification.UserFacingMessage}",
      Icon = FontAwesomeIcon.ExclamationTriangle.ToNotificationIcon(),
      Type = NotificationType.Warning,
      UserDismissable = true,
      InitialDuration = TimeSpan.FromSeconds(20),
      HardExpiry = DateTime.UtcNow.AddMinutes(2),
    };

    var activeNotification = NotificationManager.AddNotification(notification);
    Action<Dalamud.Interface.ImGuiNotification.EventArgs.INotificationDrawArgs>?
        drawActions = null;
    var openConfigurationLabel = Resources.ResourceManager.GetString(
                                     nameof(Resources.OpenConfigurationButtonLabel),
                                     this.cultureInfo) ??
                                 Resources.OpenConfigurationButtonLabel;

    drawActions = _ =>
    {
      if (!ImGui.Button(openConfigurationLabel))
      {
        return;
      }

      this.ConfigWindow();
      if (drawActions != null)
      {
        activeNotification.DrawActions -= drawActions;
      }

      activeNotification.DismissNow();
    };

    activeNotification.DrawActions += drawActions;
  }

  /// <summary>
  ///     Determines whether the specified engine should participate in the
  ///     first-pass LLM runtime failure feedback flow.
  /// </summary>
  /// <param name="translationEngine">The translation engine identifier.</param>
  /// <returns>
  ///     <see langword="true" /> when the engine is one of the current LLM
  ///     providers; otherwise, <see langword="false" />.
  /// </returns>
  private static bool IsLlmTranslationEngine(int translationEngine)
  {
    return translationEngine is
        (int)TransEngines.ChatGPT or
        (int)TransEngines.DeepSeek or
        (int)TransEngines.Gemini or
        (int)TransEngines.OpenRouter or
        (int)TransEngines.Ollama or
        (int)TransEngines.LmStudio or
        (int)TransEngines.Claude;
  }

  /// <summary>
  ///     Removes expired notification signatures from the runtime dedupe map.
  /// </summary>
  /// <param name="now">The current UTC timestamp.</param>
  private void PruneRuntimeTranslationFailureNotificationTimes(DateTime now)
  {
    if (this.runtimeTranslationFailureNotificationTimes.Count == 0)
    {
      return;
    }

    var expiredKeys = this.runtimeTranslationFailureNotificationTimes
        .Where(pair =>
            now - pair.Value >= RuntimeTranslationFailureNotificationCooldown)
        .Select(pair => pair.Key)
        .ToArray();
    foreach (var expiredKey in expiredKeys)
    {
      this.runtimeTranslationFailureNotificationTimes.Remove(expiredKey);
    }
  }
}
