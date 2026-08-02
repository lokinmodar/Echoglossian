// <copyright file="NamePlateTranslationRuntimeRegistration.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.NamePlates;

namespace Echoglossian;

/// <summary>
///     Handles bootstrap and teardown for the NamePlateGui translation runtime.
/// </summary>
public partial class Echoglossian
{
  /// <summary>
  ///     Creates the NamePlateGui translation runtime using shared translation,
  ///     persistence, and overlay services.
  /// </summary>
  /// <returns>The initialized nameplate runtime.</returns>
  private NamePlateTranslationRuntime CreateNamePlateTranslationRuntime()
  {
    return new NamePlateTranslationRuntime(
        this.configuration,
        TranslationService,
        this.TrackNamePlatePrefetchCandidate,
        text => this.RemoveDiacritics(
            text,
            this.SpecialCharsSupportedByGameFont));
  }

  /// <summary>
  ///     Registers the NamePlateGui runtime with Dalamud.
  /// </summary>
  private void RegisterNamePlateTranslationRuntime()
  {
    NamePlateGuiInterface.OnNamePlateUpdate +=
        this.namePlateTranslationRuntime.HandleNamePlateUpdate;
  }

  /// <summary>
  ///     Unregisters the NamePlateGui runtime from Dalamud.
  /// </summary>
  private void UnregisterNamePlateTranslationRuntime()
  {
    NamePlateGuiInterface.OnNamePlateUpdate -=
        this.namePlateTranslationRuntime.HandleNamePlateUpdate;
  }
}
