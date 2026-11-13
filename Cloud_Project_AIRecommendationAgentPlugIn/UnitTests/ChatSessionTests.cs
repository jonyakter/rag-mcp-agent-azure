using System.Reflection;
using MyCloudProject;
using Xunit;

namespace UnitTests;

// ChatSession's routing predicates are private static, so they're exercised here via
// reflection instead of changing their accessibility in MyCloudProject.
public class ChatSessionTests
{
    private static bool Invoke(string methodName, string arg)
    {
        var method = typeof(ChatSession).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (bool)method!.Invoke(null, new object[] { arg })!;
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("search", true)]
    [InlineData("Recommend", true)]
    [InlineData("2", false)]
    [InlineData("hello", false)]
    public void IsSearchChoice_MatchesExpected(string input, bool expected)
    {
        Assert.Equal(expected, Invoke("IsSearchChoice", input));
    }

    [Theory]
    [InlineData("2", true)]
    [InlineData("upload", true)]
    [InlineData("dataset", true)]
    [InlineData("please upload this file", true)]
    [InlineData("1", false)]
    [InlineData("search", false)]
    public void IsUploadChoice_MatchesExpected(string input, bool expected)
    {
        Assert.Equal(expected, Invoke("IsUploadChoice", input));
    }

    [Theory]
    [InlineData("/exit", true)]
    [InlineData("quit", true)]
    [InlineData("BYE", true)]
    [InlineData("goodbye", true)]
    [InlineData("hello", false)]
    public void IsExitCommand_MatchesExpected(string input, bool expected)
    {
        Assert.Equal(expected, Invoke("IsExitCommand", input));
    }
}
