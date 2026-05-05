// <copyright file="DbOperationsTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Xunit;

using PluginEntry = Echoglossian.Echoglossian;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the DB-side guard that decides whether translated text is safe
///     to persist.
/// </summary>
public class DbOperationsTests
{
    /// <summary>
    ///     Ensures synthetic translation-error placeholders are never treated
    ///     as valid rows to persist.
    /// </summary>
    [Fact]
    public void ShouldSaveToDB_RejectsSyntheticTranslationError()
    {
        var shouldSave = PluginEntry.ShouldSaveToDB(
            "[Translation Error: LmStudio: No connection could be made]");

        Assert.False(shouldSave);
    }

    /// <summary>
    ///     Ensures ordinary translated content still remains persistable.
    /// </summary>
    [Fact]
    public void ShouldSaveToDB_AcceptsNormalTranslatedText()
    {
        var shouldSave = PluginEntry.ShouldSaveToDB("O trabalho me deixa exausto.");

        Assert.True(shouldSave);
    }
}
