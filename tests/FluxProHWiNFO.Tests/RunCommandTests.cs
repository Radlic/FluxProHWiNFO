using FluxProHWiNFO.Startup;
using Xunit;

namespace FluxProHWiNFO.Tests;

public class RunCommandTests
{
    private const string Exe = @"C:\Program Files\FluxProHWiNFO\FluxProHWiNFO.exe";

    [Fact]
    public void Build_QuotesPathAndAddsBackgroundArg() =>
        Assert.Equal($"\"{Exe}\" --background", RunCommand.Build(Exe));

    [Theory]
    [InlineData("\"C:\\Program Files\\FluxProHWiNFO\\FluxProHWiNFO.exe\" --background")]
    [InlineData("\"C:\\Program Files\\FluxProHWiNFO\\FluxProHWiNFO.exe\"")] // written by 0.1.0
    [InlineData("  \"C:\\Program Files\\FluxProHWiNFO\\FluxProHWiNFO.exe\"  ")]
    public void ExePathOf_QuotedPath(string command) =>
        Assert.Equal(Exe, RunCommand.ExePathOf(command));

    [Theory]
    [InlineData(@"C:\Apps\FluxProHWiNFO.exe", @"C:\Apps\FluxProHWiNFO.exe")]
    [InlineData(@"C:\Apps\FluxProHWiNFO.exe --background", @"C:\Apps\FluxProHWiNFO.exe")]
    [InlineData("\"C:\\Apps\\FluxProHWiNFO.exe", @"C:\Apps\FluxProHWiNFO.exe")] // missing closing quote
    public void ExePathOf_Unusual(string command, string expected) =>
        Assert.Equal(expected, RunCommand.ExePathOf(command));

    [Fact]
    public void PointsAt_IgnoresCaseAndArguments() =>
        Assert.True(RunCommand.PointsAt("\"c:\\program files\\fluxprohwinfo\\FLUXPROHWINFO.EXE\" --background", Exe));

    [Fact]
    public void PointsAt_OtherExeIsFalse() =>
        Assert.False(RunCommand.PointsAt(@"""C:\Old\FluxProHWiNFO.exe"" --background", Exe));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void PointsAt_UnknownExeIsFalse(string? exe) =>
        Assert.False(RunCommand.PointsAt($"\"{Exe}\"", exe));
}
