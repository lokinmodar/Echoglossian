// <copyright file="DialogueGlossaryTermProtectorTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators;
using Echoglossian.Translators.Helpers;

using FluentAssertions;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers deterministic dialogue glossary marker protection and restoration.
/// </summary>
public class DialogueGlossaryTermProtectorTests
{
    /// <summary>
    ///     Ensures protection prefers the longest matching phrase and restores
    ///     each protected occurrence to the configured target text.
    /// </summary>
    [Fact]
    public void Protect_LongestMatchFirst_RestoresConfiguredTargets()
    {
        StructuredDialogueGlossaryEntry[] glossary =
        [
            new("Twin Adder", "Viper Order", null, null, null),
            new("The Order of the Twin Adder", "The Twin Serpent Order", null, null, null),
        ];

        var protection = DialogueGlossaryTermProtector.Protect(
            "The Order of the Twin Adder will brief every Twin Adder recruit.",
            glossary);

        protection.Occurrences.Should().HaveCount(2);
        protection.ProtectedText.Should().NotContain("The Order of the Twin Adder");
        protection.ProtectedText.Should().NotContain("Twin Adder recruit");

        var restoreResult = DialogueGlossaryTermProtector.TryRestore(
            protection.ProtectedText,
            protection);

        restoreResult.Succeeded.Should().BeTrue();
        restoreResult.RestoredText.Should().Be(
            "The Twin Serpent Order will brief every Viper Order recruit.");
    }

    /// <summary>
    ///     Ensures protection does not replace glossary terms embedded inside
    ///     larger unrelated words.
    /// </summary>
    [Fact]
    public void Protect_DoesNotReplaceInsideUnrelatedWords()
    {
        StructuredDialogueGlossaryEntry[] glossary =
        [
            new("Scions", "Scions [GLOSSARY-OK]", null, null, null),
        ];

        var protection = DialogueGlossaryTermProtector.Protect(
            "Scions and HyperScions remain distinct.",
            glossary);

        var restoreResult = DialogueGlossaryTermProtector.TryRestore(
            protection.ProtectedText,
            protection);

        restoreResult.Succeeded.Should().BeTrue();
        restoreResult.RestoredText.Should().Be(
            "Scions [GLOSSARY-OK] and HyperScions remain distinct.");
    }

    /// <summary>
    ///     Ensures restoration rejects provider output that removes a required
    ///     protected marker.
    /// </summary>
    [Fact]
    public void TryRestore_MissingMarker_FailsWithStableReason()
    {
        StructuredDialogueGlossaryEntry[] glossary =
        [
            new("Triple Triad", "Triple Triad [GLOSSARY-OK]", null, null, null),
        ];

        var protection = DialogueGlossaryTermProtector.Protect(
            "Triple Triad is popular here.",
            glossary);

        var restoreResult = DialogueGlossaryTermProtector.TryRestore(
            "This provider removed the marker entirely.",
            protection);

        restoreResult.Succeeded.Should().BeFalse();
        restoreResult.FailureReason.Should().Be("missing-required-marker");
    }
}
