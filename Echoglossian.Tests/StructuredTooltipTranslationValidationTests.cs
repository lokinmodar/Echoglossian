// <copyright file="StructuredTooltipTranslationValidationTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers source-equivalence handling for structured tooltip translations.
/// </summary>
public class StructuredTooltipTranslationValidationTests
{
    /// <summary>
    ///     Ensures a translated description copied from the source is not
    ///     accepted as a complete structured tooltip translation.
    /// </summary>
    [Fact]
    public void HasCompleteMeaningfulTranslation_RejectsSourceEquivalentDescription()
    {
        Assert.False(
            StructuredTooltipTranslationValidation
                .HasCompleteMeaningfulTranslation(
                    "Standard Step",
                    "Begin dancing, granting yourself Standard Step.",
                    "Passo padrão",
                    "Begin dancing, granting yourself Standard Step."));
    }

    /// <summary>
    ///     Ensures a proper noun may remain unchanged when the description
    ///     itself is translated.
    /// </summary>
    [Fact]
    public void HasCompleteMeaningfulTranslation_AcceptsSourceEquivalentNameWithTranslatedDescription()
    {
        Assert.True(
            StructuredTooltipTranslationValidation
                .HasCompleteMeaningfulTranslation(
                    "Aetheryte",
                    "A crystal that teleports the player.",
                    "Aetheryte",
                    "Um cristal que teletransporta o jogador."));
    }
}
