// <copyright file="FieldValidationHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Helpers;

/// <summary>
/// Provides UI helpers to display field validation warnings.
/// </summary>
public static class FieldValidationHelper
{
  private static readonly Dictionary<string, bool> FieldTouched = new();

  /// <summary>
  /// Displays a warning message if the field value is null or whitespace.
  /// </summary>
  public static void ShowFieldRequiredWarningIfEmpty(string fieldLabel, string? fieldValue)
  {
    if (string.IsNullOrWhiteSpace(fieldValue))
    {
      ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.4f, 0.4f, 1f));
      ImGui.TextWrapped(FormatRequiredFieldMessage(fieldLabel));
      ImGui.PopStyleColor();
    }
  }

  /// <summary>
  /// Draws a validated input text field. Highlights red and shows warning only after first interaction.
  /// </summary>
  /// <returns>True if the input value changed.</returns>
  public static bool ValidatedInputText(string label, ref string value, int maxLength, out bool isInvalid)
  {
    if (!FieldTouched.ContainsKey(label))
    {
      FieldTouched[label] = false;
    }

    bool changed = ImGui.InputText(label, ref value, maxLength);

    if (ImGui.IsItemActive() || changed)
    {
      FieldTouched[label] = true;
    }

    isInvalid = string.IsNullOrWhiteSpace(value);

    if (FieldTouched[label] && isInvalid)
    {
      ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.5f, 0.1f, 0.1f, 1f));
      ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 0.2f, 0.2f, 1f));
      ImGui.PopStyleColor(2);

      ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.4f, 0.4f, 1f));
      ImGui.TextWrapped(FormatRequiredFieldMessage(label));
      ImGui.PopStyleColor();
    }

    return changed;
  }

  /// <summary>
  /// Forces a field to be considered "touched", useful for triggering validation manually (e.g., on submit).
  /// </summary>
  public static void MarkFieldAsTouched(string label)
  {
    FieldTouched[label] = true;
  }

  /// <summary>
  /// Marks all known user-editable config fields as "touched" to trigger validation warnings.
  /// Call this from a "Save" or "Submit" action if needed.
  /// </summary>
  /// <param name="config">The plugin configuration instance.</param>
  public static void MarkAllRequiredFieldsTouched(Config config)
  {
    // OpenRouter
    MarkFieldAsTouched(Resources.APIKey);
    MarkFieldAsTouched(Resources.ModelEndpoint);
    MarkFieldAsTouched(Resources.LLMModel);

    // ChatGPT
    MarkFieldAsTouched(Resources.ChatGptApiKey);
    MarkFieldAsTouched(Resources.ModelEndpoint);
    MarkFieldAsTouched(Resources.LLMModel);

    // Claude
    MarkFieldAsTouched(Resources.APIKey);
    MarkFieldAsTouched(Resources.ModelEndpoint);
    MarkFieldAsTouched(Resources.LLMModel);

    // Amazon
    MarkFieldAsTouched(Resources.AWSAccessKey);
    MarkFieldAsTouched(Resources.AWSSecretKey);
    MarkFieldAsTouched(Resources.Region);

    // DeepSeek
    MarkFieldAsTouched(Resources.APIKey);
    MarkFieldAsTouched(Resources.Endpoint);

    // Gemini
    MarkFieldAsTouched(Resources.GeminiAPIKey);

    // LibreTranslate
    MarkFieldAsTouched(Resources.LibreTranslateAPIEndpoint);

    // Microsoft
    MarkFieldAsTouched(Resources.MicrosoftTranslatorAPIKey);
    MarkFieldAsTouched(Resources.Region);

    // Ollama
    MarkFieldAsTouched(Resources.ModelEndpoint);

    // Yandex Cloud
    MarkFieldAsTouched(Resources.YandexCloudFolderId);
    MarkFieldAsTouched(Resources.YandexCloudApiKey);
  }

  private static string FormatRequiredFieldMessage(string label)
  {
    var format =
      Resources.ResourceManager.GetString("RequiredFieldMessageFormat", Resources.Culture) ??
      "{0} is required.";
    return string.Format(format, label);
  }
}
