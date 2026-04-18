// <copyright file="DbFirstPayloadRecoveryHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Common;
using Echoglossian.NativeUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers recovery of canonical DB-first originals when the live UI is
///     already showing translated or mixed payload state.
/// </summary>
public class DbFirstPayloadRecoveryHelperTests
{
    /// <summary>
    ///     Ensures a fully translated live payload recovers the persisted
    ///     original payload.
    /// </summary>
    [Fact]
    public void TryRecoverOriginalPayload_RecoversOriginal_WhenLiveMatchesTranslated()
    {
        var original = CreatePayload(
            atkValues: new Dictionary<int, string>
            {
                [1] = "Profile",
            },
            stringArrayValues: new Dictionary<int, string>
            {
                [0] = "Grand Company",
            });
        var translated = CreatePayload(
            atkValues: new Dictionary<int, string>
            {
                [1] = "Perfil",
            },
            stringArrayValues: new Dictionary<int, string>
            {
                [0] = "Grande Companhia",
            });

        var resolved = DbFirstPayloadRecoveryHelper.TryRecoverOriginalPayload(
            translated,
            new[]
            {
                new DbFirstPayloadRecoveryCandidate(original, translated),
            },
            out var recoveredOriginal);

        Assert.True(resolved);
        Assert.Equal(original, recoveredOriginal);
    }

    /// <summary>
    ///     Ensures a mixed live payload still recovers the persisted original
    ///     payload when every slot matches either original or translated text.
    /// </summary>
    [Fact]
    public void TryRecoverOriginalPayload_RecoversOriginal_WhenLiveMatchesMixedState()
    {
        var original = CreatePayload(
            atkValues: new Dictionary<int, string>
            {
                [1] = "Profile",
                [2] = "Titles Acquired",
            },
            stringArrayValues: new Dictionary<int, string>
            {
                [0] = "Grand Company",
            });
        var translated = CreatePayload(
            atkValues: new Dictionary<int, string>
            {
                [1] = "Perfil",
                [2] = "Titulos Obtidos",
            },
            stringArrayValues: new Dictionary<int, string>
            {
                [0] = "Grande Companhia",
            });
        var mixedLivePayload = CreatePayload(
            atkValues: new Dictionary<int, string>
            {
                [1] = "Perfil",
                [2] = "Titles Acquired",
            },
            stringArrayValues: new Dictionary<int, string>
            {
                [0] = "Grande Companhia",
            });

        var resolved = DbFirstPayloadRecoveryHelper.TryRecoverOriginalPayload(
            mixedLivePayload,
            new[]
            {
                new DbFirstPayloadRecoveryCandidate(original, translated),
            },
            out var recoveredOriginal);

        Assert.True(resolved);
        Assert.Equal(original, recoveredOriginal);
    }

    /// <summary>
    ///     Ensures ambiguous candidates do not recover the wrong original
    ///     payload.
    /// </summary>
    [Fact]
    public void TryRecoverOriginalPayload_ReturnsFalse_WhenCandidatesAreAmbiguous()
    {
        var livePayload = CreatePayload(
            atkValues: new Dictionary<int, string>
            {
                [1] = "Perfil",
            },
            stringArrayValues: new Dictionary<int, string>());

        var firstOriginal = CreatePayload(
            atkValues: new Dictionary<int, string>
            {
                [1] = "Profile",
            },
            stringArrayValues: new Dictionary<int, string>());
        var secondOriginal = CreatePayload(
            atkValues: new Dictionary<int, string>
            {
                [1] = "Overview",
            },
            stringArrayValues: new Dictionary<int, string>());
        var translated = CreatePayload(
            atkValues: new Dictionary<int, string>
            {
                [1] = "Perfil",
            },
            stringArrayValues: new Dictionary<int, string>());

        var resolved = DbFirstPayloadRecoveryHelper.TryRecoverOriginalPayload(
            livePayload,
            new[]
            {
                new DbFirstPayloadRecoveryCandidate(firstOriginal, translated),
                new DbFirstPayloadRecoveryCandidate(secondOriginal, translated),
            },
            out _);

        Assert.False(resolved);
    }

    /// <summary>
    ///     Creates one payload for test usage.
    /// </summary>
    /// <param name="atkValues">The ATK values.</param>
    /// <param name="stringArrayValues">The string-array values.</param>
    /// <returns>The payload.</returns>
    private static DbFirstGameWindowPayload CreatePayload(
        IDictionary<int, string> atkValues,
        IDictionary<int, string> stringArrayValues)
    {
        return new DbFirstGameWindowPayload(
            new SortedDictionary<int, string>(atkValues),
            new SortedDictionary<int, string>(stringArrayValues));
    }
}
