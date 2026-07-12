// <copyright file="VisibleStorySurfaceTextTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers localized visible story-surface text helpers.
/// </summary>
public class VisibleStorySurfaceTextTests
{
  /// <summary>
  ///     Ensures unsupported story surfaces fail loudly instead of silently
  ///     reusing the Talk label.
  /// </summary>
  [Fact]
  public void ResolveSurfaceName_ThrowsForUnknownSurface()
  {
    Assert.Throws<ArgumentOutOfRangeException>(
        () => VisibleStorySurfaceText.ResolveSurfaceName(
            (VisibleStorySurfaceKind)999));
  }

  /// <summary>
  ///     Ensures unsupported provenance kinds fail loudly instead of silently
  ///     reusing a known provenance label.
  /// </summary>
  [Fact]
  public void ResolveProvenanceLabel_ThrowsForUnknownProvenance()
  {
    Assert.Throws<ArgumentOutOfRangeException>(
        () => VisibleStorySurfaceText.ResolveProvenanceLabel(
            (VisibleStorySurfaceProvenanceKind)999));
  }
}
