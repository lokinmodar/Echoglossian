// <copyright file="LlmSurfaceGroupRoutingPolicy.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Helpers;

namespace Echoglossian.Translators;

/// <summary>
///     Resolves the first-pass LLM-only surface-group routing policy while
///     keeping the global engine as the default path.
/// </summary>
internal static class LlmSurfaceGroupRoutingPolicy
{
  /// <summary>
  ///     Describes the current operator-facing state of the first-pass
  ///     dialogue-family LLM override routing.
  /// </summary>
  /// <param name="PrimaryEngine">The globally selected primary engine.</param>
  /// <param name="OverrideEnabled">Whether the dialogue override toggle is on.</param>
  /// <param name="SelectedOverrideEngine">
  ///     The normalized LLM override engine selection.
  /// </param>
  /// <param name="OverrideConfigured">
  ///     Whether the selected override engine is
  ///     configured enough to be used safely.
  /// </param>
  /// <param name="OverrideActive">
  ///     Whether dialogue-family requests are actively
  ///     routing through the override engine.
  /// </param>
  /// <param name="EffectiveDialogueEngine">
  ///     The engine currently used for
  ///     dialogue-family requests.
  /// </param>
  internal readonly record struct DialogueOverrideState(
      Echoglossian.TransEngines PrimaryEngine,
      bool OverrideEnabled,
      Echoglossian.TransEngines SelectedOverrideEngine,
      bool OverrideConfigured,
      bool OverrideActive,
      Echoglossian.TransEngines EffectiveDialogueEngine);

  /// <summary>
  ///     Normalizes the persisted dialogue override selection so its numeric
  ///     and string forms stay aligned and only LLM-backed engines remain
  ///     valid for the first-pass override path.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <returns>
  ///     <see langword="true" /> when one or more persisted fields changed;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  internal static bool NormalizeDialogueOverrideSelection(Config config)
  {
    var normalizedEngine = Echoglossian.TransEngines.ChatGPT;
    if (TryResolvePersistedOverrideEngine(
            config.DialogueLlmEngine,
            config.DialogueLlmEngineKey,
            out var resolvedEngine) &&
        IsLlmEngine(resolvedEngine))
    {
      normalizedEngine = resolvedEngine;
    }

    var changed = false;
    if (config.DialogueLlmEngine != (int)normalizedEngine)
    {
      config.DialogueLlmEngine = (int)normalizedEngine;
      changed = true;
    }

    var normalizedKey = normalizedEngine.ToString();
    if (!string.Equals(
            config.DialogueLlmEngineKey,
            normalizedKey,
            StringComparison.Ordinal))
    {
      config.DialogueLlmEngineKey = normalizedKey;
      changed = true;
    }

    return changed;
  }

  /// <summary>
  ///     Resolves the effective engine for the given surface group.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <param name="surfaceGroup">The incoming translation surface group.</param>
  /// <returns>The effective translation engine for the request.</returns>
  internal static Echoglossian.TransEngines ResolveEngine(
      Config config,
      TranslationSurfaceGroup surfaceGroup)
  {
    if (surfaceGroup == TranslationSurfaceGroup.Dialogue &&
        TryResolveDialogueOverrideEngine(config, out var overrideEngine))
    {
      return overrideEngine;
    }

    return (Echoglossian.TransEngines)config.ChosenTransEngine;
  }

  /// <summary>
  ///     Gets a debugger-friendly snapshot of the current dialogue override
  ///     routing state.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <returns>The current dialogue override routing state.</returns>
  internal static DialogueOverrideState GetDialogueOverrideState(Config config)
  {
    NormalizeDialogueOverrideSelection(config);

    var primaryEngine = (Echoglossian.TransEngines)config.ChosenTransEngine;
    var selectedOverrideEngine =
        (Echoglossian.TransEngines)config.DialogueLlmEngine;
    var overrideConfigured =
        TranslationEngineConfigurationHelper.IsConfigured(
            config,
            selectedOverrideEngine);
    var overrideActive = TryResolveDialogueOverrideEngine(
        config,
        out var effectiveOverrideEngine);

    return new DialogueOverrideState(
        primaryEngine,
        config.UseDialogueLlmOverride,
        selectedOverrideEngine,
        overrideConfigured,
        overrideActive,
        overrideActive
            ? effectiveOverrideEngine
            : primaryEngine);
  }

  /// <summary>
  ///     Attempts to resolve a usable dialogue-family LLM override engine.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <param name="engine">Receives the override engine when available.</param>
  /// <returns>
  ///     <see langword="true" /> when dialogue-family requests should route to
  ///     an LLM override engine; otherwise, <see langword="false" />.
  /// </returns>
  internal static bool TryResolveDialogueOverrideEngine(
      Config config,
      out Echoglossian.TransEngines engine)
  {
    engine = Echoglossian.TransEngines.Google;
    if (!config.UseDialogueLlmOverride)
    {
      return false;
    }

    if (!TryResolvePersistedOverrideEngine(
            config.DialogueLlmEngine,
            config.DialogueLlmEngineKey,
            out engine))
    {
      return false;
    }

    return IsLlmEngine(engine) &&
           TranslationEngineConfigurationHelper.IsConfigured(config, engine);
  }

  /// <summary>
  ///     Determines whether the given engine belongs to the current LLM family
  ///     allowed for first-pass surface-group override routing.
  /// </summary>
  /// <param name="engine">The engine to inspect.</param>
  /// <returns>
  ///     <see langword="true" /> when the engine is an LLM-backed translator;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  internal static bool IsLlmEngine(Echoglossian.TransEngines engine)
  {
    return engine is
        Echoglossian.TransEngines.ChatGPT or
        Echoglossian.TransEngines.DeepSeek or
        Echoglossian.TransEngines.Gemini or
        Echoglossian.TransEngines.OpenRouter or
        Echoglossian.TransEngines.Ollama or
        Echoglossian.TransEngines.LmStudio or
        Echoglossian.TransEngines.Claude;
  }

  /// <summary>
  ///     Resolves one persisted override engine from its numeric and string
  ///     forms without applying language-support normalization.
  /// </summary>
  /// <param name="engineId">The persisted numeric engine id.</param>
  /// <param name="engineKey">The persisted engine key.</param>
  /// <param name="engine">Receives the resolved concrete engine.</param>
  /// <returns>
  ///     <see langword="true" /> when the override resolved to one concrete
  ///     engine; otherwise, <see langword="false" />.
  /// </returns>
  private static bool TryResolvePersistedOverrideEngine(
      int engineId,
      string? engineKey,
      out Echoglossian.TransEngines engine)
  {
    if (TranslationEngineSelectionMigrationHelper.IsConcreteEngineId(engineId))
    {
      engine = (Echoglossian.TransEngines)engineId;
      return true;
    }

    if (TranslationEngineSelectionMigrationHelper.TryResolveEngineKey(
            engineKey,
            out var engineIdFromKey) &&
        TranslationEngineSelectionMigrationHelper.IsConcreteEngineId(
            engineIdFromKey))
    {
      engine = (Echoglossian.TransEngines)engineIdFromKey;
      return true;
    }

    engine = Echoglossian.TransEngines.Google;
    return false;
  }
}
