// <copyright file="ToDoPayloadTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Quest;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the dedicated ToDo payload identity contract.
/// </summary>
public sealed class ToDoPayloadTests
{
    /// <summary>
    ///     Ensures timer text does not change the persisted source identity.
    /// </summary>
    [Fact]
    public void ComputeSourceContentHash_ExcludesTimerNode()
    {
        var payload = new ToDoPayload(
            [
                new ToDoCapturedText(100, "Halatali", false),
                new ToDoCapturedText(101, "80:38", true),
                new ToDoCapturedText(102, "Clear the Hall of the Cesti: 0/1", false),
            ]);

        var baselineHash = payload.ComputeSourceContentHash();
        var changedTimerHash = new ToDoPayload(
            [
                new ToDoCapturedText(100, "Halatali", false),
                new ToDoCapturedText(101, "80:37", true),
                new ToDoCapturedText(102, "Clear the Hall of the Cesti: 0/1", false),
            ]).ComputeSourceContentHash();

        Assert.Equal(baselineHash, changedTimerHash);
    }

    /// <summary>
    ///     Ensures repeated native node ids remain distinct when component
    ///     traversal assigns different structural keys.
    /// </summary>
    [Fact]
    public void ComputeSourceContentHash_DistinguishesRepeatedNodeIdsByStructuralKey()
    {
        var firstPayload = new ToDoPayload(
            [
                new ToDoCapturedText("17:0", 17, "First objective", false),
                new ToDoCapturedText("17:1", 17, "Second objective", false),
            ]);
        var reorderedPayload = new ToDoPayload(
            [
                new ToDoCapturedText("17:0", 17, "Second objective", false),
                new ToDoCapturedText("17:1", 17, "First objective", false),
            ]);

        Assert.NotEqual(
            firstPayload.ComputeSourceContentHash(),
            reorderedPayload.ComputeSourceContentHash());
    }
}
