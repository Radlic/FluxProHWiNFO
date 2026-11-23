using FluxProHWiNFO.Display;

namespace FluxProHWiNFO.Tests;

public class PayloadEncoderTests
{
    [Fact]
    public void Vector_Cpu24_Gpu16()
    {
        Assert.Equal("55 AA 01 01 06 02 04 00 01 06 00 14", PayloadEncoder.ToHex(PayloadEncoder.Encode(24.0, 16.0)));
    }

    [Fact]
    public void Vector_Cpu24_GpuMissing()
    {
        Assert.Equal("55 AA 01 01 06 02 04 00 EE EE EE D7", PayloadEncoder.ToHex(PayloadEncoder.Encode(24.0, null)));
    }

    [Fact]
    public void Blank_SendsEeForBoth()
    {
        // 0x107 (header) + 6 * 0xEE = 0x69B -> 0x9B
        Assert.Equal("55 AA 01 01 06 EE EE EE EE EE EE 9B",PayloadEncoder.ToHex(PayloadEncoder.EncodeBlank()));
    }

    [Fact]
    public void PayloadIsTwelveBytes()
    {
        Assert.Equal(12, PayloadEncoder.Encode(50, 50).Length);
    }

    [Theory]
    [InlineData(24.7, 247)]   // the Linux version truncates this to 24.6
    [InlineData(24.66, 247)]
    [InlineData(24.64, 246)]
    [InlineData(24.65, 247)]  // midpoint rounds away from zero
    [InlineData(9.5, 95)]
    [InlineData(0.0, 0)]
    [InlineData(99.9, 999)]
    [InlineData(99.94, 999)]
    [InlineData(99.96, 999)]  // clamp
    [InlineData(100.0, 999)]
    [InlineData(150.0, 999)]
    [InlineData(-5.0, 0)]
    [InlineData(-0.04, 0)]
    public void ToTenths_RoundsAndClamps(double input, int expected)
    {
        Assert.Equal(expected, PayloadEncoder.ToTenths(input));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ToTenths_NonFiniteIsMissing(double input)
    {
        Assert.Null(PayloadEncoder.ToTenths(input));
    }

    [Fact]
    public void Digits_AreTensUnitsTenths()
    {
        var p = PayloadEncoder.Encode(12.3, 45.6);
        Assert.Equal(new byte[] { 1, 2, 3 }, p[5..8]);
        Assert.Equal(new byte[] { 4, 5, 6 }, p[8..11]);
    }

    [Fact]
    public void Digits_SingleDigitTemperatureHasLeadingZero()
    {
        var p = PayloadEncoder.Encode(7.2, null);
        Assert.Equal(new byte[] { 0, 7, 2 }, p[5..8]);
    }

    [Fact]
    public void Clamp_HighTemperatureShows999()
    {
        var p = PayloadEncoder.Encode(105.3, 100.0);
        Assert.Equal(new byte[] { 9, 9, 9 }, p[5..8]);
        Assert.Equal(new byte[] { 9, 9, 9 }, p[8..11]);
    }

    [Fact]
    public void NaN_IsEncodedAsMissing()
    {
        var p = PayloadEncoder.Encode(double.NaN, 40.0);
        Assert.Equal(new byte[] { 0xEE, 0xEE, 0xEE }, p[5..8]);
    }

    [Theory]
    [InlineData(12.3, 45.6)]
    [InlineData(99.9, 99.9)]
    [InlineData(0.0, null)]
    public void Checksum_IsLowByteOfSum(double cpu, double? gpu)
    {
        var p = PayloadEncoder.Encode(cpu, gpu);
        int sum = p.Take(11).Sum(b => b);
        Assert.Equal((byte)(sum & 0xFF), p[11]);
    }
}
