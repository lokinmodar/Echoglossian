// <copyright file="DbFirstPreDrawRefreshPolicy.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Common;

/// <summary>
/// Centralizes the pre-draw short-circuit decision for DB-first addon handlers.
/// </summary>
internal static class DbFirstPreDrawRefreshPolicy
{
    /// <summary>
    /// Determines whether the current pre-draw pass can skip a full DB-first
    /// refresh and keep the existing applied state.
    /// </summary>
    /// <param name="sameDisplayMode">
    /// Whether the effective display mode still matches the last applied mode.
    /// </param>
    /// <param name="shouldContinueAppliedStateRefresh">
    /// Whether the handler is still inside its bounded post-lifecycle refresh
    /// window.
    /// </param>
    /// <param name="refreshRequested">
    /// Whether background translation work explicitly requested one fresh
    /// re-resolution pass.
    /// </param>
    /// <param name="hasRuntimeState">
    /// Whether translated native state is currently applied.
    /// </param>
    /// <param name="usesHoverTooltips">
    /// Whether the current display mode still depends on hover registrations.
    /// </param>
    /// <param name="hasLastResolvedState">
    /// Whether one prior resolved payload pair exists for tooltip refresh.
    /// </param>
    /// <param name="requiresHoverTooltipLifetimeRefresh">
    /// Whether the current display mode still depends on one visible addon to
    /// keep tooltip lifetime refreshed, including custom hover fallback.
    /// </param>
    /// <param name="hasVisibleAddon">
    /// Whether the addon that owns the current hover targets is still visible.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the handler may short-circuit the current
    /// pre-draw pass; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool ShouldShortCircuit(
        bool sameDisplayMode,
        bool shouldContinueAppliedStateRefresh,
        bool refreshRequested,
        bool hasRuntimeState,
        bool usesHoverTooltips,
        bool hasLastResolvedState,
        bool requiresHoverTooltipLifetimeRefresh = false,
        bool hasVisibleAddon = true)
    {
        if (!sameDisplayMode ||
            shouldContinueAppliedStateRefresh ||
            refreshRequested ||
            (requiresHoverTooltipLifetimeRefresh && !hasVisibleAddon))
        {
            return false;
        }

        return hasRuntimeState ||
               (usesHoverTooltips && hasLastResolvedState);
    }
}
