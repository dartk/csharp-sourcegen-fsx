using Xunit;


namespace CsxSourceGenerator.Tests;


public class Tests
{
    [Fact]
    public void CheckGeneratedContent()
    {
        var content = File.ReadAllText("content.txt");
        Assert.Equal(content, Generated.Content);
    }
}