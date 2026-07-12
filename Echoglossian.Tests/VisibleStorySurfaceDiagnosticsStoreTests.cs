// <copyright file="VisibleStorySurfaceDiagnosticsStoreTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers runtime-only storage for visible story-surface diagnostics.
/// </summary>
public class VisibleStorySurfaceDiagnosticsStoreTests
{
  /// <summary>
  ///     Ensures recording a snapshot updates the latest visible story-surface state.
  /// </summary>
  [Fact]
  public void Record_StoresLatestSnapshot()
  {
    VisibleStorySurfaceDiagnosticsStore.Clear();

    VisibleStorySurfaceDiagnosticsStore.Record(
        new VisibleStorySurfaceDiagnosticsSnapshot(
            VisibleStorySurfaceKind.TalkSubtitle,
            VisibleStorySurfaceProvenanceKind.DbReuse,
            "TalkSubtitleMessage",
            string.Empty,
            "Original subtitle",
            string.Empty,
            string.Empty,
            "Translated subtitle",
            string.Empty,
            false,
            2,
            DateTime.UtcNow,
            null,
            null));

    Assert.Equal(
        VisibleStorySurfaceKind.TalkSubtitle,
        VisibleStorySurfaceDiagnosticsStore.GetLatestSnapshot()!.Value.Surface);
  }

  /// <summary>
  ///     Ensures explicit retranslation outcome updates are retained for the same surface.
  /// </summary>
  [Fact]
  public void SetRetranslationOutcome_UpdatesLatestSnapshotForSameSurface()
  {
    VisibleStorySurfaceDiagnosticsStore.Clear();
    var observedAtUtc = new DateTime(2026, 07, 11, 15, 0, 0, DateTimeKind.Utc);
    VisibleStorySurfaceDiagnosticsStore.Record(
        new VisibleStorySurfaceDiagnosticsSnapshot(
            VisibleStorySurfaceKind.TextGimmickHint,
            VisibleStorySurfaceProvenanceKind.FreshLiveTranslation,
            "TextGimmickHintMessage",
            string.Empty,
            "Original hint",
            string.Empty,
            string.Empty,
            "Translated hint",
            string.Empty,
            false,
            8,
            observedAtUtc,
            null,
            null));

    VisibleStorySurfaceDiagnosticsStore.SetRetranslationOutcome(
        VisibleStorySurfaceKind.TextGimmickHint,
        true,
        VisibleStorySurfaceText.GetRetranslatedAndPersistedMessage(
            VisibleStorySurfaceKind.TextGimmickHint),
        observedAtUtc.AddMinutes(1));

    Assert.Equal(
        VisibleStorySurfaceText.GetRetranslatedAndPersistedMessage(
            VisibleStorySurfaceKind.TextGimmickHint),
        VisibleStorySurfaceDiagnosticsStore.GetLatestSnapshot()!
            .Value.LastRetranslationMessage);
  }
}
