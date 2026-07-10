// <copyright file="TranslatorMetricsCollector.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Concurrent;

namespace Echoglossian.Translators;

/// <summary>
///     Defines the aggregated outcome kinds recorded for translator runtime
///     metrics.
/// </summary>
public enum TranslationRequestMetricOutcome
{
  /// <summary>
  ///     A live translator request completed with a usable translated result.
  /// </summary>
  Success,

  /// <summary>
  ///     A live translator request completed but fell back to the original
  ///     text because the result was empty or failed classification.
  /// </summary>
  Failure,

  /// <summary>
  ///     A request was short-circuited before hitting the live translator.
  /// </summary>
  ShortCircuited,
}

/// <summary>
///     Represents one immutable aggregated runtime metrics snapshot for one
///     translation engine.
/// </summary>
/// <param name="EngineId">The persisted translation engine id.</param>
/// <param name="EngineName">The displayable engine name.</param>
/// <param name="ProviderName">The provider family or service label.</param>
/// <param name="ModelName">The active model label when applicable.</param>
/// <param name="LiveRequestCount">The number of live translator requests issued.</param>
/// <param name="ContextAwareRequestCount">
/// The number of live translator requests that consumed runtime-only short-lived dialogue context.
/// </param>
/// <param name="StructuredRequestCount">
/// The number of requests that attempted the structured dialogue path.
/// </param>
/// <param name="StructuredSuccessCount">
/// The number of structured dialogue attempts that produced an accepted structured payload.
/// </param>
/// <param name="GlossaryAugmentedStructuredRequestCount">
/// The number of structured dialogue attempts that carried one or more glossary rows.
/// </param>
/// <param name="SuccessCount">The number of successful translated results.</param>
/// <param name="FailureCount">The number of live requests that fell back.</param>
/// <param name="ShortCircuitCount">
/// The number of requests suppressed before live translation.
/// </param>
/// <param name="AverageLatencyMs">The average latency of live requests.</param>
/// <param name="MaxLatencyMs">The longest observed live request latency.</param>
/// <param name="LastLatencyMs">The most recent live request latency.</param>
/// <param name="LastRequestAtUtc">The time of the most recent activity.</param>
/// <param name="LastSuccessAtUtc">The time of the most recent successful translation.</param>
/// <param name="LastFailureAtUtc">The time of the most recent failed or suppressed request.</param>
/// <param name="LastFailureReason">The last observed failure detail.</param>
public readonly record struct TranslatorMetricsSnapshot(
    int EngineId,
    string EngineName,
    string? ProviderName,
    string? ModelName,
    long LiveRequestCount,
    long ContextAwareRequestCount,
    long StructuredRequestCount,
    long StructuredSuccessCount,
    long GlossaryAugmentedStructuredRequestCount,
    long SuccessCount,
    long FailureCount,
    long ShortCircuitCount,
    double AverageLatencyMs,
    double MaxLatencyMs,
    double LastLatencyMs,
    DateTime? LastRequestAtUtc,
    DateTime? LastSuccessAtUtc,
    DateTime? LastFailureAtUtc,
    string? LastFailureReason,
    string? LastStructuredFailureReason);

/// <summary>
///     Aggregates lightweight runtime translator metrics in memory without
///     emitting hot-path logs.
/// </summary>
public static class TranslatorMetricsCollector
{
  private static readonly ConcurrentDictionary<int, TranslatorMetricsBucket> Buckets = new();

  /// <summary>
  ///     Records one aggregated translator activity sample.
  /// </summary>
  /// <param name="engineId">The translation engine id.</param>
  /// <param name="outcome">The aggregated outcome kind.</param>
  /// <param name="latency">The live request latency, or <see cref="TimeSpan.Zero" /> for short-circuits.</param>
  /// <param name="failureReason">Optional failure detail.</param>
  /// <param name="usedDialogueContext">
  /// Whether the live request consumed runtime-only short-lived dialogue context.
  /// </param>
  /// <param name="observedAtUtc">Optional explicit observation time for tests.</param>
  public static void Record(
      int engineId,
      TranslationRequestMetricOutcome outcome,
      TimeSpan latency,
      string? failureReason = null,
      bool usedDialogueContext = false,
      DateTime? observedAtUtc = null)
  {
    var bucket = Buckets.GetOrAdd(engineId, _ => new TranslatorMetricsBucket());
    bucket.Record(
        outcome,
        latency,
        failureReason,
        usedDialogueContext,
        observedAtUtc ?? DateTime.UtcNow);
  }

  /// <summary>
  ///     Records one structured dialogue attempt so the debugger can expose
  ///     how often the structured path and glossary augmentation are active.
  /// </summary>
  /// <param name="engineId">The translation engine id.</param>
  /// <param name="succeeded">
  /// Whether the structured path produced an accepted structured payload.
  /// </param>
  /// <param name="usedGlossary">
  /// Whether the structured request carried one or more glossary rows.
  /// </param>
  /// <param name="failureReason">
  /// Optional structured-path failure detail when the attempt fell back.
  /// </param>
  /// <param name="observedAtUtc">Optional explicit observation time for tests.</param>
  public static void RecordStructuredAttempt(
      int engineId,
      bool succeeded,
      bool usedGlossary,
      string? failureReason = null,
      DateTime? observedAtUtc = null)
  {
    var bucket = Buckets.GetOrAdd(engineId, _ => new TranslatorMetricsBucket());
    bucket.RecordStructuredAttempt(
        succeeded,
        usedGlossary,
        failureReason,
        observedAtUtc ?? DateTime.UtcNow);
  }

  /// <summary>
  ///     Updates display metadata for one translation engine so the debugger
  ///     window can show the active provider and model labels.
  /// </summary>
  /// <param name="engineId">The translation engine id.</param>
  /// <param name="providerName">The provider family or service label.</param>
  /// <param name="modelName">The active model label when applicable.</param>
  public static void DescribeEngine(int engineId, string? providerName, string? modelName)
  {
    var bucket = Buckets.GetOrAdd(engineId, _ => new TranslatorMetricsBucket());
    bucket.Describe(providerName, modelName);
  }

  /// <summary>
  ///     Gets immutable snapshots for every engine with observed activity.
  /// </summary>
  /// <returns>The ordered metrics snapshot list.</returns>
  public static IReadOnlyList<TranslatorMetricsSnapshot> GetSnapshots()
  {
    return Buckets
        .Select(kvp => kvp.Value.CreateSnapshot(kvp.Key))
        .OrderBy(snapshot => snapshot.EngineId)
        .ToList();
  }

  /// <summary>
  ///     Clears all in-memory translator runtime metrics.
  /// </summary>
  public static void Clear()
  {
    Buckets.Clear();
  }

  private sealed class TranslatorMetricsBucket
  {
    private readonly Lock syncRoot = new();
    private long contextAwareRequestCount;
    private long failureCount;
    private DateTime? lastFailureAtUtc;
    private string? lastFailureReason;
    private double lastLatencyMs;
    private DateTime? lastRequestAtUtc;
    private DateTime? lastSuccessAtUtc;
    private long liveRequestCount;
    private double maxLatencyMs;
    private long glossaryAugmentedStructuredRequestCount;
    private long shortCircuitCount;
    private long successCount;
    private string? lastStructuredFailureReason;
    private long structuredRequestCount;
    private long structuredSuccessCount;
    private double totalLatencyMs;
    private string? modelName;
    private string? providerName;

    public void Describe(string? providerName, string? modelName)
    {
      lock (this.syncRoot)
      {
        this.providerName = string.IsNullOrWhiteSpace(providerName) ? null : providerName;
        this.modelName = string.IsNullOrWhiteSpace(modelName) ? null : modelName;
      }
    }

    public void Record(
        TranslationRequestMetricOutcome outcome,
        TimeSpan latency,
        string? failureReason,
        bool usedDialogueContext,
        DateTime observedAtUtc)
    {
      lock (this.syncRoot)
      {
        this.lastRequestAtUtc = observedAtUtc;

        if (outcome == TranslationRequestMetricOutcome.ShortCircuited)
        {
          this.shortCircuitCount++;
          this.lastFailureAtUtc = observedAtUtc;
          this.lastFailureReason = failureReason;
          return;
        }

        var latencyMs = latency.TotalMilliseconds;
        this.liveRequestCount++;
        if (usedDialogueContext)
        {
          this.contextAwareRequestCount++;
        }

        this.lastLatencyMs = latencyMs;
        this.totalLatencyMs += latencyMs;
        if (latencyMs > this.maxLatencyMs)
        {
          this.maxLatencyMs = latencyMs;
        }

        if (outcome == TranslationRequestMetricOutcome.Success)
        {
          this.successCount++;
          this.lastSuccessAtUtc = observedAtUtc;
          return;
        }

        this.failureCount++;
        this.lastFailureAtUtc = observedAtUtc;
        this.lastFailureReason = failureReason;
      }
    }

    public void RecordStructuredAttempt(
        bool succeeded,
        bool usedGlossary,
        string? failureReason,
        DateTime observedAtUtc)
    {
      lock (this.syncRoot)
      {
        this.lastRequestAtUtc = observedAtUtc;
        this.structuredRequestCount++;
        if (usedGlossary)
        {
          this.glossaryAugmentedStructuredRequestCount++;
        }

        if (succeeded)
        {
          this.structuredSuccessCount++;
          return;
        }

        this.lastStructuredFailureReason = failureReason;
      }
    }

    public TranslatorMetricsSnapshot CreateSnapshot(int engineId)
    {
      lock (this.syncRoot)
      {
        var averageLatencyMs = this.liveRequestCount == 0
            ? 0d
            : this.totalLatencyMs / this.liveRequestCount;
        var engineName = Enum.IsDefined(typeof(Echoglossian.TransEngines), engineId)
            ? ((Echoglossian.TransEngines)engineId).ToString()
            : $"Engine {engineId}";
        return new TranslatorMetricsSnapshot(
            engineId,
            engineName,
            this.providerName,
            this.modelName,
            this.liveRequestCount,
            this.contextAwareRequestCount,
            this.structuredRequestCount,
            this.structuredSuccessCount,
            this.glossaryAugmentedStructuredRequestCount,
            this.successCount,
            this.failureCount,
            this.shortCircuitCount,
            averageLatencyMs,
            this.maxLatencyMs,
            this.lastLatencyMs,
            this.lastRequestAtUtc,
            this.lastSuccessAtUtc,
            this.lastFailureAtUtc,
            this.lastFailureReason,
            this.lastStructuredFailureReason);
      }
    }
  }
}
