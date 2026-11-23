using FluxProHWiNFO.Display;

namespace FluxProHWiNFO.Tests;

public class HidReportBuilderTests
{
    private static readonly byte[] Payload = PayloadEncoder.Encode(12.3, 45.6);

    [Fact]
    public void ReportIdZeroPadded_PrefixesZeroAndPadsToReportLength()
    {
        var r = HidReportBuilder.Build(Payload, ReportFraming.ReportIdZeroPadded, 65);
        Assert.Equal(65, r.Length);
        Assert.Equal(0x00, r[0]);
        Assert.Equal(Payload, r[1..13]);
        Assert.All(r[13..], b => Assert.Equal(0, b));
    }

    [Fact]
    public void ReportIdZero_IsThirteenBytes()
    {
        var r = HidReportBuilder.Build(Payload, ReportFraming.ReportIdZero, 65);
        Assert.Equal(13, r.Length);
        Assert.Equal(0x00, r[0]);
        Assert.Equal(Payload, r[1..]);
    }

    [Fact]
    public void NoReportIdPadded_StartsWithHeader()
    {
        var r = HidReportBuilder.Build(Payload, ReportFraming.NoReportIdPadded, 64);
        Assert.Equal(64, r.Length);
        Assert.Equal(Payload, r[..12]);
    }

    [Fact]
    public void Padding_NeverTruncatesWhenReportLengthIsShort()
    {
        var r = HidReportBuilder.Build(Payload, ReportFraming.ReportIdZeroPadded, 8);
        Assert.Equal(13, r.Length);
        Assert.Equal(Payload, r[1..]);
    }

    [Fact]
    public void Padding_UnknownLengthZeroFallsBackToContentLength()
    {
        Assert.Equal(13, HidReportBuilder.Build(Payload, ReportFraming.ReportIdZeroPadded, 0).Length);
    }

    [Fact]
    public void CliNames_RoundTrip()
    {
        foreach (var f in Enum.GetValues<ReportFraming>())
            Assert.Equal(f, HidReportBuilder.CliNames[HidReportBuilder.CliName(f)]);
    }
}
