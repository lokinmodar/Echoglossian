using Xunit;

namespace Echoglossian.Tests;

public class CleanStringTests
{
    [Fact]
    public void CleanString_RemovesDoubleSpacesAndNewlines()
    {
        var input = "Hello  World\nNext";
        var result = TestableEchoglossian.CleanString(input);
        Assert.Equal("Hello WorldNext", result);
    }

    [Fact]
    public void CleanString_PreservesTrailingFiveSpaces()
    {
        var input = "Hello  World     ";
        var result = TestableEchoglossian.CleanString(input);
        Assert.Equal("Hello World          ", result);
    }
}
