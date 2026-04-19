// <copyright file="CharacterReputeSubWindowHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Common;
using Echoglossian.NativeUI.Helpers;

namespace Echoglossian.NativeUI.AddonHandlers.Character;

/// <summary>
///     Handles translation for the "CharacterClass" addon using AtkValues and
///     StringArrayData.
///     Lifecycle-safe: extracts and applies values within valid memory scope per
///     frame.
/// </summary>
public class CharacterReputeSubWindowHandler : DbFirstGameWindowAddonHandler
{
  /// <summary>
  ///     Initializes a new instance of the
  ///     <see cref="CharacterReputeSubWindowHandler" /> class.
  /// </summary>
  /// <param name="config">The configuration settings for the plugin.</param>
  /// <param name="hoverTooltipManager">The shared hover-tooltip manager.</param>
  /// <param name="translationService">The service used for translating text.</param>
  public CharacterReputeSubWindowHandler(
      Config config,
      HoverTooltipManager hoverTooltipManager,
      TranslationService translationService)
      : base(
          addonName: "CharacterRepute",
          config: config,
          hoverTooltipManager: hoverTooltipManager,
          translationService: translationService,
          enabledSelector: static configuration =>
              configuration.TranslateCharacterWindow,
          useAtkValues: true,
          stringArrayDataType: StringArrayType.Character,
          displayModeSelector: static configuration =>
              configuration.CharacterWindowTranslationDisplayMode)
    {
    }

    /// <inheritdoc />
    protected override bool ShouldCaptureStringArrayValues(
        byte subscribedAddonsCount)
    {
        return subscribedAddonsCount <= 1;
    }
}
