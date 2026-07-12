// <copyright file="DbTableNameMatcherTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.DBManagerUI.Services;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers exact table-name matching used by debugger-to-DB-manager handoff.
/// </summary>
public class DbTableNameMatcherTests
{
  /// <summary>
  ///     Ensures exact matches are returned unchanged.
  /// </summary>
  [Fact]
  public void Match_ReturnsExactTableName()
  {
    var matched = DbTableNameMatcher.Match(
        ["TalkMessage", "BattleTalkMessage", "SelectString"],
        "SelectString");

    Assert.Equal("SelectString", matched);
  }

  /// <summary>
  ///     Ensures unknown requests do not resolve to arbitrary tables.
  /// </summary>
  [Fact]
  public void Match_ReturnsNullForUnknownTable()
  {
    var matched = DbTableNameMatcher.Match(
        ["TalkMessage", "BattleTalkMessage"],
        "UnknownTable");

    Assert.Null(matched);
  }
}
