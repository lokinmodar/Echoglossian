// <copyright file="IVisibleDialogueRetranslationHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Talk;

/// <summary>
///     Defines the runtime contract for explicitly retranslating the currently
///     visible dialogue line and persisting the refreshed result.
/// </summary>
public interface IVisibleDialogueRetranslationHandler
{
  /// <summary>
  ///     Retranslates the currently visible dialogue line using the active
  ///     translator configuration and persists the refreshed result when the
  ///     output is usable.
  /// </summary>
  /// <returns>
  ///     A <see cref="VisibleDialogueRetranslationResult" /> describing whether
  ///     this handler had an active visible line, whether retranslating it
  ///     succeeded, and the user-facing outcome message.
  /// </returns>
  Task<VisibleDialogueRetranslationResult> RetranslateVisibleTextAndPersistAsync();
}

/// <summary>
///     Describes the outcome of one explicit visible-dialogue retranslation
///     request.
/// </summary>
/// <param name="IsApplicable">
/// Whether the handler had an active visible dialogue line to operate on.
/// </param>
/// <param name="Success">
/// Whether the visible retranslation completed successfully for the current
/// line.
/// </param>
/// <param name="SurfaceName">The surface that handled the request.</param>
/// <param name="Message">The user-facing outcome message.</param>
public readonly record struct VisibleDialogueRetranslationResult(
    bool IsApplicable,
    bool Success,
    string SurfaceName,
    string Message);
