using FluxProHWiNFO.Startup;
using Xunit;

namespace FluxProHWiNFO.Tests;

public class StartupApprovalTests
{
    [Fact]
    public void Enabled_MatchesTaskManagerFormat() =>
        Assert.Equal(new byte[] { 0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, StartupApproval.Enabled);

    [Fact]
    public void Enabled_IsAFreshCopyEachTime()
    {
        StartupApproval.Enabled[0] = 0x03;
        Assert.Equal(0x02, StartupApproval.Enabled[0]);
    }

    [Fact]
    public void EnabledValue_IsEnabled() => Assert.True(StartupApproval.IsEnabled(StartupApproval.Enabled));

    [Theory]
    [InlineData(new byte[] { 0x03, 0, 0, 0, 0x6E, 0x91, 0xD9, 0xBD, 0x4D, 0x02, 0xDC, 0x01 })] // disabled in Task Manager
    [InlineData(new byte[] { 0x01, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 })]
    [InlineData(new byte[] { 0x07, 0, 0, 0, 0xBB, 0x22, 0xD3, 0xBE, 0x4D, 0x02, 0xDC, 0x01 })]
    [InlineData(new byte[0])]
    public void DisabledOrEmpty_IsNotEnabled(byte[] value) => Assert.False(StartupApproval.IsEnabled(value));

    [Fact]
    public void Missing_IsNotEnabled() => Assert.False(StartupApproval.IsEnabled(null));
}
