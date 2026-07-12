// <copyright file="VisibleStorySurfaceDiagnosticsStore.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Stores runtime-only visible story-surface diagnostics for the debugger.
/// </summary>
public static class VisibleStorySurfaceDiagnosticsStore
{
  private static readonly object SyncRoot = new();
  private static readonly Dictionary<VisibleStorySurfaceKind, VisibleStorySurfaceDiagnosticsSnapshot>
      SnapshotsBySurface = new();

  private static VisibleStorySurfaceKind? latestSurface;

  /// <summary>
  /// Clears all retained diagnostics snapshots.
  /// </summary>
  public static void Clear()
  {
    lock (SyncRoot)
    {
      SnapshotsBySurface.Clear();
      latestSurface = null;
    }
  }

  /// <summary>
  /// Clears the retained diagnostics snapshot for one visible story surface.
  /// </summary>
  /// <param name="surface">The surface to clear.</param>
  public static void Clear(VisibleStorySurfaceKind surface)
  {
    lock (SyncRoot)
    {
      SnapshotsBySurface.Remove(surface);
      if (latestSurface == surface)
      {
        latestSurface = null;
      }
    }
  }

  /// <summary>
  /// Records the latest diagnostics snapshot for a visible story surface.
  /// </summary>
  /// <param name="snapshot">The snapshot to retain.</param>
  public static void Record(VisibleStorySurfaceDiagnosticsSnapshot snapshot)
  {
    lock (SyncRoot)
    {
      if (SnapshotsBySurface.TryGetValue(snapshot.Surface, out var existingSnapshot) &&
          snapshot.LastRetranslationSuccess == null &&
          string.IsNullOrWhiteSpace(snapshot.LastRetranslationMessage))
      {
        snapshot = snapshot with
        {
          LastRetranslationSuccess = existingSnapshot.LastRetranslationSuccess,
          LastRetranslationMessage = existingSnapshot.LastRetranslationMessage,
        };
      }

      SnapshotsBySurface[snapshot.Surface] = snapshot;
      latestSurface = snapshot.Surface;
    }
  }

  /// <summary>
  /// Updates the latest explicit retranslation outcome for one story surface.
  /// </summary>
  /// <param name="surface">The surface that handled the request.</param>
  /// <param name="success">Whether the explicit retranslation succeeded.</param>
  /// <param name="message">The user-facing outcome message.</param>
  /// <param name="observedAtUtc">The UTC timestamp for the outcome.</param>
  public static void SetRetranslationOutcome(
      VisibleStorySurfaceKind surface,
      bool success,
      string message,
      DateTime observedAtUtc)
  {
    lock (SyncRoot)
    {
      if (!SnapshotsBySurface.TryGetValue(surface, out var snapshot))
      {
        return;
      }

      SnapshotsBySurface[surface] = snapshot with
      {
        LastRetranslationSuccess = success,
        LastRetranslationMessage = message,
        ObservedAtUtc = observedAtUtc,
      };
    }
  }

  /// <summary>
  /// Gets the latest snapshot across all visible story surfaces.
  /// </summary>
  /// <returns>The latest snapshot, or <see langword="null"/> when none exist.</returns>
  public static VisibleStorySurfaceDiagnosticsSnapshot? GetLatestSnapshot()
  {
    lock (SyncRoot)
    {
      if (latestSurface == null ||
          !SnapshotsBySurface.TryGetValue(latestSurface.Value, out var snapshot))
      {
        return null;
      }

      return snapshot;
    }
  }
}
