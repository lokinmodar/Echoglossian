// <copyright file="NamePlatePrefetchRuntime.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Game.Gui.NamePlate;

namespace Echoglossian;

/// <summary>
///     Provides DB-first background prefetch for recently observed NamePlateGui
///     world-object names.
/// </summary>
public partial class Echoglossian
{
  private const int NamePlatePrefetchCandidatesPerTick = 8;

  private static readonly TimeSpan NamePlatePrefetchTickInterval =
      TimeSpan.FromSeconds(2);

  private static readonly TimeSpan NamePlatePrefetchCandidateLifetime =
      TimeSpan.FromMinutes(2);

  private readonly ConcurrentDictionary<string, NamePlatePrefetchCandidateState>
      namePlatePrefetchCandidates = new(StringComparer.Ordinal);

  private readonly List<NamePlatePrefetchCandidate> namePlatePrefetchQueue = [];

  private string namePlatePrefetchSignature = string.Empty;

  private DateTime namePlatePrefetchLastTickUtc = DateTime.MinValue;

  private int namePlatePrefetchQueueIndex;

  /// <summary>
  ///     Records one stable nameplate source observed from the live
  ///     NamePlateGui callback so background prefetch can translate it later.
  /// </summary>
  /// <param name="candidate">The candidate source text.</param>
  private void TrackNamePlatePrefetchCandidate(
      NamePlatePrefetchCandidate candidate)
  {
    if (!NamePlateTranslationPolicy.ShouldTranslateKind(candidate.Kind) ||
        string.IsNullOrWhiteSpace(candidate.OriginalText))
    {
      return;
    }

    var normalizedCandidate = candidate with
    {
      OriginalText = candidate.OriginalText.Trim(),
    };
    var nowUtc = DateTime.UtcNow;
    var key = BuildNamePlatePrefetchCandidateKey(normalizedCandidate);
    this.namePlatePrefetchCandidates.AddOrUpdate(
        key,
        _ => new NamePlatePrefetchCandidateState(
            normalizedCandidate,
            nowUtc),
        (_, existing) =>
        {
          existing.LastSeenUtc = nowUtc;
          return existing;
        });
  }

  /// <summary>
  ///     Ticks the nameplate prefetch runtime so recently observed world-object
  ///     names are translated into nameplate storage ahead of the next redraw.
  /// </summary>
  private void TickNamePlatePrefetch()
  {
    if (!this.ShouldPrefetchNamePlates() ||
        DateTime.UtcNow - this.namePlatePrefetchLastTickUtc <
        NamePlatePrefetchTickInterval)
    {
      return;
    }

    this.namePlatePrefetchLastTickUtc = DateTime.UtcNow;
    var candidates = this.CollectRecentNamePlatePrefetchCandidates();
    if (candidates.Count == 0)
    {
      this.namePlatePrefetchQueue.Clear();
      this.namePlatePrefetchQueueIndex = 0;
      this.namePlatePrefetchSignature = string.Empty;
      return;
    }

    var signature = string.Join(
        "|",
        candidates.Select(BuildNamePlatePrefetchCandidateKey));
    if (!string.Equals(
            this.namePlatePrefetchSignature,
            signature,
            StringComparison.Ordinal))
    {
      this.namePlatePrefetchSignature = signature;
      this.namePlatePrefetchQueue.Clear();
      this.namePlatePrefetchQueue.AddRange(candidates);
      this.namePlatePrefetchQueueIndex = 0;
    }

    if (this.namePlatePrefetchQueueIndex >= this.namePlatePrefetchQueue.Count)
    {
      return;
    }

    var processedCount = 0;
    while (processedCount < NamePlatePrefetchCandidatesPerTick &&
           this.namePlatePrefetchQueueIndex < this.namePlatePrefetchQueue.Count)
    {
      var candidate =
          this.namePlatePrefetchQueue[this.namePlatePrefetchQueueIndex++];
      this.PrefetchNamePlate(candidate);
      processedCount++;
    }
  }

  /// <summary>
  ///     Clears nameplate prefetch state.
  /// </summary>
  private void ClearNamePlatePrefetchState()
  {
    this.namePlatePrefetchQueue.Clear();
    this.namePlatePrefetchQueueIndex = 0;
    this.namePlatePrefetchSignature = string.Empty;
    this.namePlatePrefetchLastTickUtc = DateTime.MinValue;
    this.namePlatePrefetchCandidates.Clear();
  }

  /// <summary>
  ///     Gets whether nameplate prefetch should run in the current runtime
  ///     state.
  /// </summary>
  /// <returns><see langword="true" /> when background prefetch should run.</returns>
  private bool ShouldPrefetchNamePlates()
  {
    return this.configuration.Translate &&
           this.configuration.TranslateNamePlates &&
           FrameworkAccessGuard.IsClientReadyForPlayerScopedFrameworkAccess();
  }

  /// <summary>
  ///     Prefetches one observed nameplate candidate and schedules any missing
  ///     translation through the shared paced broker.
  /// </summary>
  /// <param name="candidate">The observed nameplate candidate.</param>
  private void PrefetchNamePlate(NamePlatePrefetchCandidate candidate)
  {
    var translationService = TranslationService;
    RunNamePlatePrefetchOperationEntry(
        candidate,
        ResolveCurrentPrefetchSourceLanguage,
        this.configuration,
        this.TryGetQueuedTranslation,
        this.QueueTranslation,
        (sourceText, capturedSource, targetLanguage, originContext) =>
            translationService.Translate(
                sourceText,
                capturedSource,
                targetLanguage,
                originContext: originContext),
        row => this.InsertNamePlateMessageData(row),
        out _,
        out _);
  }

  /// <summary>
  ///     Captures live scope and routes one nameplate prefetch through the
  ///     shared broker and persistence.
  /// </summary>
  /// <param name="candidate">The stable nameplate source candidate.</param>
  /// <param name="sourceLanguageResolver">Resolves the live client source.</param>
  /// <param name="configuration">The live translation configuration.</param>
  /// <param name="tryGetTranslation">The shared broker cache lookup.</param>
  /// <param name="queueTranslation">The shared broker queue operation.</param>
  /// <param name="translate">The production translation operation.</param>
  /// <param name="persistRow">The nameplate persistence operation.</param>
  /// <param name="sourceLanguage">The captured source contract.</param>
  /// <param name="scope">The captured reuse scope.</param>
  /// <returns>The production dispatch result.</returns>
  internal static PrefetchTranslationDispatchResult
      RunNamePlatePrefetchOperationEntry(
          NamePlatePrefetchCandidate candidate,
          Func<SourceClientLanguage?> sourceLanguageResolver,
          Config configuration,
          TryGetPrefetchTranslationDelegate tryGetTranslation,
          QueuePrefetchTranslationDelegate queueTranslation,
          ResolvePrefetchTranslationDelegate translate,
          Action<NamePlateMessage> persistRow,
          out SourceClientLanguage sourceLanguage,
          out TranslationReuseScope scope)
  {
    if (!NamePlateTranslationPolicy.ShouldTranslateKind(candidate.Kind) ||
        string.IsNullOrWhiteSpace(candidate.OriginalText) ||
        !TryCapturePrefetchOperationScope(
            sourceLanguageResolver,
            configuration,
            out sourceLanguage,
            out scope))
    {
      sourceLanguage = default;
      scope = default;
      return PrefetchTranslationDispatchResult.Rejected;
    }

    var originalText = candidate.OriginalText.Trim();
    var capturedSourceLanguage = sourceLanguage;
    var capturedScope = scope;
    if (NamePlateCacheManager.TryFindMatch(
            candidate.Kind,
            originalText,
            capturedScope) is { } existingRow &&
        !string.IsNullOrWhiteSpace(existingRow.TranslatedNamePlateText))
    {
      return PrefetchTranslationDispatchResult.Rejected;
    }

    var surfaceIdentity = BuildNamePlateOriginContext(candidate.Kind);
    return DispatchScopedPrefetchTranslation(
        BuildNamePlatePrefetchScopedTranslationKey(
            $"NamePlatePrefetch|{(int)candidate.Kind}|Name|{originalText}",
            capturedScope),
        capturedSourceLanguage,
        capturedScope,
        tryGetTranslation,
        queueTranslation,
        () => translate(
            originalText,
            capturedSourceLanguage,
            capturedScope.TargetLanguageCode,
            surfaceIdentity),
        (translatedName, capturedScope, _) =>
        {
          if (string.IsNullOrWhiteSpace(translatedName))
          {
            return;
          }

          persistRow(
              CreateTranslatedNamePlateMessage(
                  candidate.Kind,
                  originalText,
                  translatedName,
                  capturedScope));
        });
  }

  /// <summary>
  ///     Creates one translated nameplate row for captured background prefetch
  ///     scope.
  /// </summary>
  /// <param name="kind">The nameplate kind.</param>
  /// <param name="originalText">The original nameplate text.</param>
  /// <param name="translatedText">The translated nameplate text.</param>
  /// <param name="scope">The captured translation reuse scope.</param>
  /// <returns>The translated nameplate row.</returns>
  private static NamePlateMessage CreateTranslatedNamePlateMessage(
      NamePlateKind kind,
      string originalText,
      string translatedText,
      TranslationReuseScope scope)
  {
    var now = DateTime.Now;
    return new NamePlateMessage(
        (int)kind,
        originalText,
        scope.SourceLanguageCode,
        translatedText,
        scope.TargetLanguageCode,
        scope.TranslationEngine!.Value,
        now,
        now);
  }

  /// <summary>
  ///     Adds the complete operation scope to one nameplate payload key.
  /// </summary>
  /// <param name="payloadIdentity">The existing payload identity.</param>
  /// <param name="scope">The immutable operation reuse scope.</param>
  /// <returns>The source-scoped broker key.</returns>
  private static string BuildNamePlatePrefetchScopedTranslationKey(
      string payloadIdentity,
      TranslationReuseScope scope)
  {
    return BuildTranslationReuseScopedKey(payloadIdentity, scope);
  }

  /// <summary>
  ///     Builds the diagnostic surface identity for one nameplate name.
  /// </summary>
  /// <param name="kind">The nameplate kind.</param>
  /// <returns>The diagnostic surface identity.</returns>
  private static string BuildNamePlateOriginContext(NamePlateKind kind)
  {
    return $"NamePlate/{kind}/Name";
  }

  /// <summary>
  ///     Collects recent prefetch candidates while pruning stale entries.
  /// </summary>
  /// <returns>The recent candidates sorted by stable identity.</returns>
  private IReadOnlyList<NamePlatePrefetchCandidate>
      CollectRecentNamePlatePrefetchCandidates()
  {
    var cutoffUtc = DateTime.UtcNow - NamePlatePrefetchCandidateLifetime;
    List<NamePlatePrefetchCandidate> candidates = [];
    foreach (var pair in this.namePlatePrefetchCandidates.ToArray())
    {
      if (pair.Value.LastSeenUtc < cutoffUtc)
      {
        this.namePlatePrefetchCandidates.TryRemove(pair.Key, out _);
        continue;
      }

      candidates.Add(pair.Value.Candidate);
    }

    return candidates
        .OrderBy(BuildNamePlatePrefetchCandidateKey, StringComparer.Ordinal)
        .ToList();
  }

  /// <summary>
  ///     Builds a stable identity for one nameplate prefetch candidate.
  /// </summary>
  /// <param name="candidate">The candidate.</param>
  /// <returns>The stable candidate key.</returns>
  private static string BuildNamePlatePrefetchCandidateKey(
      NamePlatePrefetchCandidate candidate)
  {
    return $"{(int)candidate.Kind}|{candidate.OriginalText}";
  }

  /// <summary>
  ///     Tracks one recently observed nameplate candidate and its freshness
  ///     window for background prefetch.
  /// </summary>
  private sealed class NamePlatePrefetchCandidateState
  {
    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="NamePlatePrefetchCandidateState" /> class.
    /// </summary>
    /// <param name="candidate">The stable candidate source.</param>
    /// <param name="lastSeenUtc">The last time the source was observed.</param>
    internal NamePlatePrefetchCandidateState(
        NamePlatePrefetchCandidate candidate,
        DateTime lastSeenUtc)
    {
      this.Candidate = candidate;
      this.LastSeenUtc = lastSeenUtc;
    }

    /// <summary>
    ///     Gets the stable nameplate candidate.
    /// </summary>
    internal NamePlatePrefetchCandidate Candidate { get; }

    /// <summary>
    ///     Gets or sets the last time this candidate was observed.
    /// </summary>
    internal DateTime LastSeenUtc { get; set; }
  }
}
