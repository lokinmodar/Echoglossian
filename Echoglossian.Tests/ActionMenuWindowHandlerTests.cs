// <copyright file="ActionMenuWindowHandlerTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.ActionMenu;
using Echoglossian.NativeUI.AddonHandlers.Common;
using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Tests.TestDoubles;
using System.Reflection;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers narrow text-shape helpers used by the <c>ActionMenu</c> DB-first
///     runtime.
/// </summary>
public class ActionMenuWindowHandlerTests
{
    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="ActionMenuWindowHandlerTests" /> class.
    /// </summary>
    public ActionMenuWindowHandlerTests()
    {
        Echoglossian.PluginLog ??=
            new NoOpPluginLog();
    }

    /// <summary>
    ///     Ensures the shared GameWindow runtime cannot reuse a persisted row
    ///     from a different source client language.
    /// </summary>
    [Fact]
    public void MatchesPersistedSourceIdentity_ReturnsFalseForDifferentSource()
    {
        var sourceLanguage = new SourceClientLanguage("de", "de");

        var matches = DbFirstGameWindowAddonHandler
            .MatchesPersistedSourceIdentity("en", sourceLanguage);

        Assert.False(matches);
    }

    /// <summary>
    ///     Ensures a failed queued ActionMenu payload can retry under the same
    ///     scope and that a matching page signature in another full scope is
    ///     not blocked by the first queue owner.
    /// </summary>
    [Fact]
    public void ActionMenuQueue_FailureReleasesSignatureAndScopeChangeAllowsRetry()
    {
        const string signature = "Dancer\u001FSupport Desk";
        var tracker = new ActionMenuQueuedSignatureTracker();
        var originalScope = new TranslationReuseScope("en", "pt-BR", 0, false);
        var changedScope = new TranslationReuseScope("en", "ja", 7, true);

        Assert.True(tracker.TryQueue(originalScope, generation: 1, signature));
        Assert.False(tracker.TryQueue(originalScope, generation: 1, signature));

        tracker.Release(originalScope, generation: 1, signature);

        Assert.True(tracker.TryQueue(originalScope, generation: 1, signature));
        Assert.True(tracker.TryQueue(changedScope, generation: 2, signature));
        Assert.False(tracker.TryQueue(changedScope, generation: 2, signature));
    }

    /// <summary>
    ///     Ensures a completion from an older scope cannot release the active
    ///     scope's queued signature.
    /// </summary>
    [Fact]
    public void ActionMenuQueue_StaleScopeCannotReleaseActiveSignature()
    {
        const string signature = "Dancer\u001FSupport Desk";
        var tracker = new ActionMenuQueuedSignatureTracker();
        var originalScope = new TranslationReuseScope("en", "pt-BR", 0, false);
        var activeScope = new TranslationReuseScope("en", "ja", 7, true);

        Assert.True(tracker.TryQueue(originalScope, generation: 1, signature));
        Assert.True(tracker.TryQueue(activeScope, generation: 2, signature));

        tracker.Release(originalScope, generation: 1, signature);

        Assert.False(tracker.TryQueue(activeScope, generation: 2, signature));
    }

    /// <summary>
    ///     Ensures an older A completion cannot release a new A request after
    ///     the active lifecycle has progressed through A to B and back to A.
    /// </summary>
    [Fact]
    public void ActionMenuQueue_ScopeCycleKeepsNewGenerationClaimed()
    {
        const string signature = "Dancer\u001FSupport Desk";
        var tracker = new ActionMenuQueuedSignatureTracker();
        var scopeA = new TranslationReuseScope("en", "pt-BR", 0, false);
        var scopeB = new TranslationReuseScope("en", "ja", 7, true);

        Assert.True(tracker.TryQueue(scopeA, generation: 1, signature));
        Assert.True(tracker.TryQueue(scopeB, generation: 2, signature));
        Assert.True(tracker.TryQueue(scopeA, generation: 3, signature));

        tracker.Release(scopeA, generation: 1, signature);

        Assert.False(tracker.TryQueue(scopeA, generation: 3, signature));
    }

    /// <summary>
    ///     Ensures a failure cooldown is evaluated before an ActionMenu
    ///     signature can be claimed, so the next eligible refresh can retry.
    /// </summary>
    [Fact]
    public void ActionMenuQueue_CooldownDoesNotClaimSignature()
    {
        const string signature = "Dancer\u001FSupport Desk";
        var tracker = new ActionMenuQueuedSignatureTracker();
        var scope = new TranslationReuseScope("en", "pt-BR", 0, false);

        Assert.False(tracker.TryQueue(
            scope,
            generation: 1,
            signature,
            retryCoolingDown: true));
        Assert.True(tracker.TryQueue(
            scope,
            generation: 1,
            signature,
            retryCoolingDown: false));
    }

    /// <summary>
    ///     Ensures a translated action label preserves the original line break
    ///     before the trailing level token.
    /// </summary>
    [Fact]
    public void PreserveSourceLevelSeparator_RestoresOriginalLineBreak()
    {
        var result = ActionMenuWindowHandler.PreserveSourceLevelSeparator(
            "Peloton\r\nLv. 20",
            "Peloton Nível 20");

        Assert.Equal("Peloton\r\nNível 20", result);
    }

    /// <summary>
    ///     Ensures a collapsed level separator is restored when the source used
    ///     a normal space separator.
    /// </summary>
    [Fact]
    public void PreserveSourceLevelSeparator_RestoresOriginalSpaceSeparator()
    {
        var result = ActionMenuWindowHandler.PreserveSourceLevelSeparator(
            "Fan Dance IV Lv. 86",
            "Dança do Leque IVLv. 86");

        Assert.Equal("Dança do Leque IV Lv. 86", result);
    }

    /// <summary>
    ///     Ensures unrelated text is left untouched when no trailing level
    ///     token exists.
    /// </summary>
    [Fact]
    public void PreserveSourceLevelSeparator_LeavesNonLevelTextUnchanged()
    {
        var result = ActionMenuWindowHandler.PreserveSourceLevelSeparator(
            "Display Mode",
            "Modo de exibição");

        Assert.Equal("Modo de exibição", result);
    }

    /// <summary>
    ///     Ensures the `ActionMenu` handler keeps a short bounded post-event
    ///     refresh window so new pages can satisfy the stability gate without
    ///     restoring permanent applied-state `PreDraw` polling.
    /// </summary>
    [Fact]
    public void GetActionMenuAppliedStateRefreshWindow_ReturnsExpectedDuration()
    {
        var duration = ActionMenuWindowHandler
            .GetActionMenuAppliedStateRefreshWindow();

        Assert.Equal(TimeSpan.FromMilliseconds(250), duration);
    }

    /// <summary>
    ///     Ensures the ActionMenu capture policy accepts the text-node ids
    ///     observed in the live left rail, window title, command grid, and
    ///     mode-description surfaces from the addon probe logs.
    /// </summary>
    /// <param name="nodeId">The observed text-node identifier.</param>
    /// <param name="visibleText">One representative visible text.</param>
    [Theory]
    [InlineData(2u, "ACTIONS")]
    [InlineData(3u, "Actions & Traits")]
    [InlineData(7u, "Support Desk\r")]
    [InlineData(10u, "Peloton\r\nLv. 20")]
    [InlineData(71u, "Display job gauge description.")]
    [InlineData(72u, "Display Mode")]
    public void ShouldCaptureActionMenuTextNode_ReturnsTrueForObservedNodeIds(
        uint nodeId,
        string visibleText)
    {
        var shouldCapture = ActionMenuWindowHandler
            .ShouldCaptureActionMenuTextNode(
                nodeId,
                visibleText);

        Assert.True(shouldCapture);
    }

    /// <summary>
    ///     Ensures the ActionMenu capture policy rejects node ids and texts
    ///     that do not belong to the active page surface.
    /// </summary>
    [Fact]
    public void ShouldCaptureActionMenuTextNode_ReturnsFalseForIrrelevantInputs()
    {
        Assert.False(ActionMenuWindowHandler.ShouldCaptureActionMenuTextNode(
            99,
            "Untracked Surface"));
        Assert.False(ActionMenuWindowHandler.ShouldCaptureActionMenuTextNode(
            7,
            " "));
    }

    /// <summary>
    ///     Ensures base-name-only action resolution preserves the captured
    ///     level token and separator verbatim.
    /// </summary>
    [Fact]
    public void MergeResolvedTranslatedPayload_BaseNameOnlyPreservesLevelToken()
    {
        ActionTooltipCacheManager.Clear();

        try
        {
            ActionTooltipCacheManager.Update(new ActionTooltip
            {
                Id = 1,
                ActionId = 16001,
                ActionName = "Peloton",
                OriginalLang = "en",
                TranslatedActionName = "Pelotão",
                TranslationLang = "pt-BR",
                TranslationEngine = 0,
                GameVersion = "7.3",
                SourceContentHash = "hash-peloton",
            });

            var originalPayload = new DbFirstGameWindowPayload(
                new SortedDictionary<int, string>
                {
                    [17] = "Peloton\r\nLv. 20",
                },
                new SortedDictionary<int, string>(),
                new SortedDictionary<string, string>(StringComparer.Ordinal));
            var resolvedTranslatedPayload = new DbFirstGameWindowPayload(
                new SortedDictionary<int, string>
                {
                    [17] = "PelotonLv. 20",
                },
                new SortedDictionary<int, string>(),
                new SortedDictionary<string, string>(StringComparer.Ordinal));

            var mergedPayload = ActionMenuWindowHandler
                .MergeResolvedTranslatedPayload(
                    originalPayload,
                    resolvedTranslatedPayload,
                    new TranslationReuseScope("en", "pt-BR", 0, true),
                    "7.3",
                    new Dictionary<string, string>(StringComparer.Ordinal));

            Assert.Equal(
                "Pelotão\r\nLv. 20",
                mergedPayload.AtkValues[17]);
        }
        finally
        {
            ActionTooltipCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures level-aware trait-name resolution preserves the captured
    ///     level token while reusing the canonical trait cache.
    /// </summary>
    [Fact]
    public void MergeResolvedTranslatedPayload_UsesLevelAwareTraitLookup()
    {
        TraitCacheManager.Clear();

        try
        {
            TraitCacheManager.Update(new Trait
            {
                Id = 1,
                TraitId = 201,
                TraitName = "Enhanced Windmill",
                OriginalLang = "en",
                TranslatedTraitName = "Moinho Aprimorado",
                TranslationLang = "pt-BR",
                TranslationEngine = 0,
                GameVersion = "7.3",
                SourceContentHash = "hash-enhanced-windmill",
            });

            var originalPayload = new DbFirstGameWindowPayload(
                new SortedDictionary<int, string>
                {
                    [17] = "Enhanced Windmill\r\nLv. 15",
                },
                new SortedDictionary<int, string>(),
                new SortedDictionary<string, string>(StringComparer.Ordinal));
            var resolvedTranslatedPayload = new DbFirstGameWindowPayload(
                new SortedDictionary<int, string>
                {
                    [17] = "Enhanced WindmillLv. 15",
                },
                new SortedDictionary<int, string>(),
                new SortedDictionary<string, string>(StringComparer.Ordinal));

            var mergedPayload = ActionMenuWindowHandler
                .MergeResolvedTranslatedPayload(
                    originalPayload,
                    resolvedTranslatedPayload,
                    new TranslationReuseScope("en", "pt-BR", 0, true),
                    "7.3",
                    new Dictionary<string, string>(StringComparer.Ordinal));

            Assert.Equal(
                "Moinho Aprimorado\r\nLv. 15",
                mergedPayload.AtkValues[17]);
        }
        finally
        {
            TraitCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures one persisted fallback lookup can repair one malformed
    ///     ActionMenu level label even when the source separator was already
    ///     collapsed.
    /// </summary>
    [Fact]
    public void MergeResolvedTranslatedPayload_UsesPersistedFallbackForCollapsedLevelSeparator()
    {
        var originalPayload = new DbFirstGameWindowPayload(
            new SortedDictionary<int, string>
            {
                [17] = "PelotonLv. 20",
            },
            new SortedDictionary<int, string>(),
            new SortedDictionary<string, string>(StringComparer.Ordinal));
        var resolvedTranslatedPayload = new DbFirstGameWindowPayload(
            new SortedDictionary<int, string>
            {
                [17] = "PelotonLv. 20",
            },
            new SortedDictionary<int, string>(),
            new SortedDictionary<string, string>(StringComparer.Ordinal));

        var mergedPayload = ActionMenuWindowHandler
            .MergeResolvedTranslatedPayload(
                originalPayload,
                resolvedTranslatedPayload,
                new TranslationReuseScope("en", "pt-BR", 0, true),
                "7.3",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Peloton"] = "Pelotão",
                });

        Assert.Equal(
            "Pelotão Lv. 20",
            mergedPayload.AtkValues[17]);
    }

    /// <summary>
    ///     Ensures one persisted fallback lookup can repair one normal menu
    ///     chrome label that is absent from action-tooltip storage.
    /// </summary>
    [Fact]
    public void MergeResolvedTranslatedPayload_UsesPersistedFallbackForWindowChrome()
    {
        var originalPayload = new DbFirstGameWindowPayload(
            new SortedDictionary<int, string>(),
            new SortedDictionary<int, string>(),
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["3:100"] = "System",
            });
        var resolvedTranslatedPayload = new DbFirstGameWindowPayload(
            new SortedDictionary<int, string>(),
            new SortedDictionary<int, string>(),
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["3:100"] = "System",
            });

        var mergedPayload = ActionMenuWindowHandler
            .MergeResolvedTranslatedPayload(
                originalPayload,
                resolvedTranslatedPayload,
                new TranslationReuseScope("en", "pt-BR", 0, true),
                "7.3",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["System"] = "Sistema",
                });

        Assert.Equal(
            "Sistema",
            mergedPayload.TextNodes["3:100"]);
    }

    /// <summary>
    ///     Ensures the stable ActionMenu page signature ignores volatile
    ///     metadata and long descriptive text while keeping short page labels.
    /// </summary>
    [Fact]
    public void BuildStablePayloadSignature_IgnoresMetadataAndLongDescriptions()
    {
        var payload = new DbFirstGameWindowPayload(
            new SortedDictionary<int, string>
            {
                [0] = "Switch View",
                [10] = "Level 92",
                [12] = "Dancer",
                [49] =
                    "Search for answers to frequently asked questions, report issues, and confirm messages from the FINAL FANTASY XIV support team.",
            },
            new SortedDictionary<int, string>(),
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["3:100"] = "System",
                ["6:101"] = "Support Desk",
                ["6:102"] = "Official Sites",
                ["6:103"] = "Playguide",
                ["6:104"] = "Character Configuration",
            });

        var signature = ActionMenuWindowHandler.BuildStablePayloadSignature(
            payload);

        Assert.DoesNotContain("Switch View", signature, StringComparison.Ordinal);
        Assert.DoesNotContain("Level 92", signature, StringComparison.Ordinal);
        Assert.Contains("job:Dancer", signature, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Search for answers",
            signature,
            StringComparison.Ordinal);
        Assert.Contains("Support Desk", signature, StringComparison.Ordinal);
        Assert.Contains("Official Sites", signature, StringComparison.Ordinal);
        Assert.Contains("Playguide", signature, StringComparison.Ordinal);
        Assert.Contains(
            "Character Configuration",
            signature,
            StringComparison.Ordinal);
        Assert.Contains("System", signature, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the stable ActionMenu page signature still records the
    ///     active class or job so persisted pages from one job are not reused
    ///     for another job.
    /// </summary>
    [Fact]
    public void BuildStablePayloadSignature_IncludesClassJobIdentity()
    {
        var payload = new DbFirstGameWindowPayload(
            new SortedDictionary<int, string>
            {
                [10] = "Level 92",
                [12] = "Dancer",
                [17] = "Support Desk\n",
                [25] = "Official Sites\n",
                [33] = "Playguide\n",
                [41] = "Character Configuration\n",
                [49] = "System Configuration\n",
            },
            new SortedDictionary<int, string>(),
            new SortedDictionary<string, string>(StringComparer.Ordinal));

        var signature = ActionMenuWindowHandler.BuildStablePayloadSignature(
            payload);

        Assert.Contains("job:Dancer", signature, StringComparison.Ordinal);
        Assert.DoesNotContain("Level 92", signature, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures stale ATK-only labels do not affect the stable page
    ///     signature when the active ActionMenu surface already exposes the
    ///     real page labels through visible text nodes.
    /// </summary>
    [Fact]
    public void BuildStablePayloadSignature_PrefersVisibleTextsOverResidualAtkStrings()
    {
        var payload = new DbFirstGameWindowPayload(
            new SortedDictionary<int, string>
            {
                [12] = "Dancer",
                [17] = "Support Desk",
                [25] = "Official Sites",
                [33] = "Alarm",
            },
            new SortedDictionary<int, string>(),
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["3:100"] = "Support Desk",
                ["3:101"] = "Official Sites",
            });

        var signature = ActionMenuWindowHandler.BuildStablePayloadSignature(
            payload);

        Assert.Contains("job:Dancer", signature, StringComparison.Ordinal);
        Assert.Contains("Support Desk", signature, StringComparison.Ordinal);
        Assert.Contains("Official Sites", signature, StringComparison.Ordinal);
        Assert.DoesNotContain("Alarm", signature, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the unseen-text counter excludes short texts already
    ///     covered by either canonical action names or persisted window-chrome
    ///     fallbacks.
    /// </summary>
    [Fact]
    public void CountMeaningfulUnseenTexts_UsesActionAndWindowFallbacks()
    {
        ActionTooltipCacheManager.Clear();

        try
        {
            ActionTooltipCacheManager.Update(new ActionTooltip
            {
                Id = 1,
                ActionId = 16001,
                ActionName = "Peloton",
                OriginalLang = "en",
                TranslatedActionName = "Peloton",
                TranslationLang = "pt-BR",
                TranslationEngine = 0,
                GameVersion = "7.3",
                SourceContentHash = "hash-peloton",
            });

            var payload = new DbFirstGameWindowPayload(
                new SortedDictionary<int, string>
                {
                    [17] = "Peloton Lv. 20",
                    [25] = "Support Desk",
                    [33] = "Playguide",
                },
                new SortedDictionary<int, string>(),
                new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["3:100"] = "System",
                    ["3:101"] = "Display Mode",
                });

            var unseenCount = ActionMenuWindowHandler.CountMeaningfulUnseenTexts(
                payload,
                new TranslationReuseScope("en", "pt-BR", 0, true),
                "7.3",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Support Desk"] = "Central de Suporte",
                    ["Playguide"] = "Guia do Jogo",
                    ["System"] = "Sistema",
                });

            Assert.Equal(1, unseenCount);
        }
        finally
        {
            ActionTooltipCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures the unseen-text counter treats level-tagged ActionMenu
    ///     labels as canonical when only the base action name exists in cache
    ///     and the translated name is identical to the original.
    /// </summary>
    [Fact]
    public void CountMeaningfulUnseenTexts_TreatsLevelAwareCanonicalNamesAsKnown()
    {
        ActionTooltipCacheManager.Clear();

        try
        {
            ActionTooltipCacheManager.Update(new ActionTooltip
            {
                Id = 2,
                ActionId = 16002,
                ActionName = "Canonical Peloton Test",
                OriginalLang = "en",
                TranslatedActionName = "Canonical Peloton Test",
                TranslationLang = "pt",
                TranslationEngine = 0,
                GameVersion = "7.3",
                SourceContentHash = "hash-canonical-peloton-test",
            });

            var payload = new DbFirstGameWindowPayload(
                new SortedDictionary<int, string>
                {
                    [17] = "Canonical Peloton Test\r\nLv. 20",
                },
                new SortedDictionary<int, string>(),
                new SortedDictionary<string, string>(StringComparer.Ordinal));

            var unseenCount = ActionMenuWindowHandler.CountMeaningfulUnseenTexts(
                payload,
                new TranslationReuseScope("en", "pt-BR", 0, true),
                "7.3",
                new Dictionary<string, string>(StringComparer.Ordinal));

            Assert.Equal(0, unseenCount);
        }
        finally
        {
            ActionTooltipCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures the unseen-text counter recognizes canonical reference-text
    ///     rows even when the captured ActionMenu chrome text carries embedded
    ///     control bytes and the canonical row was stored under the generic
    ///     <c>pt</c> language code.
    /// </summary>
    [Fact]
    public void CountMeaningfulUnseenTexts_UsesNormalizedReferenceTextLookup()
    {
        ReferenceTextCacheRegistry.MainCommandTexts.Update(new MainCommandText
        {
            Id = 7001,
            ReferenceId = 7001,
            OriginalLang = "en",
            TranslationLang = "pt",
            TranslationEngine = 0,
            GameVersion = "7.3",
            SourceContentHash = "hash-maincommand-character-configuration",
            OriginalName = "Character Configuration",
            TranslatedName = "Configuracao de Personagem",
            OriginalDescription = "Opens character configuration.",
            TranslatedDescription = "Abre a configuracao de personagem.",
        });

        var payload = new DbFirstGameWindowPayload(
            new SortedDictionary<int, string>(),
            new SortedDictionary<int, string>(),
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["3:100"] = "Character \u0002\u0010\u0001\u0003Configuration\r",
            });

        var unseenCount = ActionMenuWindowHandler.CountMeaningfulUnseenTexts(
            payload,
            new TranslationReuseScope("en", "pt-BR", 0, true),
            "7.3",
            new Dictionary<string, string>(StringComparer.Ordinal));

        Assert.Equal(0, unseenCount);
    }

    /// <summary>
    ///     Ensures residual ATK-only labels do not inflate the unseen-text
    ///     count when the active ActionMenu page already exposes corroborated
    ///     visible text nodes for the real page content.
    /// </summary>
    [Fact]
    public void CountMeaningfulUnseenTexts_IgnoresResidualAtkStringsWhenVisibleTextsExist()
    {
        var payload = new DbFirstGameWindowPayload(
            new SortedDictionary<int, string>
            {
                [17] = "Support Desk",
                [25] = "Official Sites",
                [33] = "Alarm",
            },
            new SortedDictionary<int, string>(),
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["3:100"] = "Support Desk",
                ["3:101"] = "Official Sites",
            });

        var unseenCount = ActionMenuWindowHandler.CountMeaningfulUnseenTexts(
            payload,
            new TranslationReuseScope("en", "pt-BR", 0, true),
            "7.3",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Support Desk"] = "Central de Suporte",
                ["Official Sites"] = "Sites Oficiais",
            });

        Assert.Equal(0, unseenCount);
    }

    /// <summary>
    ///     Ensures canonical ActionMenu lookup never reuses an otherwise
    ///     compatible action row from a different source language.
    /// </summary>
    [Fact]
    public void CountMeaningfulUnseenTexts_DifferentSource_DoesNotReuseCanonicalRow()
    {
        ActionTooltipCacheManager.Clear();

        try
        {
            ActionTooltipCacheManager.Update(new ActionTooltip
            {
                Id = 3,
                ActionId = 16003,
                ActionName = "Source Scoped Action",
                OriginalLang = "en",
                TranslatedActionName = "Acao com origem",
                TranslationLang = "pt-BR",
                TranslationEngine = 0,
                GameVersion = "7.3",
                SourceContentHash = "hash-source-scoped-action",
            });
            var payload = new DbFirstGameWindowPayload(
                new SortedDictionary<int, string>
                {
                    [17] = "Source Scoped Action",
                },
                new SortedDictionary<int, string>(),
                new SortedDictionary<string, string>(StringComparer.Ordinal));

            var matchingSourceCount =
                ActionMenuWindowHandler.CountMeaningfulUnseenTexts(
                    payload,
                    new TranslationReuseScope("en", "pt-BR", 0, true),
                    "7.3",
                    new Dictionary<string, string>(StringComparer.Ordinal));
            var mismatchedSourceCount =
                ActionMenuWindowHandler.CountMeaningfulUnseenTexts(
                    payload,
                    new TranslationReuseScope("de", "pt-BR", 0, true),
                    "7.3",
                    new Dictionary<string, string>(StringComparer.Ordinal));

            Assert.Equal(0, matchingSourceCount);
            Assert.Equal(1, mismatchedSourceCount);
        }
        finally
        {
            ActionTooltipCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures one resolved payload is rejected when only the stable
    ///     ActionMenu metadata changed and the actual command labels remained
    ///     in the original language.
    /// </summary>
    [Fact]
    public void ShouldAcceptActionMenuResolvedPayload_ReturnsFalseForMetadataOnlyTranslation()
    {
        var originalPayload = new DbFirstGameWindowPayload(
            new SortedDictionary<int, string>
            {
                [0] = "Switch View",
                [10] = "Level 92",
                [12] = "Dancer",
                [17] = "Support Desk",
                [25] = "Official Sites",
                [33] = "Playguide",
                [41] = "Active Help",
                [49] = "Character Configuration",
                [57] = "System Configuration",
            },
            new SortedDictionary<int, string>(),
            new SortedDictionary<string, string>(StringComparer.Ordinal));
        var translatedPayload = new DbFirstGameWindowPayload(
            new SortedDictionary<int, string>
            {
                [0] = "Alternar Visão",
                [10] = "Nível 92",
                [12] = "Dançarino",
                [17] = "Support Desk",
                [25] = "Official Sites",
                [33] = "Playguide",
                [41] = "Active Help",
                [49] = "Character Configuration",
                [57] = "System Configuration",
            },
            new SortedDictionary<int, string>(),
            new SortedDictionary<string, string>(StringComparer.Ordinal));

        var accepted = ActionMenuWindowHandler
            .ShouldAcceptActionMenuResolvedPayload(
                originalPayload,
                translatedPayload);

        Assert.False(accepted);
    }

    /// <summary>
    ///     Ensures one resolved payload is accepted when the majority of
    ///     ActionMenu command labels already carry translated content.
    /// </summary>
    [Fact]
    public void ShouldAcceptActionMenuResolvedPayload_ReturnsTrueForMostlyTranslatedContent()
    {
        var originalPayload = new DbFirstGameWindowPayload(
            new SortedDictionary<int, string>
            {
                [0] = "Switch View",
                [10] = "Level 92",
                [12] = "Dancer",
                [17] = "Withdraw",
                [25] = "Follow",
                [33] = "Free Stance",
                [41] = "Defender Stance",
                [49] = "Healer Stance",
                [57] = "Attacker Stance",
                [65] = "Rising Windmill\r\nLv. 35",
            },
            new SortedDictionary<int, string>(),
            new SortedDictionary<string, string>(StringComparer.Ordinal));
        var translatedPayload = new DbFirstGameWindowPayload(
            new SortedDictionary<int, string>
            {
                [0] = "Alternar Visão",
                [10] = "Nível 92",
                [12] = "Dançarino",
                [17] = "Retirar",
                [25] = "Seguir",
                [33] = "Postura Livre",
                [41] = "Postura Defensiva",
                [49] = "Postura de Cura",
                [57] = "Postura de Ataque",
                [65] = "Moinho de Vento Ascendente\r\nNv. 35",
            },
            new SortedDictionary<int, string>(),
            new SortedDictionary<string, string>(StringComparer.Ordinal));

        var accepted = ActionMenuWindowHandler
            .ShouldAcceptActionMenuResolvedPayload(
                originalPayload,
                translatedPayload);

        Assert.True(accepted);
    }

    /// <summary>
    ///     Ensures generic hover-tooltip lookup still resolves when the saved
    ///     payload captured line feeds but the live text node exposes carriage
    ///     returns for the same ActionMenu entry.
    /// </summary>
    [Fact]
    public void BuildTooltipTextMap_NormalizesLineEndingsForHoverLookup()
    {
        var originalPayload = new DbFirstGameWindowPayload(
            new SortedDictionary<int, string>
            {
                [25] = "Second Wind\nLv. 8",
            },
            new SortedDictionary<int, string>(),
            new SortedDictionary<string, string>(StringComparer.Ordinal));
        var translatedPayload = new DbFirstGameWindowPayload(
            new SortedDictionary<int, string>
            {
                [25] = "Segundo fôlego\nNv. 8",
            },
            new SortedDictionary<int, string>(),
            new SortedDictionary<string, string>(StringComparer.Ordinal));

        var lookup = BuildTooltipTextMap(
            originalPayload,
            translatedPayload,
            useTranslatedKeys: false);
        var normalizedVisibleText = NormalizeTooltipLookupText(
            "Second Wind\rLv. 8");

        Assert.True(lookup.TryGetValue(
            normalizedVisibleText,
            out var tooltipBody));
        Assert.Equal("Segundo fôlego\nNv. 8", tooltipBody);
    }

    /// <summary>
    ///     Invokes the shared hover-tooltip lookup builder through reflection.
    /// </summary>
    /// <param name="originalPayload">The original payload.</param>
    /// <param name="translatedPayload">The translated payload.</param>
    /// <param name="useTranslatedKeys">
    ///     Whether translated texts should become the lookup keys.
    /// </param>
    /// <returns>The built lookup map.</returns>
    private static Dictionary<string, string> BuildTooltipTextMap(
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload,
        bool useTranslatedKeys)
    {
        var method = typeof(DbFirstGameWindowAddonHandler).GetMethod(
            "BuildTooltipTextMap",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method!.Invoke(
            null,
            [originalPayload, translatedPayload, useTranslatedKeys]);
        return Assert.IsType<Dictionary<string, string>>(result);
    }

    /// <summary>
    ///     Invokes the shared tooltip-text normalizer through reflection.
    /// </summary>
    /// <param name="text">The text to normalize.</param>
    /// <returns>The normalized lookup text.</returns>
    private static string NormalizeTooltipLookupText(string text)
    {
        var method = typeof(DbFirstGameWindowAddonHandler).GetMethod(
            "NormalizeTooltipLookupText",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method!.Invoke(null, [text]);
        return Assert.IsType<string>(result);
    }
}


