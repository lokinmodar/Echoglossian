// <copyright file="RuntimeConfigurationRefreshContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Verifies runtime configuration refresh sequencing for translation
///     signature changes.
/// </summary>
public sealed class RuntimeConfigurationRefreshContractTests
{
    /// <summary>
    ///     Ensures every quest-surface toggle that can leave handler-owned
    ///     native state on screen invalidates the addon-handler refresh
    ///     signature.
    /// </summary>
    /// <param name="fieldName">The config field that toggles one quest surface.</param>
    [Theory]
    [InlineData(nameof(Config.TranslateJournal))]
    [InlineData(nameof(Config.TranslateJournalDetail))]
    [InlineData(nameof(Config.TranslateJournalAccept))]
    [InlineData(nameof(Config.TranslateJournalResult))]
    [InlineData(nameof(Config.TranslateRecommendList))]
    [InlineData(nameof(Config.TranslateAreaMap))]
    public void AddonHandlerRegistrationSignature_ChangesWhenQuestSurfaceToggleChanges(
        string fieldName)
    {
        var disabled = new Config();
        var enabled = new Config();
        var field = typeof(Config).GetField(fieldName);

        Assert.NotNull(field);
        field!.SetValue(enabled, true);

        Assert.NotEqual(
            Echoglossian.ComputeAddonHandlerRegistrationSignature(disabled),
            Echoglossian.ComputeAddonHandlerRegistrationSignature(enabled));
    }

    /// <summary>
    ///     Ensures visible addon-owned presentation is restored before runtime
    ///     reset and translator rebuild happen.
    /// </summary>
    [Fact]
    public void Translation_refresh_restores_visible_addons_before_resetting_runtime_state()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "GeneralHelpers",
            "RuntimeConfigurationRefresh.cs"));

        var restoreCall = source.IndexOf(
            "this.RestoreVisibleAddonPresentationStateBeforeRuntimeReset();",
            StringComparison.Ordinal);
        var resetCall = source.IndexOf(
            "this.ResetRuntimeTranslationPresentationState();",
            StringComparison.Ordinal);
        var rebuildCall = source.IndexOf(
            "this.RebuildTranslationServiceSafely();",
            StringComparison.Ordinal);

        Assert.True(restoreCall >= 0);
        Assert.True(resetCall > restoreCall);
        Assert.True(rebuildCall > resetCall);
        Assert.Contains(
            "translationRefreshRestoreApplied",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures live configuration refresh synchronizes the authoritative
    /// target-language runtime state before translation signatures and
    /// translator rebuilds are evaluated.
    /// </summary>
    [Fact]
    public void Translation_refresh_synchronizes_target_language_before_signature_and_rebuild()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "GeneralHelpers",
            "RuntimeConfigurationRefresh.cs"));

        var synchronizeCall = source.IndexOf(
            "TargetLanguageRuntimeState.Synchronize(",
            StringComparison.Ordinal);
        var signatureCall = source.IndexOf(
            "this.ComputeTranslationRuntimeSignature();",
            StringComparison.Ordinal);
        var rebuildCall = source.IndexOf(
            "this.RebuildTranslationServiceSafely();",
            StringComparison.Ordinal);

        Assert.True(synchronizeCall >= 0);
        Assert.True(signatureCall > synchronizeCall);
        Assert.True(rebuildCall > synchronizeCall);
    }

    /// <summary>
    ///     Ensures a live translator rebuild also recreates the NamePlate
    ///     runtime so it cannot keep using a stale captured
    ///     <c>TranslationService</c> instance after language or provider
    ///     changes.
    /// </summary>
    [Fact]
    public void Translation_refresh_rebuilds_nameplate_runtime_when_translation_signature_changes()
    {
        var root = FindRepositoryRoot();
        var refreshSource = File.ReadAllText(Path.Combine(
            root.FullName,
            "GeneralHelpers",
            "RuntimeConfigurationRefresh.cs"));
        var registrationSource = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "Helpers",
            "NamePlateTranslationRuntimeRegistration.cs"));

        var rebuildServiceCall = refreshSource.IndexOf(
            "this.RebuildTranslationServiceSafely();",
            StringComparison.Ordinal);
        var rebuildNamePlateCall = refreshSource.IndexOf(
            "this.RebuildNamePlateTranslationRuntime();",
            StringComparison.Ordinal);
        var signatureUpdate = refreshSource.IndexOf(
            "this.translationRuntimeSignature = translationSignature;",
            StringComparison.Ordinal);

        Assert.True(rebuildServiceCall >= 0);
        Assert.True(rebuildNamePlateCall > rebuildServiceCall);
        Assert.True(signatureUpdate > rebuildNamePlateCall);
        Assert.Contains(
            "private void RebuildNamePlateTranslationRuntime()",
            registrationSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "this.UnregisterNamePlateTranslationRuntime();",
            registrationSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "this.namePlateTranslationRuntime.Dispose();",
            registrationSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "this.namePlateTranslationRuntime = this.CreateNamePlateTranslationRuntime();",
            registrationSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "this.RegisterNamePlateTranslationRuntime();",
            registrationSource,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures a translation runtime reset clears capability snapshots so a
    ///     later configuration scope cannot reuse stale policy state.
    /// </summary>
    [Fact]
    public void Translation_refresh_clears_llm_capability_runtime_cache()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "GeneralHelpers",
            "RuntimeConfigurationRefresh.cs"));

        var resetMethod = source.IndexOf(
            "private void ResetRuntimeTranslationPresentationState()",
            StringComparison.Ordinal);
        var capabilityCacheClear = source.IndexOf(
            "LlmCapabilityCacheManager.Clear();",
            StringComparison.Ordinal);

        Assert.True(resetMethod >= 0);
        Assert.True(capabilityCacheClear > resetMethod);
    }

    /// <summary>
    ///     Ensures every persisted LLM prompt contributes to the translation
    ///     runtime signature so prompt-only saves request a rebuild on the next
    ///     framework tick.
    /// </summary>
    [Fact]
    public void Translation_runtime_signature_includes_all_persisted_llm_prompts()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "GeneralHelpers",
            "RuntimeConfigurationRefresh.cs"));

        Assert.Contains("this.configuration.ChatGptPrompt,", source, StringComparison.Ordinal);
        Assert.Contains("this.configuration.ClaudePrompt,", source, StringComparison.Ordinal);
        Assert.Contains("this.configuration.DeepSeekPrompt,", source, StringComparison.Ordinal);
        Assert.Contains("this.configuration.GeminiPrompt,", source, StringComparison.Ordinal);
        Assert.Contains("this.configuration.OpenRouterPrompt,", source, StringComparison.Ordinal);
        Assert.Contains("this.configuration.AmazonPrompt,", source, StringComparison.Ordinal);
        Assert.Contains("this.configuration.MicrosoftTranslatorPrompt,", source, StringComparison.Ordinal);
        Assert.Contains("this.configuration.YandexCloudPrompt,", source, StringComparison.Ordinal);
        Assert.Contains("this.configuration.OllamaPrompt,", source, StringComparison.Ordinal);
        Assert.Contains("this.configuration.LmStudioPrompt,", source, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the dialogue-only LLM override contributes to the
    ///     translation runtime signature so override-only saves rebuild the
    ///     live translation service.
    /// </summary>
    [Fact]
    public void Translation_runtime_signature_includes_dialogue_override_fields()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "GeneralHelpers",
            "RuntimeConfigurationRefresh.cs"));

        Assert.Contains(
            "this.configuration.UseDialogueLlmOverride,",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "this.configuration.DialogueLlmEngine,",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "this.configuration.DialogueLlmEngineKey,",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures a completed save only marks runtime configuration dirty and
    ///     defers translator rebuilds until the next framework tick applies the
    ///     translation signature gate.
    /// </summary>
    [Fact]
    public void Configuration_save_marks_runtime_dirty_before_next_tick_translation_rebuild()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "GeneralHelpers",
            "RuntimeConfigurationRefresh.cs"));

        var onSaved = source.IndexOf(
            "private void OnConfigurationSaved(Config config)",
            StringComparison.Ordinal);
        var dirtyAssignment = source.IndexOf(
            "this.runtimeConfigurationDirty = true;",
            onSaved,
            StringComparison.Ordinal);
        var applyPending = source.IndexOf(
            "private void ApplyPendingRuntimeConfigurationChanges()",
            StringComparison.Ordinal);
        var translationChanged = source.IndexOf(
            "if (translationChanged)",
            applyPending,
            StringComparison.Ordinal);
        var rebuildCall = source.IndexOf(
            "this.RebuildTranslationServiceSafely();",
            translationChanged,
            StringComparison.Ordinal);

        Assert.True(onSaved >= 0);
        Assert.True(dirtyAssignment > onSaved);
        Assert.True(applyPending > dirtyAssignment);
        Assert.True(translationChanged > applyPending);
        Assert.True(rebuildCall > translationChanged);
    }

    /// <summary>
    ///     Finds the repository root from the current test directory.
    /// </summary>
    /// <returns>The repository root directory.</returns>
    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Echoglossian.sln")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root.");
    }
}
