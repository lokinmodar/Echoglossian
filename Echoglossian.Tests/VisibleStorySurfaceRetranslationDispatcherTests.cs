// <copyright file="VisibleStorySurfaceRetranslationDispatcherTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Talk;
using Echoglossian.NativeUI.Handlers;
using Echoglossian.NativeUI.Helpers;
using Echoglossian.PluginUI.Helpers;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers explicit visible story-surface retranslation dispatch across
///     registered addon handlers.
/// </summary>
public class VisibleStorySurfaceRetranslationDispatcherTests
{
  /// <summary>
  ///     Ensures the dispatcher returns the first applicable handler result.
  /// </summary>
  [Fact]
  public async Task DispatchAsync_ReturnsFirstApplicableResult()
  {
    var dispatcher = new VisibleStorySurfaceRetranslationDispatcher();
    var handlers = new List<(string AddonName, IAddonTranslationHandler Handler)>
    {
      ("Talk", new FakeRetranslationHandler(
          false,
          false,
          VisibleStorySurfaceKind.Talk,
          VisibleStorySurfaceText.ResolveSurfaceName(
              VisibleStorySurfaceKind.Talk))),
      ("TalkSubtitle", new FakeRetranslationHandler(
          true,
          true,
          VisibleStorySurfaceKind.TalkSubtitle,
          VisibleStorySurfaceText.ResolveSurfaceName(
              VisibleStorySurfaceKind.TalkSubtitle))),
    };

    var result = await dispatcher.DispatchAsync(handlers);

    Assert.True(result.IsApplicable);
    Assert.Equal(VisibleStorySurfaceKind.TalkSubtitle, result.Surface);
    Assert.Equal(
        VisibleStorySurfaceText.ResolveSurfaceName(
            VisibleStorySurfaceKind.TalkSubtitle),
        result.SurfaceName);
  }

  private sealed class FakeRetranslationHandler :
      IAddonTranslationHandler,
      IVisibleDialogueRetranslationHandler
  {
    private readonly VisibleDialogueRetranslationResult result;

    public FakeRetranslationHandler(
        bool applicable,
        bool success,
        VisibleStorySurfaceKind surface,
        string surfaceName)
    {
      this.result = new VisibleDialogueRetranslationResult(
          applicable,
          success,
          surface,
          surfaceName,
          surfaceName + " result");
    }

    public Dictionary<AddonEvent, IAddonLifecycle.AddonEventDelegate> GetEventHandlers()
    {
      return new Dictionary<AddonEvent, IAddonLifecycle.AddonEventDelegate>();
    }

    public Task<VisibleDialogueRetranslationResult> RetranslateVisibleTextAndPersistAsync()
    {
      return Task.FromResult(this.result);
    }
  }
}
