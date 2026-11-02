// <copyright file="GameWindowStaleLayoutCleanupTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;
using Echoglossian.NativeUI.AddonHandlers.Common;
using Echoglossian.NativeUI.AddonHandlers.MainMenu;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>Verifies stale layout cleanup dispatch without invoking native text or layout functions.</summary>
public sealed class GameWindowStaleLayoutCleanupTests
{
    /// <summary>Ensures a fully repainted payload still restores owned layout when no old text remains.</summary>
    [Fact]
    public void ChangedPayload_RestoresOwnedLayoutWithoutRestoringFreshText()
    {
        var probe = new LayoutCleanupProbe();
        SetRuntimeState(probe);
        var live = Payload("New tooltip");

        Assert.True(probe.Cleanup(live));

        Assert.Equal(1, probe.HookCalls);
        Assert.Equal(1, probe.NodeResolutionCalls);
        Assert.Equal(48, probe.Height);
        Assert.False(probe.HasSnapshot);
        Assert.Equal("New tooltip", live.TextNodes["3:0"]);
        Assert.False(probe.Cleanup(live));
        Assert.Equal(48, probe.Height);
    }

    /// <summary>Ensures the current original or translated payload retains its applied layout.</summary>
    /// <param name="translated">Whether the live payload matches the translated snapshot.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StablePayload_DoesNotDispatchCleanup(bool translated)
    {
        var probe = new LayoutCleanupProbe();
        SetRuntimeState(probe);

        Assert.False(probe.Cleanup(Payload(translated ? "Previous translation" : "Previous original")));

        Assert.Equal(0, probe.HookCalls);
        Assert.Equal(0, probe.NodeResolutionCalls);
        Assert.Equal(300, probe.Height);
        Assert.True(probe.HasSnapshot);
    }

    /// <summary>Ensures missing ownership or opt-in cannot mutate the managed layout snapshot.</summary>
    /// <param name="runtimeState">Whether an applied payload is owned.</param>
    /// <param name="textNodes">Whether the handler captures text nodes.</param>
    /// <param name="optIn">Whether the handler permits stale cleanup.</param>
    /// <param name="snapshot">Whether the addon hook owns a layout snapshot.</param>
    /// <param name="hookCalls">The expected addon hook dispatch count.</param>
    [Theory]
    [InlineData(false, true, true, true, 0)]
    [InlineData(true, false, true, true, 0)]
    [InlineData(true, true, false, true, 0)]
    [InlineData(true, true, true, false, 1)]
    public void MissingCleanupOwnership_DoesNotMutateLayout(
        bool runtimeState, bool textNodes, bool optIn, bool snapshot, int hookCalls)
    {
        var probe = new LayoutCleanupProbe(textNodes, optIn) { HasSnapshot = snapshot };
        if (runtimeState)
        {
            SetRuntimeState(probe);
        }

        var live = Payload("New tooltip");
        Assert.False(probe.Cleanup(live));

        Assert.Equal(hookCalls, probe.HookCalls);
        Assert.Equal(300, probe.Height);
        Assert.Equal(snapshot, probe.HasSnapshot);
        Assert.Equal("New tooltip", live.TextNodes["3:0"]);
    }

    /// <summary>Ensures other stale-text owners retain the default layout follow-up hook.</summary>
    /// <param name="handlerType">The non-Tooltip addon owner.</param>
    [Theory]
    [InlineData(typeof(AddonContextMenuTitleHandler))]
    [InlineData(typeof(SystemMenuHandler))]
    public void OtherStaleTextOwners_KeepDefaultLayoutHook(Type handlerType)
    {
        var hook = handlerType.GetMethod(
            "AfterRestoreStaleTranslatedTextNodes", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(hook);
        Assert.Equal(typeof(DbFirstGameWindowAddonHandler), hook.DeclaringType);
    }

    /// <summary>Creates a text-node payload without a native addon.</summary>
    /// <param name="text">The visible node text.</param>
    /// <returns>The managed payload.</returns>
    private static DbFirstGameWindowPayload Payload(string text)
    {
        return new DbFirstGameWindowPayload([], [], new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["3:0"] = text,
        });
    }

    /// <summary>Installs the previously applied managed payload without running native capture.</summary>
    /// <param name="handler">The test handler.</param>
    private static void SetRuntimeState(DbFirstGameWindowAddonHandler handler)
    {
        var field = typeof(DbFirstGameWindowAddonHandler).GetField(
            "runtimeState", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(handler, new DbFirstGameWindowRuntimeState(
            "en", "previous", Payload("Previous original"), Payload("Previous translation")));
    }

    /// <summary>Models only addon-owned layout while native node traversal returns no nodes.</summary>
    private sealed unsafe class LayoutCleanupProbe : DbFirstGameWindowAddonHandler
    {
        private readonly bool optIn;

        /// <summary>Initializes the minimal managed cleanup probe.</summary>
        /// <param name="textNodes">Whether text-node capture is enabled.</param>
        /// <param name="optIn">Whether stale cleanup is enabled.</param>
        public LayoutCleanupProbe(bool textNodes = true, bool optIn = true)
            : base("LayoutCleanupProbe", new Config(), null!, null!, static _ => true,
                useAtkValues: false, useTextNodes: textNodes)
        {
            this.optIn = optIn;
        }

        /// <summary>Gets or sets whether layout restoration is owned.</summary>
        public bool HasSnapshot { get; set; } = true;

        /// <summary>Gets the current managed layout height.</summary>
        public int Height { get; private set; } = 300;

        /// <summary>Gets the number of addon follow-up dispatches.</summary>
        public int HookCalls { get; private set; }

        /// <summary>Gets the number of native node resolver calls.</summary>
        public int NodeResolutionCalls { get; private set; }

        /// <summary>Dispatches cleanup with no native addon pointer.</summary>
        /// <param name="live">The freshly captured payload.</param>
        /// <returns>Whether cleanup requires recapture.</returns>
        public bool Cleanup(DbFirstGameWindowPayload live)
        {
            return this.TryCleanupStaleTextNodePayload(null, live);
        }

        /// <inheritdoc />
        protected override bool ShouldRestoreStaleTranslatedTextNodesOnPayloadChange()
        {
            return this.optIn;
        }

        /// <inheritdoc />
        protected override List<nint> ResolveTextNodeAddresses(AtkUnitBase* addon)
        {
            this.NodeResolutionCalls++;
            return [];
        }

        /// <inheritdoc />
        private protected override bool AfterRestoreStaleTranslatedTextNodes(AtkUnitBase* addon)
        {
            this.HookCalls++;
            if (!this.HasSnapshot)
            {
                return false;
            }

            this.Height = 48;
            this.HasSnapshot = false;
            return true;
        }
    }
}
