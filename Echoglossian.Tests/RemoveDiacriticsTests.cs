using System.Collections.Generic;

using Xunit;

namespace Echoglossian.Tests;

public class RemoveDiacriticsTests
{
    private static TestableEchoglossian CreateInstance() => new();

    [Fact]
    public void RemoveDiacritics_KeepsSupportedCharacters()
    {
        var instance = CreateInstance();
        var supported = new HashSet<char> { 'é' };
        var result = instance.RemoveDiacritics("café", supported);
        Assert.Equal("café", result);
    }

    [Fact]
    public void RemoveDiacritics_RemovesUnsupportedCharacters()
    {
        var instance = CreateInstance();
        var supported = new HashSet<char>();
        var result = instance.RemoveDiacritics("café", supported);
        Assert.Equal("cafe", result);
    }

    [Fact]
    public void RemoveDiacritics_UsesCustomReplacements()
    {
        var instance = CreateInstance();
        var supported = new HashSet<char>();
        var result = instance.RemoveDiacritics("Łódź", supported);
        Assert.Equal("Lodz", result);
    }
}
