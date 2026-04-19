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
    ///     Ensures a fully translated live text-node payload recovers the
    ///     persisted original payload.
    /// </summary>
    [Fact]
    public void TryRecoverOriginalPayload_RecoversOriginal_WhenLiveTextNodesMatchTranslated()
    {
        var original = CreatePayload(
            atkValues: new Dictionary<int, string>(),
            stringArrayValues: new Dictionary<int, string>(),
            textNodes: new Dictionary<string, string>
            {
                ["2:0"] = "Attributes",
                ["3:0"] = "Character",
            });
        var translated = CreatePayload(
            atkValues: new Dictionary<int, string>(),
            stringArrayValues: new Dictionary<int, string>(),
            textNodes: new Dictionary<string, string>
            {
                ["2:0"] = "Atributos",
                ["3:0"] = "Personagem",
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
    ///     Ensures recovery still succeeds when the live payload is only a
    ///     subset of the persisted candidate, which happens when the game
    ///     temporarily repaints only part of a string-array-backed surface.
    /// </summary>
    [Fact]
    public void TryRecoverOriginalPayload_RecoversOriginal_WhenLiveMatchesSubsetOfCandidate()
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
                [1] = "Frontline",
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
                [1] = "Linha de Frente",
            });
        var partialLivePayload = CreatePayload(
            atkValues: new Dictionary<int, string>
            {
                [1] = "Perfil",
            },
            stringArrayValues: new Dictionary<int, string>
            {
                [0] = "Grand Company",
            });

        var resolved = DbFirstPayloadRecoveryHelper.TryRecoverOriginalPayload(
            partialLivePayload,
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
    ///     Ensures translated-slot evidence is detected even when full payload
    ///     recovery is not possible.
    /// </summary>
    [Fact]
    public void HasTranslatedSlotEvidence_ReturnsTrue_WhenLiveContainsTranslatedSlot()
    {
        var livePayload = CreatePayload(
            atkValues: new Dictionary<int, string>
            {
                [1] = "Perfil",
            },
            stringArrayValues: new Dictionary<int, string>
            {
                [0] = "Grand Company",
            });
        var original = CreatePayload(
            atkValues: new Dictionary<int, string>
            {
                [1] = "Profile",
            },
            stringArrayValues: new Dictionary<int, string>
            {
                [0] = "Grand Company",
                [1] = "Frontline",
            });
        var translated = CreatePayload(
            atkValues: new Dictionary<int, string>
            {
                [1] = "Perfil",
            },
            stringArrayValues: new Dictionary<int, string>
            {
                [0] = "Grande Companhia",
                [1] = "Linha de Frente",
            });

        var hasEvidence = DbFirstPayloadRecoveryHelper.HasTranslatedSlotEvidence(
            livePayload,
            new[]
            {
                new DbFirstPayloadRecoveryCandidate(original, translated),
            });

        Assert.True(hasEvidence);
    }

    /// <summary>
    ///     Ensures translated-slot evidence is detected for text-node payloads.
    /// </summary>
    [Fact]
    public void HasTranslatedSlotEvidence_ReturnsTrue_WhenLiveContainsTranslatedTextNode()
    {
        var livePayload = CreatePayload(
            atkValues: new Dictionary<int, string>(),
            stringArrayValues: new Dictionary<int, string>(),
            textNodes: new Dictionary<string, string>
            {
                ["2:0"] = "Atributos",
            });
        var original = CreatePayload(
            atkValues: new Dictionary<int, string>(),
            stringArrayValues: new Dictionary<int, string>(),
            textNodes: new Dictionary<string, string>
            {
                ["2:0"] = "Attributes",
                ["3:0"] = "Character",
            });
        var translated = CreatePayload(
            atkValues: new Dictionary<int, string>(),
            stringArrayValues: new Dictionary<int, string>(),
            textNodes: new Dictionary<string, string>
            {
                ["2:0"] = "Atributos",
                ["3:0"] = "Personagem",
            });

        var hasEvidence = DbFirstPayloadRecoveryHelper.HasTranslatedSlotEvidence(
            livePayload,
            new[]
            {
                new DbFirstPayloadRecoveryCandidate(original, translated),
            });

        Assert.True(hasEvidence);
    }

    /// <summary>
    ///     Ensures compatible persisted candidates can still be reused when
    ///     the currently visible original-facing payload is only a subset of
    ///     the persisted original payload.
    /// </summary>
    [Fact]
    public void TryResolveCompatibleCandidate_ResolvesUniqueSupersetOriginal()
    {
        var liveOriginalPayload = CreatePayload(
            atkValues: new Dictionary<int, string>
            {
                [3] = "Character",
                [4] = "Duty",
            },
            stringArrayValues: new Dictionary<int, string>());
        var candidateOriginal = CreatePayload(
            atkValues: new Dictionary<int, string>
            {
                [3] = "Character",
                [4] = "Duty",
                [5] = "Logs",
            },
            stringArrayValues: new Dictionary<int, string>());
        var candidateTranslated = CreatePayload(
            atkValues: new Dictionary<int, string>
            {
                [3] = "Personagem",
                [4] = "Dever",
                [5] = "Registros",
            },
            stringArrayValues: new Dictionary<int, string>());

        var resolved = DbFirstPayloadRecoveryHelper.TryResolveCompatibleCandidate(
            liveOriginalPayload,
            new[]
            {
                new DbFirstPayloadRecoveryCandidate(
                    candidateOriginal,
                    candidateTranslated),
            },
            out var resolvedCandidate);

        Assert.True(resolved);
        Assert.Equal(candidateOriginal, resolvedCandidate.OriginalPayload);
        Assert.Equal(candidateTranslated, resolvedCandidate.TranslatedPayload);
    }

    /// <summary>
    ///     Ensures ambiguous compatible candidates do not resolve to the wrong
    ///     persisted row.
    /// </summary>
    [Fact]
    public void TryResolveCompatibleCandidate_ReturnsFalse_WhenCompatibleCandidatesAreAmbiguous()
    {
        var liveOriginalPayload = CreatePayload(
            atkValues: new Dictionary<int, string>
            {
                [3] = "Character",
            },
            stringArrayValues: new Dictionary<int, string>());
        var firstOriginal = CreatePayload(
            atkValues: new Dictionary<int, string>
            {
                [3] = "Character",
                [4] = "Duty",
            },
            stringArrayValues: new Dictionary<int, string>());
        var secondOriginal = CreatePayload(
            atkValues: new Dictionary<int, string>
            {
                [3] = "Character",
                [5] = "Logs",
            },
            stringArrayValues: new Dictionary<int, string>());
        var translated = CreatePayload(
            atkValues: new Dictionary<int, string>
            {
                [3] = "Personagem",
            },
            stringArrayValues: new Dictionary<int, string>());

        var resolved = DbFirstPayloadRecoveryHelper.TryResolveCompatibleCandidate(
            liveOriginalPayload,
            new[]
            {
                new DbFirstPayloadRecoveryCandidate(firstOriginal, translated),
                new DbFirstPayloadRecoveryCandidate(secondOriginal, translated),
            },
            out _);

        Assert.False(resolved);
    }

    /// <summary>
    ///     Ensures compatible superset payloads are projected back to the live
    ///     shape before apply so reused addons do not receive translated text
    ///     for slots that are not part of the current context.
    /// </summary>
    [Fact]
    public void ProjectToShape_KeepsOnlyReferenceKeys()
    {
        var referencePayload = CreatePayload(
            atkValues: new Dictionary<int, string>
            {
                [3] = "Character",
                [4] = "Duty",
            },
            stringArrayValues: new Dictionary<int, string>
            {
                [0] = "Category",
            });
        var supersetPayload = CreatePayload(
            atkValues: new Dictionary<int, string>
            {
                [3] = "Personagem",
                [4] = "Dever",
                [5] = "Registros",
            },
            stringArrayValues: new Dictionary<int, string>
            {
                [0] = "Categoria",
                [1] = "Extra",
            });

        var projectedPayload = supersetPayload.ProjectToShape(referencePayload);

        var expectedPayload = CreatePayload(
            atkValues: new Dictionary<int, string>
            {
                [3] = "Personagem",
                [4] = "Dever",
            },
            stringArrayValues: new Dictionary<int, string>
            {
                [0] = "Categoria",
            });

        Assert.Equal(expectedPayload.Serialize(), projectedPayload.Serialize());
    }

    /// <summary>
    ///     Creates one payload for test usage.
    /// </summary>
    /// <param name="atkValues">The ATK values.</param>
    /// <param name="stringArrayValues">The string-array values.</param>
    /// <returns>The payload.</returns>
    private static DbFirstGameWindowPayload CreatePayload(
        IDictionary<int, string> atkValues,
        IDictionary<int, string> stringArrayValues,
        IDictionary<string, string>? textNodes = null)
    {
        return new DbFirstGameWindowPayload(
            new SortedDictionary<int, string>(atkValues),
            new SortedDictionary<int, string>(stringArrayValues),
            new SortedDictionary<string, string>(
                textNodes ?? new Dictionary<string, string>(),
                StringComparer.Ordinal));
    }
}
