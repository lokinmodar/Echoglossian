// <copyright file="AddonStructureProbe.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
/// Provides a generic recursive addon probe for live UI inspection.
/// </summary>
/// <remarks>
/// The traversal pattern is intentionally generic and was inspired by the
/// recursive addon walk used in SimpleTweaksPlugin's UIDebug helper and the
/// addon inspection approach used by HaselDebug. The goal here is to keep the
/// probe passive, reusable, and focused on structural logging rather than a
/// dedicated UI panel.
/// </remarks>
internal static unsafe class AddonStructureProbe
{
  private const int DefaultMaxDepth = 32;
  private const int DefaultMaxNodes = 4096;

  private static readonly ConcurrentDictionary<string, AddonStructureProbeSnapshot>
      LatestSnapshots = new();

  /// <summary>
  /// Dumps a recursive probe of the requested addon to the log.
  /// </summary>
  /// <param name="gameGui">The game GUI service used to resolve the addon.</param>
  /// <param name="pluginLog">The plugin log used to emit the probe output.</param>
  /// <param name="addonName">The addon name to inspect.</param>
  /// <param name="index">The addon instance index.</param>
  /// <param name="maxDepth">Maximum recursion depth for the probe.</param>
  /// <param name="maxNodes">Maximum number of visited nodes before aborting.</param>
  /// <returns><see langword="true" /> if the addon was found and probed; otherwise <see langword="false" />.</returns>
  public static bool ProbeAddon(
      IGameGui gameGui,
      IPluginLog pluginLog,
      string addonName,
      int index = 0,
      int maxDepth = DefaultMaxDepth,
      int maxNodes = DefaultMaxNodes)
  {
    if (!TryResolveAddonPointer(gameGui, addonName, index, out var addon))
    {
      pluginLog.Warning(
          $"[AddonProbe] addon='{addonName}' index={index} was not found.");
      return false;
    }

    return ProbeAddon(
        addon,
        pluginLog,
        addonName,
        index,
        trigger: "manual",
        maxDepth,
        maxNodes);
  }

  /// <summary>
  /// Dumps a recursive probe for an already-resolved addon pointer.
  /// </summary>
  /// <param name="addon">The resolved addon pointer to inspect.</param>
  /// <param name="pluginLog">The plugin log used to emit the probe output.</param>
  /// <param name="addonName">The addon name to inspect.</param>
  /// <param name="index">The addon instance index.</param>
  /// <param name="trigger">Optional trigger label used in the logs.</param>
  /// <param name="maxDepth">Maximum recursion depth for the probe.</param>
  /// <param name="maxNodes">Maximum number of visited nodes before aborting.</param>
  /// <returns><see langword="true" /> if the addon pointer was valid and probed; otherwise <see langword="false" />.</returns>
  public static bool ProbeAddon(
      AtkUnitBase* addon,
      IPluginLog pluginLog,
      string addonName,
      int index = 0,
      string? trigger = null,
      int maxDepth = DefaultMaxDepth,
      int maxNodes = DefaultMaxNodes)
  {
    if (addon == null)
    {
      pluginLog.Warning(
          $"[AddonProbe] addon='{addonName}' index={index} resolved to a null pointer.");
      return false;
    }

    var snapshot = new AddonStructureProbeSnapshot(addonName.Trim(), index)
    {
      RootNodeAddress = (nint)addon->UldManager.RootNode,
      IsVisible = addon->IsVisible,
      NodeListCount = (int)addon->UldManager.NodeListCount,
      CollisionNodeListCount = (int)addon->CollisionNodeListCount,
    };

    var visitedNodes = new HashSet<nint>();
    pluginLog.Information(
        $"[AddonProbe] Starting probe addon='{snapshot.AddonName}' index={index} trigger='{trigger ?? "manual"}' ptr=0x{(ulong)(nint)addon:X} visible={snapshot.IsVisible} root=0x{(ulong)snapshot.RootNodeAddress:X} nodeList={snapshot.NodeListCount} collisionList={snapshot.CollisionNodeListCount}");

    if (addon->UldManager.RootNode != null)
    {
      WalkNode(
          pluginLog,
          snapshot,
          addon->UldManager.RootNode,
          "RootNode",
          0,
          maxDepth,
          maxNodes,
          visitedNodes);
    }

    WalkNodeList(
        pluginLog,
        snapshot,
        addon->UldManager.NodeList,
        (int)addon->UldManager.NodeListCount,
        "NodeList",
        maxDepth,
        maxNodes,
        visitedNodes);

    WalkNodeList(
        pluginLog,
        snapshot,
        addon->CollisionNodeList,
        (int)addon->CollisionNodeListCount,
        "CollisionNodeList",
        maxDepth,
        maxNodes,
        visitedNodes);

    LatestSnapshots[GetSnapshotKey(snapshot.AddonName, snapshot.Index)] = snapshot;

    pluginLog.Information(
        $"[AddonProbe] Summary addon='{snapshot.AddonName}' index={index} nodes={snapshot.NodeCount} textNodes={snapshot.TextNodeCount} visibleTextNodes={snapshot.VisibleTextNodeCount} componentNodes={snapshot.ComponentNodeCount} bestAnchor='{snapshot.BestTextAnchorPath ?? "<none>"}' bestText='{NormalizeForLog(snapshot.BestTextAnchorText) ?? "<none>"}'");

    return true;
  }

  /// <summary>
  /// Attempts to resolve an addon pointer by name and optional index.
  /// </summary>
  /// <param name="gameGui">The game GUI service used for the initial lookup.</param>
  /// <param name="addonName">The addon name to resolve.</param>
  /// <param name="index">The addon instance index.</param>
  /// <param name="addon">The resolved addon pointer, when found.</param>
  /// <returns><see langword="true" /> when the addon was resolved.</returns>
  private static bool TryResolveAddonPointer(
      IGameGui gameGui,
      string addonName,
      int index,
      out AtkUnitBase* addon)
  {
    addon = null;

    if (string.IsNullOrWhiteSpace(addonName))
    {
      return false;
    }

    var addonPtr = gameGui.GetAddonByName(addonName, index);
    if (addonPtr != IntPtr.Zero)
    {
      addon = (AtkUnitBase*)addonPtr.Address;
      return true;
    }

    if (!global::Echoglossian.FrameworkAccessGuard.TryGetRaptureAtkUnitManager(out var manager))
    {
      return false;
    }

    var matchIndex = 0;
    foreach (var unit in manager->AllLoadedUnitsList.Entries)
    {
      var unitPtr = unit.Value;
      if (unitPtr == null || !unitPtr->IsReady)
      {
        continue;
      }

      if (!string.Equals(unitPtr->NameString, addonName, StringComparison.Ordinal))
      {
        continue;
      }

      if (matchIndex++ != index)
      {
        continue;
      }

      addon = unitPtr;
      return true;
    }

    return false;
  }

  /// <summary>
  /// Starts a one-minute addon watch that polls for the addon and captures a
  /// complete structural dump as soon as the addon becomes live.
  /// </summary>
  /// <param name="gameGui">The game GUI service used to resolve the addon.</param>
  /// <param name="pluginLog">The plugin log used to emit the probe output.</param>
  /// <param name="addonName">The addon name to watch.</param>
  /// <param name="index">The addon instance index.</param>
  /// <param name="duration">How long the watch should stay active.</param>
  /// <returns>The active watch instance.</returns>
  public static AddonStructureProbeWatch StartWatch(
      IGameGui gameGui,
      IPluginLog pluginLog,
      string addonName,
      int index = 0,
      TimeSpan? duration = null)
  {
    return new AddonStructureProbeWatch(
        gameGui,
        pluginLog,
        addonName,
        index,
        duration ?? TimeSpan.FromMinutes(1));
  }

  /// <summary>
  /// Attempts to retrieve the latest cached snapshot for an addon probe.
  /// </summary>
  /// <param name="addonName">The addon name.</param>
  /// <param name="index">The addon instance index.</param>
  /// <param name="snapshot">The cached snapshot, if any.</param>
  /// <returns><see langword="true" /> when a cached snapshot was found.</returns>
  internal static bool TryGetLatestSnapshot(
      string addonName,
      int index,
      out AddonStructureProbeSnapshot? snapshot)
  {
    return LatestSnapshots.TryGetValue(
        GetSnapshotKey(addonName, index),
        out snapshot);
  }

  /// <summary>
  /// Walks a node list and logs each entry recursively.
  /// </summary>
  /// <param name="pluginLog">The plugin log used for output.</param>
  /// <param name="snapshot">The probe snapshot being populated.</param>
  /// <param name="nodeList">The node list pointer.</param>
  /// <param name="nodeCount">The number of nodes in the list.</param>
  /// <param name="listName">The logical name of the list.</param>
  /// <param name="maxDepth">Maximum recursion depth.</param>
  /// <param name="maxNodes">Maximum number of visited nodes.</param>
  /// <param name="visitedNodes">The visited-node guard set.</param>
  private static void WalkNodeList(
      IPluginLog pluginLog,
      AddonStructureProbeSnapshot snapshot,
      AtkResNode** nodeList,
      int nodeCount,
      string listName,
      int maxDepth,
      int maxNodes,
      HashSet<nint> visitedNodes)
  {
    if (nodeList == null || nodeCount <= 0)
    {
      return;
    }

    for (var i = 0; i < nodeCount; i++)
    {
      var node = nodeList[i];
      if (node == null)
      {
        continue;
      }

      WalkNode(
          pluginLog,
          snapshot,
          node,
          $"{listName}[{i}]",
          0,
          maxDepth,
          maxNodes,
          visitedNodes);
    }
  }

  /// <summary>
  /// Walks a single node recursively, including sibling chains and component roots.
  /// </summary>
  /// <param name="pluginLog">The plugin log used for output.</param>
  /// <param name="snapshot">The probe snapshot being populated.</param>
  /// <param name="node">The current node.</param>
  /// <param name="path">The logical path of the current node within the addon.</param>
  /// <param name="depth">The current recursion depth.</param>
  /// <param name="maxDepth">Maximum recursion depth.</param>
  /// <param name="maxNodes">Maximum number of visited nodes.</param>
  /// <param name="visitedNodes">The visited-node guard set.</param>
  private static void WalkNode(
      IPluginLog pluginLog,
      AddonStructureProbeSnapshot snapshot,
      AtkResNode* node,
      string path,
      int depth,
      int maxDepth,
      int maxNodes,
      HashSet<nint> visitedNodes)
  {
    if (node == null)
    {
      return;
    }

    if (depth > maxDepth)
    {
      pluginLog.Debug(
          $"[AddonProbe] addon='{snapshot.AddonName}' index={snapshot.Index} path={path} depth={depth} max-depth reached");
      return;
    }

    var nodeAddress = (nint)node;
    if (!visitedNodes.Add(nodeAddress))
    {
      return;
    }

    snapshot.NodeCount++;
    if (snapshot.NodeCount > maxNodes)
    {
      pluginLog.Warning(
          $"[AddonProbe] addon='{snapshot.AddonName}' index={snapshot.Index} aborted after {snapshot.NodeCount} nodes to avoid runaway traversal.");
      return;
    }

    var text = ReadNodeText(node);
    if (node->Type == NodeType.Text)
    {
      snapshot.TextNodeCount++;
      if (node->IsVisible() && !string.IsNullOrWhiteSpace(text))
      {
        snapshot.VisibleTextNodeCount++;
      }
    }

    if ((ushort)node->Type >= 1000)
    {
      snapshot.ComponentNodeCount++;
    }

    var nodeDescription = DescribeNode(node, text);
    pluginLog.Debug(
        $"[AddonProbe] addon='{snapshot.AddonName}' index={snapshot.Index} depth={depth} path={path} {nodeDescription}");

    if (snapshot.BestTextAnchorPath == null &&
        node->Type == NodeType.Text &&
        node->IsVisible() &&
        !string.IsNullOrWhiteSpace(text))
    {
      snapshot.BestTextAnchorPath = path;
      snapshot.BestTextAnchorText = text;
    }

    if ((ushort)node->Type >= 1000)
    {
      var componentNode = (AtkComponentNode*)node;
      if (componentNode->Component != null &&
          componentNode->Component->UldManager.RootNode != null)
      {
        var componentRoot = componentNode->Component->UldManager.RootNode;
        pluginLog.Debug(
            $"[AddonProbe] addon='{snapshot.AddonName}' index={snapshot.Index} path={path} component-root=0x{(ulong)(nint)componentRoot:X}");
        WalkNode(
            pluginLog,
            snapshot,
            componentRoot,
            $"{path}/ComponentRoot",
            depth + 1,
            maxDepth,
            maxNodes,
            visitedNodes);
      }

      if (componentNode->Component != null &&
          componentNode->Component->UldManager.NodeList != null &&
          componentNode->Component->UldManager.NodeListCount > 0)
      {
        WalkNodeList(
            pluginLog,
            snapshot,
            componentNode->Component->UldManager.NodeList,
            (int)componentNode->Component->UldManager.NodeListCount,
            $"{path}/ComponentNodes",
            maxDepth,
            maxNodes,
            visitedNodes);
      }
    }

    if (node->ChildNode != null)
    {
      WalkNode(
          pluginLog,
          snapshot,
          node->ChildNode,
          $"{path}/Child",
          depth + 1,
          maxDepth,
          maxNodes,
          visitedNodes);
    }

    if (node->NextSiblingNode != null)
    {
      WalkNode(
          pluginLog,
          snapshot,
          node->NextSiblingNode,
          $"{path}/Next",
          depth,
          maxDepth,
          maxNodes,
          visitedNodes);
    }
  }

  /// <summary>
  /// Reads the visible text from a node when the node is text-based.
  /// </summary>
  /// <param name="node">The node being inspected.</param>
  /// <returns>The text value or <see langword="null" /> when unavailable.</returns>
  private static string? ReadNodeText(AtkResNode* node)
  {
    if (node == null || node->Type != NodeType.Text)
    {
      return null;
    }

    var textNode = (AtkTextNode*)node;
    return textNode->NodeText.IsEmpty ? string.Empty : textNode->NodeText.ToString();
  }

  /// <summary>
  /// Builds a concise textual description of a node for the debug log.
  /// </summary>
  /// <param name="node">The node being described.</param>
  /// <param name="text">Optional text contents for text nodes.</param>
  /// <returns>A log-friendly node description.</returns>
  private static string DescribeNode(AtkResNode* node, string? text)
  {
    var shortText = NormalizeForLog(text, 120) ?? "<empty>";
    return
        $"addr=0x{(ulong)(nint)node:X} type={node->Type} nodeId={node->NodeId} visible={node->IsVisible()} x={node->X} y={node->Y} width={node->Width} height={node->Height} scale=({node->ScaleX},{node->ScaleY}) depth={node->Depth} flags={node->NodeFlags} drawFlags={node->DrawFlags} text='{shortText}'";
  }

  /// <summary>
  /// Normalizes a string so it is safe and compact for log output.
  /// </summary>
  /// <param name="text">The text to normalize.</param>
  /// <param name="maxLength">The maximum length to preserve.</param>
  /// <returns>The normalized text or <see langword="null" /> when the input is empty.</returns>
  private static string? NormalizeForLog(string? text, int maxLength = 160)
  {
    if (string.IsNullOrWhiteSpace(text))
    {
      return null;
    }

    var normalized = text
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("\t", "\\t", StringComparison.Ordinal)
        .Trim();

    return normalized.Length <= maxLength
        ? normalized
        : normalized[..maxLength] + "...";
  }

  /// <summary>
  /// Generates a stable cache key for a probe snapshot.
  /// </summary>
  /// <param name="addonName">The addon name.</param>
  /// <param name="index">The addon instance index.</param>
  /// <returns>The cache key.</returns>
  private static string GetSnapshotKey(string addonName, int index)
  {
    return $"{addonName.Trim().ToLowerInvariant()}[{index}]";
  }

  /// <summary>
  /// Represents a live probe watch for an addon name.
  /// </summary>
  internal sealed class AddonStructureProbeWatch : IDisposable
  {
    private readonly IGameGui gameGui;
    private readonly DateTimeOffset expiresAt;
    private readonly IPluginLog pluginLog;
    private readonly string addonName;
    private readonly int index;
    private nint lastDumpedAddonAddress;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddonStructureProbeWatch"/> class.
    /// </summary>
    /// <param name="gameGui">The game GUI service used to resolve the addon.</param>
    /// <param name="pluginLog">The plugin log used to emit watch output.</param>
    /// <param name="addonName">The addon name being watched.</param>
    /// <param name="index">The addon instance index.</param>
    /// <param name="duration">How long the watch should stay active.</param>
    public AddonStructureProbeWatch(
        IGameGui gameGui,
        IPluginLog pluginLog,
        string addonName,
        int index,
        TimeSpan duration)
    {
      this.gameGui = gameGui;
      this.pluginLog = pluginLog;
      this.addonName = addonName.Trim();
      this.index = index;
      this.expiresAt = DateTimeOffset.UtcNow + duration;

      this.pluginLog.Information(
          $"[AddonProbe] Watching addon='{this.addonName}' index={this.index} for {(int)duration.TotalSeconds}s");

      this.TryDumpCurrent("watch-start");
    }

    /// <summary>
    /// Ticks the watch, polls for the addon, and disposes it when the time budget is exhausted.
    /// </summary>
    public void Tick()
    {
      if (this.disposed)
      {
        return;
      }

      this.TryDumpCurrent("watch-poll");

      if (DateTimeOffset.UtcNow >= this.expiresAt)
      {
        this.Dispose();
      }
    }

    /// <summary>
    /// Disposes the watch and ends the probing session.
    /// </summary>
    public void Dispose()
    {
      this.DisposeInternal("finished");
    }

    /// <summary>
    /// Stops the watch early and ends the probing session.
    /// </summary>
    public void Stop()
    {
      this.DisposeInternal("stopped");
    }

    /// <summary>
    /// Gets a value indicating whether the watch already finished.
    /// </summary>
    internal bool IsDisposed => this.disposed;

    /// <summary>
    /// Disposes the watch and logs why the probing session ended.
    /// </summary>
    /// <param name="reason">The reason logged when the watch ends.</param>
    private void DisposeInternal(string reason)
    {
      if (this.disposed)
      {
        return;
      }

      this.disposed = true;

      this.pluginLog.Information(
          $"[AddonProbe] Watch {reason} addon='{this.addonName}' index={this.index}");
    }

    /// <summary>
    /// Attempts to dump the currently live addon immediately when the watch starts
    /// or when a later tick finds it alive.
    /// </summary>
    /// <param name="trigger">A label describing why the dump is happening.</param>
    private void TryDumpCurrent(string trigger)
    {
      if (this.disposed)
      {
        return;
      }

      if (!TryResolveAddonPointer(this.gameGui, this.addonName, this.index, out var addon))
      {
        return;
      }

      if ((nint)addon == this.lastDumpedAddonAddress)
      {
        return;
      }

      this.lastDumpedAddonAddress = (nint)addon;

      ProbeAddon(
          addon,
          this.pluginLog,
          this.addonName,
          this.index,
          trigger);
    }
  }

  /// <summary>
  /// Represents the latest known summary for a probed addon.
  /// </summary>
  internal sealed class AddonStructureProbeSnapshot
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="AddonStructureProbeSnapshot"/> class.
    /// </summary>
    /// <param name="addonName">The addon name.</param>
    /// <param name="index">The addon index.</param>
    public AddonStructureProbeSnapshot(string addonName, int index)
    {
      this.AddonName = addonName;
      this.Index = index;
      this.CapturedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets the addon name.
    /// </summary>
    public string AddonName { get; }

    /// <summary>
    /// Gets the addon instance index.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the time the snapshot was captured.
    /// </summary>
    public DateTimeOffset CapturedAt { get; }

    /// <summary>
    /// Gets or sets whether the addon was visible when probed.
    /// </summary>
    public bool IsVisible { get; set; }

    /// <summary>
    /// Gets or sets the root node address.
    /// </summary>
    public nint RootNodeAddress { get; set; }

    /// <summary>
    /// Gets or sets the number of nodes visited during the probe.
    /// </summary>
    public int NodeCount { get; set; }

    /// <summary>
    /// Gets or sets the number of text nodes encountered during the probe.
    /// </summary>
    public int TextNodeCount { get; set; }

    /// <summary>
    /// Gets or sets the number of visible, non-empty text nodes encountered.
    /// </summary>
    public int VisibleTextNodeCount { get; set; }

    /// <summary>
    /// Gets or sets the number of component nodes encountered.
    /// </summary>
    public int ComponentNodeCount { get; set; }

    /// <summary>
    /// Gets or sets the addon node-list count.
    /// </summary>
    public int NodeListCount { get; set; }

    /// <summary>
    /// Gets or sets the addon collision-node-list count.
    /// </summary>
    public int CollisionNodeListCount { get; set; }

    /// <summary>
    /// Gets or sets the path of the first visible text node that looked like a
    /// good anchor candidate.
    /// </summary>
    public string? BestTextAnchorPath { get; set; }

    /// <summary>
    /// Gets or sets the text of the first visible text node that looked like a
    /// good anchor candidate.
    /// </summary>
    public string? BestTextAnchorText { get; set; }
  }
}
