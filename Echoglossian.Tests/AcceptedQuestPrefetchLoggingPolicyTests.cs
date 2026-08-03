// <copyright file="AcceptedQuestPrefetchLoggingPolicyTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers which accepted-quest prefetch phases are mirrored to the main
/// plugin log.
/// </summary>
public class AcceptedQuestPrefetchLoggingPolicyTests
{
    /// <summary>
    /// Ensures noisy existing-row skips stay out of the main plugin log so
    /// accepted-quest prefetch churn remains diagnostic-file only.
    /// </summary>
    [Fact]
    public void ShouldEmitAcceptedQuestPrefetchDalamudLog_TranslationSkipExisting_ReturnsFalse()
    {
        Assert.False(Echoglossian.ShouldEmitAcceptedQuestPrefetchDalamudLog("translation-skip-existing"));
    }

    /// <summary>
    /// Ensures the main log surfaces the canonical quest resolution summary so
    /// request processing is visible without opening the diagnostic file.
    /// </summary>
    [Fact]
    public void ShouldEmitAcceptedQuestPrefetchDalamudLog_Resolved_ReturnsTrue()
    {
        Assert.True(Echoglossian.ShouldEmitAcceptedQuestPrefetchDalamudLog("resolved"));
    }

    /// <summary>
    /// Ensures noisy empty-row skips remain diagnostic-file only.
    /// </summary>
    [Fact]
    public void ShouldEmitAcceptedQuestPrefetchDalamudLog_TranslationSkipEmpty_ReturnsFalse()
    {
        Assert.False(Echoglossian.ShouldEmitAcceptedQuestPrefetchDalamudLog("translation-skip-empty"));
    }
}
