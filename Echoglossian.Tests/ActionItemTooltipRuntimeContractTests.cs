// <copyright file="ActionItemTooltipRuntimeContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;
using PluginEntry = Echoglossian.Echoglossian;

namespace Echoglossian.Tests;

/// <summary>
/// Guards the DB-4 runtime boundaries independently of SQLite and native game state.
/// </summary>
public sealed class ActionItemTooltipRuntimeContractTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.NonPublic;

    /// <summary>Runtime cache misses cannot invoke a legacy synchronous database helper.</summary>
    /// <param name="file">The migrated runtime source file.</param>
    [Theory]
    [InlineData("ActionDetailPrefetchRuntime.cs")]
    [InlineData("TraitDetailPrefetchRuntime.cs")]
    [InlineData("ItemDetailPrefetchRuntime.cs")]
    [InlineData("ActionItemDetailUiRuntime.cs")]
    public void MigratedRuntime_HasNoSynchronousDatabaseFallback(string file)
    {
        var source = ReadRuntimeSource(file);
        Assert.DoesNotMatch(
            @"\b(?:FindActionTooltip|InsertActionTooltip|FindTrait|InsertTrait|FindItemTooltip|InsertItemTooltip)\b",
            source);
        Assert.DoesNotContain("SaveChanges(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAwaiter().GetResult()", source, StringComparison.Ordinal);
    }

    /// <summary>Each prefetch preserves its bounded capture budget and two-second cadence.</summary>
    /// <param name="domain">The tooltip domain.</param>
    /// <param name="budgetField">The existing per-tick budget field.</param>
    /// <param name="budget">The required capture limit.</param>
    [Theory]
    [InlineData("Action", "ActionDetailPrefetchActionsPerTick", 6)]
    [InlineData("Trait", "TraitDetailPrefetchTraitsPerTick", 6)]
    [InlineData("Item", "ItemDetailPrefetchItemsPerTick", 10)]
    public void Prefetch_PreservesCaptureBudgetAndCadence(string domain, string budgetField, int budget)
    {
        Assert.Equal(budget, typeof(PluginEntry).GetField(budgetField, StaticFlags)!.GetRawConstantValue());
        Assert.Equal(TimeSpan.FromSeconds(2), typeof(PluginEntry)
            .GetField(domain + "DetailPrefetchTickInterval", StaticFlags)!.GetValue(null));

        var source = ReadRuntimeSource(domain + "DetailPrefetchRuntime.cs");
        Assert.Matches(@"processedCount\s*<\s*" + Regex.Escape(budgetField), source);
        Assert.Contains(domain + "DetailPrefetchTickInterval", source, StringComparison.Ordinal);
    }

    /// <summary>Captured workers cannot inspect game state or recapture a Lumina payload.</summary>
    /// <param name="domain">The tooltip domain.</param>
    [Theory]
    [InlineData("Action")]
    [InlineData("Trait")]
    [InlineData("Item")]
    public void AsyncWorker_UsesOnlyCapturedManagedState(string domain)
    {
        var source = ReadRuntimeSource(domain + "DetailPrefetchRuntime.cs");
        var worker = ReadMethod(source, "Complete" + domain + "DetailOperationAsync");
        Assert.Contains("await ", worker, StringComparison.Ordinal);
        Assert.DoesNotMatch(
            @"\b(?:GameGuiInterface|DataManager|ClientStateInterface|FrameworkAccessGuard|GetExcelSheet|GetGameVersion|TryGetCurrentClassJobId|TryBuild\w*CanonicalPayload|TryResolveCurrentSourceLanguage|AtkValue|AtkUnitBase|AtkTextNode)\b",
            worker);
        Assert.DoesNotContain("this.configuration", worker, StringComparison.Ordinal);
        Assert.DoesNotContain("->", worker, StringComparison.Ordinal);

        var capture = ReadMethod(source, "Prefetch" + domain + "Detail");
        Assert.Contains("Start" + domain + "DetailOperation(", capture, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Run(", capture, StringComparison.Ordinal);
        Assert.DoesNotContain("await ", capture, StringComparison.Ordinal);
    }

    /// <summary>Disabling global translation prevents all three ticks from touching native state.</summary>
    /// <param name="domain">The tooltip domain.</param>
    [Theory]
    [InlineData("Action")]
    [InlineData("Trait")]
    [InlineData("Item")]
    public void DisabledGlobalTranslation_TickReturnsBeforeCapture(string domain)
    {
        var plugin = CreateWithoutNativeServices(new Config
        {
            Translate = false,
            TranslateTooltips = true,
            TranslateActionMenuWindow = true,
        });
        var method = typeof(PluginEntry).GetMethod("Tick" + domain + "DetailPrefetch", InstanceFlags)!;
        Assert.Null(Record.Exception(() => method.Invoke(plugin, null)));
    }

    /// <summary>Disabled tooltip capture and rendering policies return without game services.</summary>
    /// <param name="policy">The existing runtime policy.</param>
    [Theory]
    [InlineData("ShouldPrefetchStructuredTooltips")]
    [InlineData("ShouldRunStructuredTooltipUiRuntime")]
    public void DisabledTooltips_PolicyReturnsBeforeGameAccess(string policy)
    {
        var plugin = CreateWithoutNativeServices(new Config { Translate = true, TranslateTooltips = false });
        Assert.Equal(false, typeof(PluginEntry).GetMethod(policy, InstanceFlags)!.Invoke(plugin, null));
    }

    /// <summary>One identity is suppressed during cooldown while changed content remains admissible.</summary>
    [Fact]
    public void VisibleMiss_CooldownSuppressesFramesAndExpires()
    {
        var plugin = CreateWithoutNativeServices(new Config());
        var requests = new Dictionary<string, DateTime>();
        typeof(PluginEntry).GetField("structuredTooltipOnDemandPrefetchLastRequestedUtc", InstanceFlags)!
            .SetValue(plugin, requests);
        var acquire = typeof(PluginEntry).GetMethod("TryAcquireStructuredTooltipOnDemandPrefetchSlot", InstanceFlags)!;

        Assert.Equal(TimeSpan.FromSeconds(30), typeof(PluginEntry)
            .GetField("StructuredTooltipOnDemandPrefetchCooldown", StaticFlags)!.GetValue(null));
        Assert.Equal(true, acquire.Invoke(plugin, ["same-content"]));
        for (var frame = 0; frame < 20; frame++)
        {
            Assert.Equal(false, acquire.Invoke(plugin, ["same-content"]));
        }

        Assert.Equal(true, acquire.Invoke(plugin, ["changed-content"]));
        requests["same-content"] = DateTime.UtcNow.AddSeconds(-31);
        Assert.Equal(true, acquire.Invoke(plugin, ["same-content"]));
        Assert.Equal(false, acquire.Invoke(plugin, ["same-content"]));
    }

    /// <summary>Visible misses pass through cooldown before admitting interactive persistence.</summary>
    /// <param name="domain">The tooltip domain.</param>
    [Theory]
    [InlineData("Action")]
    [InlineData("Trait")]
    [InlineData("Item")]
    public void VisibleMiss_UsesCooldownBeforeInteractiveAdmission(string domain)
    {
        var source = ReadRuntimeSource("ActionItemDetailUiRuntime.cs");
        var request = ReadMethod(source, "TryRequest" + domain + "DetailOnDemandPrefetch");
        var cooldown = request.IndexOf("TryAcquireStructuredTooltipOnDemandPrefetchSlot(", StringComparison.Ordinal);
        var admission = request.IndexOf("Start" + domain + "DetailOperation(", StringComparison.Ordinal);
        Assert.True(cooldown >= 0 && admission > cooldown,
            "Visible misses must acquire their cooldown before starting managed persistence work.");
        Assert.Contains("PersistencePriority.Interactive", request, StringComparison.Ordinal);

        var lookup = ReadMethod(source, "TryFindTranslated" + domain + "TooltipPayload");
        Assert.Contains("CacheManager.TryFindCanonicalMatch(", lookup, StringComparison.Ordinal);
        Assert.DoesNotContain("Start" + domain + "DetailOperation(", lookup, StringComparison.Ordinal);
        Assert.DoesNotContain("TryRead", lookup, StringComparison.Ordinal);
    }

    /// <summary>Creates only managed instance state so accidental native access fails the test.</summary>
    /// <param name="configuration">The runtime configuration.</param>
    /// <returns>An instance whose native services and per-surface queues are unavailable.</returns>
    private static PluginEntry CreateWithoutNativeServices(Config configuration)
    {
        var plugin = (PluginEntry)RuntimeHelpers.GetUninitializedObject(typeof(PluginEntry));
        typeof(PluginEntry).GetField("configuration", InstanceFlags)!.SetValue(plugin, configuration);
        return plugin;
    }

    /// <summary>Extracts a named method without depending on indentation or checkout line endings.</summary>
    /// <param name="source">The runtime source text.</param>
    /// <param name="name">The exact method name.</param>
    /// <returns>The declaration and body of the method.</returns>
    private static string ReadMethod(string source, string name)
    {
        var declaration = Regex.Match(source,
            @"\b(?:private|internal)\s+(?:static\s+)?(?:async\s+)?[\w<>?]+\s+" + Regex.Escape(name) + @"\s*\(");
        Assert.True(declaration.Success, "Missing runtime method " + name);
        var opening = source.IndexOf('{', declaration.Index);
        var depth = 0;
        for (var index = opening; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}' && --depth == 0)
            {
                return source[declaration.Index..(index + 1)];
            }
        }

        throw new InvalidOperationException("Incomplete runtime method " + name);
    }

    /// <summary>Reads one runtime file from the repository containing the test output.</summary>
    /// <param name="file">The runtime source filename.</param>
    /// <returns>The source text.</returns>
    private static string ReadRuntimeSource(string file)
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Echoglossian.sln")))
            {
                return File.ReadAllText(Path.Combine(directory.FullName, "NativeUI", "Helpers", file));
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root.");
    }
}
