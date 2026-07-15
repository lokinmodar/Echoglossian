// <copyright file="TranslatorMetricsCollectorTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers in-memory aggregation of translator runtime metrics.
/// </summary>
public class TranslatorMetricsCollectorTests
{
  /// <summary>
  ///     Ensures success, failure, and short-circuit samples aggregate into one
  ///     per-engine snapshot without using hot-path logs.
  /// </summary>
  [Fact]
  public void Record_AggregatesOutcomesPerEngine()
  {
    TranslatorMetricsCollector.Clear();
    var observedAtUtc = new DateTime(2026, 05, 12, 12, 0, 0, DateTimeKind.Utc);
    TranslatorMetricsCollector.DescribeEngine(
        (int)Echoglossian.TransEngines.Ollama,
        "Ollama",
        "llama3");

    TranslatorMetricsCollector.Record(
        (int)Echoglossian.TransEngines.Ollama,
        TranslationRequestMetricOutcome.Success,
        TimeSpan.FromMilliseconds(120),
        usedDialogueContext: true,
        observedAtUtc: observedAtUtc);
    TranslatorMetricsCollector.RecordStructuredAttempt(
        (int)Echoglossian.TransEngines.Ollama,
        succeeded: true,
        usedGlossary: true,
        observedAtUtc: observedAtUtc);
    TranslatorMetricsCollector.RecordStructuredAttempt(
        (int)Echoglossian.TransEngines.Ollama,
        succeeded: false,
        usedGlossary: false,
        failureReason: "structured-json-invalid",
        observedAtUtc: observedAtUtc.AddMilliseconds(500));
    TranslatorMetricsCollector.Record(
        (int)Echoglossian.TransEngines.Ollama,
        TranslationRequestMetricOutcome.Failure,
        TimeSpan.FromMilliseconds(240),
        failureReason: "llm-timeout",
        observedAtUtc: observedAtUtc.AddSeconds(1));
    TranslatorMetricsCollector.Record(
        (int)Echoglossian.TransEngines.Ollama,
        TranslationRequestMetricOutcome.ShortCircuited,
        TimeSpan.Zero,
        failureReason: "known-failure-cache",
        observedAtUtc: observedAtUtc.AddSeconds(2));

    var snapshot = Assert.Single(TranslatorMetricsCollector.GetSnapshots());

    Assert.Equal((int)Echoglossian.TransEngines.Ollama, snapshot.EngineId);
    Assert.Equal("Ollama", snapshot.ProviderName);
    Assert.Equal("llama3", snapshot.ModelName);
    Assert.Equal(2, snapshot.LiveRequestCount);
    Assert.Equal(1, snapshot.ContextAwareRequestCount);
    Assert.Equal(2, snapshot.StructuredRequestCount);
    Assert.Equal(1, snapshot.StructuredSuccessCount);
    Assert.Equal(1, snapshot.GlossaryAugmentedStructuredRequestCount);
    Assert.Equal(1, snapshot.SuccessCount);
    Assert.Equal(1, snapshot.FailureCount);
    Assert.Equal(1, snapshot.ShortCircuitCount);
    Assert.Equal(180d, snapshot.AverageLatencyMs);
    Assert.Equal(240d, snapshot.MaxLatencyMs);
    Assert.Equal("known-failure-cache", snapshot.LastFailureReason);
    Assert.Equal("structured-json-invalid", snapshot.LastStructuredFailureReason);
    Assert.Equal(observedAtUtc.AddSeconds(2), snapshot.LastRequestAtUtc);
  }

  /// <summary>
  ///     Ensures clearing the collector removes all retained snapshots.
  /// </summary>
  [Fact]
  public void Clear_RemovesAllSnapshots()
  {
    TranslatorMetricsCollector.Clear();
    TranslatorMetricsCollector.Record(
        (int)Echoglossian.TransEngines.ChatGPT,
        TranslationRequestMetricOutcome.Success,
        TimeSpan.FromMilliseconds(10));

    TranslatorMetricsCollector.Clear();

    Assert.Empty(TranslatorMetricsCollector.GetSnapshots());
  }
}
