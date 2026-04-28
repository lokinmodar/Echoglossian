// <copyright file="ReferenceTextCacheRegistry.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;

namespace Echoglossian.Cache;

/// <summary>
///     Provides the specific reference-text caches used by action-adjacent
///     Excel-sheet families and aggregated text lookup helpers for
///     <c>ActionMenu</c>.
/// </summary>
public static class ReferenceTextCacheRegistry
{
    /// <summary>
    ///     Gets the specific GeneralAction cache.
    /// </summary>
    public static ReferenceTextCacheStore<GeneralActionText> GeneralActionTexts { get; } =
        new("GeneralActionCacheManager");

    /// <summary>
    ///     Gets the specific BuddyAction cache.
    /// </summary>
    public static ReferenceTextCacheStore<BuddyActionText> BuddyActionTexts { get; } =
        new("BuddyActionCacheManager");

    /// <summary>
    ///     Gets the specific CompanyAction cache.
    /// </summary>
    public static ReferenceTextCacheStore<CompanyActionText> CompanyActionTexts { get; } =
        new("CompanyActionCacheManager");

    /// <summary>
    ///     Gets the specific CraftAction cache.
    /// </summary>
    public static ReferenceTextCacheStore<CraftActionText> CraftActionTexts { get; } =
        new("CraftActionCacheManager");

    /// <summary>
    ///     Gets the specific PetAction cache.
    /// </summary>
    public static ReferenceTextCacheStore<PetActionText> PetActionTexts { get; } =
        new("PetActionCacheManager");

    /// <summary>
    ///     Gets the specific EventAction cache.
    /// </summary>
    public static ReferenceTextCacheStore<EventActionText> EventActionTexts { get; } =
        new("EventActionCacheManager");

    /// <summary>
    ///     Gets the specific EventItem cache.
    /// </summary>
    public static ReferenceTextCacheStore<EventItemText> EventItemTexts { get; } =
        new("EventItemCacheManager");

    /// <summary>
    ///     Gets the specific BgcArmyAction cache.
    /// </summary>
    public static ReferenceTextCacheStore<BgcArmyActionText> BgcArmyActionTexts { get; } =
        new("BgcArmyActionCacheManager");

    /// <summary>
    ///     Gets the specific AozAction cache.
    /// </summary>
    public static ReferenceTextCacheStore<AozActionText> AozActionTexts { get; } =
        new("AozActionCacheManager");

    /// <summary>
    ///     Gets the specific PvPAction cache.
    /// </summary>
    public static ReferenceTextCacheStore<PvPActionText> PvPActionTexts { get; } =
        new("PvPActionCacheManager");

    /// <summary>
    ///     Gets the specific MountAction cache.
    /// </summary>
    public static ReferenceTextCacheStore<MountActionText> MountActionTexts { get; } =
        new("MountActionCacheManager");

    /// <summary>
    ///     Gets the specific MainCommand sheet cache.
    /// </summary>
    public static ReferenceTextCacheStore<MainCommandText> MainCommandTexts { get; } =
        new("MainCommandCacheManager");

    /// <summary>
    ///     Gets the specific EurekaMagiaAction cache.
    /// </summary>
    public static ReferenceTextCacheStore<EurekaMagiaActionText> EurekaMagiaActionTexts { get; } =
        new("EurekaMagiaActionCacheManager");

    /// <summary>
    ///     Gets the specific DeepDungeonItem cache.
    /// </summary>
    public static ReferenceTextCacheStore<DeepDungeonItemText> DeepDungeonItemTexts { get; } =
        new("DeepDungeonItemCacheManager");

    /// <summary>
    ///     Preloads every specific reference-text cache from SQLite.
    /// </summary>
    /// <param name="configDir">The plugin configuration directory.</param>
    public static void PreloadAll(string configDir)
    {
        GeneralActionTexts.Preload(
            configDir,
            static context => context.GeneralActionTexts);
        BuddyActionTexts.Preload(
            configDir,
            static context => context.BuddyActionTexts);
        CompanyActionTexts.Preload(
            configDir,
            static context => context.CompanyActionTexts);
        CraftActionTexts.Preload(
            configDir,
            static context => context.CraftActionTexts);
        PetActionTexts.Preload(
            configDir,
            static context => context.PetActionTexts);
        EventActionTexts.Preload(
            configDir,
            static context => context.EventActionTexts);
        EventItemTexts.Preload(
            configDir,
            static context => context.EventItemTexts);
        BgcArmyActionTexts.Preload(
            configDir,
            static context => context.BgcArmyActionTexts);
        AozActionTexts.Preload(
            configDir,
            static context => context.AozActionTexts);
        PvPActionTexts.Preload(
            configDir,
            static context => context.PvPActionTexts);
        MountActionTexts.Preload(
            configDir,
            static context => context.MountActionTexts);
        MainCommandTexts.Preload(
            configDir,
            static context => context.MainCommandTexts);
        EurekaMagiaActionTexts.Preload(
            configDir,
            static context => context.EurekaMagiaActionTexts);
        DeepDungeonItemTexts.Preload(
            configDir,
            static context => context.DeepDungeonItemTexts);
    }

    /// <summary>
    ///     Clears every specific reference-text cache.
    /// </summary>
    public static void ClearAll()
    {
        GeneralActionTexts.Clear();
        BuddyActionTexts.Clear();
        CompanyActionTexts.Clear();
        CraftActionTexts.Clear();
        PetActionTexts.Clear();
        EventActionTexts.Clear();
        EventItemTexts.Clear();
        BgcArmyActionTexts.Clear();
        AozActionTexts.Clear();
        PvPActionTexts.Clear();
        MountActionTexts.Clear();
        MainCommandTexts.Clear();
        EurekaMagiaActionTexts.Clear();
        DeepDungeonItemTexts.Clear();
    }

    /// <summary>
    ///     Tries to resolve one exact translated text from the specific
    ///     reference-text caches.
    /// </summary>
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation-engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="originalText">The original text to translate.</param>
    /// <param name="translatedText">The resolved translated text.</param>
    /// <returns>
    ///     <see langword="true" /> when one translated text was found;
    ///     otherwise <see langword="false" />.
    /// </returns>
    public static bool TryFindTranslatedText(
        string lang,
        int engine,
        string? gameVersion,
        string originalText,
        out string translatedText)
    {
        translatedText = string.Empty;

        return GeneralActionTexts.TryFindTranslatedText(
                   lang,
                   engine,
                   gameVersion,
                   originalText,
                   out translatedText) ||
               BuddyActionTexts.TryFindTranslatedText(
                   lang,
                   engine,
                   gameVersion,
                   originalText,
                   out translatedText) ||
               CompanyActionTexts.TryFindTranslatedText(
                   lang,
                   engine,
                   gameVersion,
                   originalText,
                   out translatedText) ||
               CraftActionTexts.TryFindTranslatedText(
                   lang,
                   engine,
                   gameVersion,
                   originalText,
                   out translatedText) ||
               PetActionTexts.TryFindTranslatedText(
                   lang,
                   engine,
                   gameVersion,
                   originalText,
                   out translatedText) ||
               EventActionTexts.TryFindTranslatedText(
                   lang,
                   engine,
                   gameVersion,
                   originalText,
                   out translatedText) ||
               BgcArmyActionTexts.TryFindTranslatedText(
                   lang,
                   engine,
                   gameVersion,
                   originalText,
                   out translatedText) ||
               AozActionTexts.TryFindTranslatedText(
                   lang,
                   engine,
                   gameVersion,
                   originalText,
                   out translatedText) ||
               PvPActionTexts.TryFindTranslatedText(
                   lang,
                   engine,
                   gameVersion,
                   originalText,
                   out translatedText) ||
               MountActionTexts.TryFindTranslatedText(
                   lang,
                   engine,
                   gameVersion,
                   originalText,
                   out translatedText) ||
               MainCommandTexts.TryFindTranslatedText(
                   lang,
                   engine,
                   gameVersion,
                   originalText,
                   out translatedText) ||
               EurekaMagiaActionTexts.TryFindTranslatedText(
                   lang,
                   engine,
                   gameVersion,
                   originalText,
                   out translatedText);
    }

    /// <summary>
    ///     Tries to resolve one exact canonical original text from the specific
    ///     reference-text caches.
    /// </summary>
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation-engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="translatedText">The translated text to reverse.</param>
    /// <param name="originalText">The resolved canonical original text.</param>
    /// <returns>
    ///     <see langword="true" /> when one original text was found;
    ///     otherwise <see langword="false" />.
    /// </returns>
    public static bool TryFindOriginalText(
        string lang,
        int engine,
        string? gameVersion,
        string translatedText,
        out string originalText)
    {
        originalText = string.Empty;

        return GeneralActionTexts.TryFindOriginalText(
                   lang,
                   engine,
                   gameVersion,
                   translatedText,
                   out originalText) ||
               BuddyActionTexts.TryFindOriginalText(
                   lang,
                   engine,
                   gameVersion,
                   translatedText,
                   out originalText) ||
               CompanyActionTexts.TryFindOriginalText(
                   lang,
                   engine,
                   gameVersion,
                   translatedText,
                   out originalText) ||
               CraftActionTexts.TryFindOriginalText(
                   lang,
                   engine,
                   gameVersion,
                   translatedText,
                   out originalText) ||
               PetActionTexts.TryFindOriginalText(
                   lang,
                   engine,
                   gameVersion,
                   translatedText,
                   out originalText) ||
               EventActionTexts.TryFindOriginalText(
                   lang,
                   engine,
                   gameVersion,
                   translatedText,
                   out originalText) ||
               BgcArmyActionTexts.TryFindOriginalText(
                   lang,
                   engine,
                   gameVersion,
                   translatedText,
                   out originalText) ||
               AozActionTexts.TryFindOriginalText(
                   lang,
                   engine,
                   gameVersion,
                   translatedText,
                   out originalText) ||
               PvPActionTexts.TryFindOriginalText(
                   lang,
                   engine,
                   gameVersion,
                   translatedText,
                   out originalText) ||
               MountActionTexts.TryFindOriginalText(
                   lang,
                   engine,
                   gameVersion,
                   translatedText,
                   out originalText) ||
               MainCommandTexts.TryFindOriginalText(
                   lang,
                   engine,
                   gameVersion,
                   translatedText,
                   out originalText) ||
               EurekaMagiaActionTexts.TryFindOriginalText(
                   lang,
                   engine,
                   gameVersion,
                   translatedText,
                   out originalText);
    }

    /// <summary>
    ///     Determines whether one canonical original text already exists in
    ///     the specific reference-text caches.
    /// </summary>
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation-engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="originalText">The canonical original text to test.</param>
    /// <returns>
    ///     <see langword="true" /> when the original text exists in one of
    ///     the specific reference-text caches; otherwise <see langword="false" />.
    /// </returns>
    public static bool ContainsOriginalText(
        string lang,
        int engine,
        string? gameVersion,
        string originalText)
    {
        return GeneralActionTexts.ContainsOriginalText(
                   lang,
                   engine,
                   gameVersion,
                   originalText) ||
               BuddyActionTexts.ContainsOriginalText(
                   lang,
                   engine,
                   gameVersion,
                   originalText) ||
               CompanyActionTexts.ContainsOriginalText(
                   lang,
                   engine,
                   gameVersion,
                   originalText) ||
               CraftActionTexts.ContainsOriginalText(
                   lang,
                   engine,
                   gameVersion,
                   originalText) ||
               PetActionTexts.ContainsOriginalText(
                   lang,
                   engine,
                   gameVersion,
                   originalText) ||
               EventActionTexts.ContainsOriginalText(
                   lang,
                   engine,
                   gameVersion,
                   originalText) ||
               BgcArmyActionTexts.ContainsOriginalText(
                   lang,
                   engine,
                   gameVersion,
                   originalText) ||
               AozActionTexts.ContainsOriginalText(
                   lang,
                   engine,
                   gameVersion,
                   originalText) ||
               PvPActionTexts.ContainsOriginalText(
                   lang,
                   engine,
                   gameVersion,
                   originalText) ||
               MountActionTexts.ContainsOriginalText(
                   lang,
                   engine,
                   gameVersion,
                   originalText) ||
               MainCommandTexts.ContainsOriginalText(
                   lang,
                   engine,
                   gameVersion,
                   originalText) ||
               EurekaMagiaActionTexts.ContainsOriginalText(
                   lang,
                   engine,
                   gameVersion,
                   originalText);
    }

    /// <summary>
    ///     Tries to resolve one translated action-adjacent reference payload by
    ///     stable identity.
    /// </summary>
    /// <param name="referenceId">The sheet-row identifier.</param>
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation-engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="payload">The translated payload, if any.</param>
    /// <returns>
    ///     <see langword="true" /> when one translated payload was found;
    ///     otherwise <see langword="false" />.
    /// </returns>
    public static bool TryFindTranslatedActionIdentityPayload(
        uint referenceId,
        string lang,
        int engine,
        string? gameVersion,
        out ReferenceTextCanonicalPayload payload)
    {
        payload = new ReferenceTextCanonicalPayload();

        return TryFindTranslatedIdentityPayload(
                   GeneralActionTexts,
                   referenceId,
                   lang,
                   engine,
                   gameVersion,
                   out payload) ||
               TryFindTranslatedIdentityPayload(
                   BuddyActionTexts,
                   referenceId,
                   lang,
                   engine,
                   gameVersion,
                   out payload) ||
               TryFindTranslatedIdentityPayload(
                   CompanyActionTexts,
                   referenceId,
                   lang,
                   engine,
                   gameVersion,
                   out payload) ||
               TryFindTranslatedIdentityPayload(
                   CraftActionTexts,
                   referenceId,
                   lang,
                   engine,
                   gameVersion,
                   out payload) ||
               TryFindTranslatedIdentityPayload(
                   PetActionTexts,
                   referenceId,
                   lang,
                   engine,
                   gameVersion,
                   out payload) ||
               TryFindTranslatedIdentityPayload(
                   EventActionTexts,
                   referenceId,
                   lang,
                   engine,
                   gameVersion,
                   out payload) ||
               TryFindTranslatedIdentityPayload(
                   BgcArmyActionTexts,
                   referenceId,
                   lang,
                   engine,
                   gameVersion,
                   out payload) ||
               TryFindTranslatedIdentityPayload(
                   AozActionTexts,
                   referenceId,
                   lang,
                   engine,
                   gameVersion,
                   out payload) ||
               TryFindTranslatedIdentityPayload(
                   PvPActionTexts,
                   referenceId,
                   lang,
                   engine,
                   gameVersion,
                   out payload) ||
               TryFindTranslatedIdentityPayload(
                   MountActionTexts,
                   referenceId,
                   lang,
                   engine,
                   gameVersion,
                   out payload) ||
               TryFindTranslatedIdentityPayload(
                   MainCommandTexts,
                   referenceId,
                   lang,
                   engine,
                   gameVersion,
                   out payload) ||
               TryFindTranslatedIdentityPayload(
                   EurekaMagiaActionTexts,
                   referenceId,
                   lang,
                   engine,
                   gameVersion,
                   out payload);
    }

    /// <summary>
    ///     Tries to resolve one translated item-adjacent reference payload by
    ///     stable identity.
    /// </summary>
    /// <param name="referenceId">The sheet-row identifier.</param>
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation-engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="payload">The translated payload, if any.</param>
    /// <returns>
    ///     <see langword="true" /> when one translated payload was found;
    ///     otherwise <see langword="false" />.
    /// </returns>
    public static bool TryFindTranslatedItemIdentityPayload(
        uint referenceId,
        string lang,
        int engine,
        string? gameVersion,
        out ReferenceTextCanonicalPayload payload)
    {
        payload = new ReferenceTextCanonicalPayload();

        return TryFindTranslatedIdentityPayload(
                   EventItemTexts,
                   referenceId,
                   lang,
                   engine,
                   gameVersion,
                   out payload) ||
               TryFindTranslatedIdentityPayload(
                   DeepDungeonItemTexts,
                   referenceId,
                   lang,
                   engine,
                   gameVersion,
                   out payload);
    }

    /// <summary>
    ///     Tries to resolve one translated payload by exact row identity from a
    ///     specific reference-text cache.
    /// </summary>
    /// <typeparam name="TRow">The specific persisted row type.</typeparam>
    /// <param name="cacheStore">The cache store to query.</param>
    /// <param name="referenceId">The sheet-row identifier.</param>
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation-engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="payload">The translated payload, if any.</param>
    /// <returns>
    ///     <see langword="true" /> when one translated payload was found;
    ///     otherwise <see langword="false" />.
    /// </returns>
    private static bool TryFindTranslatedIdentityPayload<TRow>(
        ReferenceTextCacheStore<TRow> cacheStore,
        uint referenceId,
        string lang,
        int engine,
        string? gameVersion,
        out ReferenceTextCanonicalPayload payload)
        where TRow : ReferenceTextRowBase
    {
        payload = new ReferenceTextCanonicalPayload();

        var row = cacheStore.TryFindIdentityMatch(
            referenceId,
            lang,
            engine,
            gameVersion);
        if (row == null)
        {
            return false;
        }

        var resolvedPayload = ReferenceTextCanonicalPayload.Deserialize(
            row.CanonicalPayloadAsText);
        if (resolvedPayload == null ||
            string.IsNullOrWhiteSpace(resolvedPayload.TranslatedName) ||
            (!string.IsNullOrWhiteSpace(resolvedPayload.Description) &&
             string.IsNullOrWhiteSpace(
                 resolvedPayload.TranslatedDescription)))
        {
            return false;
        }

        payload = resolvedPayload;
        return true;
    }
}
