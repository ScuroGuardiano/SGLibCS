namespace SGLibCS.Ms.Tests;

// This file was mainly translated from JS using ChatGPT.
// Sorry but my mental was very low after manually translating
// Over 200 test cases.

public class MsConverter_ToStringShould
{
    [Theory]
    [InlineData(Double.NaN)]
    [InlineData(Double.PositiveInfinity)]
    [InlineData(Double.NegativeInfinity)]
    public void ToString_ThrowsOnInvalid(Double input)
    {
        Assert.Throws<ArgumentException>(() => MsConverter.ToString(input));
        Assert.Throws<ArgumentException>(() => MsConverter.ToString(input, new() { Long = true }));
    }

    [Fact]
    public void ToString_Long_SupportMilliseconds()
    {
        var result1 = MsConverter.ToString(500, new() { Long = true });
        Assert.Equal("500 ms", result1);

        var result2 = MsConverter.ToString(-500, new() { Long = true });
        Assert.Equal("-500 ms", result2);
    }

    [Fact]
    public void ToString_Long_SupportSeconds()
    {
        var result1 = MsConverter.ToString(1000, new() { Long = true });
        Assert.Equal("1 second", result1);

        var result2 = MsConverter.ToString(1200, new() { Long = true });
        Assert.Equal("1 second", result2);

        var result3 = MsConverter.ToString(10000, new() { Long = true });
        Assert.Equal("10 seconds", result3);

        var result4 = MsConverter.ToString(-1000, new() { Long = true });
        Assert.Equal("-1 second", result4);

        var result5 = MsConverter.ToString(-1200, new() { Long = true });
        Assert.Equal("-1 second", result5);

        var result6 = MsConverter.ToString(-10000, new() { Long = true });
        Assert.Equal("-10 seconds", result6);
    }

    [Fact]
    public void ToString_Long_SupportMinutes()
    {
        var result1 = MsConverter.ToString(60 * 1000, new() { Long = true });
        Assert.Equal("1 minute", result1);

        var result2 = MsConverter.ToString(60 * 1200, new() { Long = true });
        Assert.Equal("1 minute", result2);

        var result3 = MsConverter.ToString(60 * 10000, new() { Long = true });
        Assert.Equal("10 minutes", result3);

        var result4 = MsConverter.ToString(-1 * 60 * 1000, new() { Long = true });
        Assert.Equal("-1 minute", result4);

        var result5 = MsConverter.ToString(-1 * 60 * 1200, new() { Long = true });
        Assert.Equal("-1 minute", result5);

        var result6 = MsConverter.ToString(-1 * 60 * 10000, new() { Long = true });
        Assert.Equal("-10 minutes", result6);
    }

    [Fact]
    public void ToString_Long_SupportHours()
    {
        var result1 = MsConverter.ToString(60 * 60 * 1000, new() { Long = true });
        Assert.Equal("1 hour", result1);

        var result2 = MsConverter.ToString(60 * 60 * 1200, new() { Long = true });
        Assert.Equal("1 hour", result2);

        var result3 = MsConverter.ToString(60 * 60 * 10000, new() { Long = true });
        Assert.Equal("10 hours", result3);

        var result4 = MsConverter.ToString(-1 * 60 * 60 * 1000, new() { Long = true });
        Assert.Equal("-1 hour", result4);

        var result5 = MsConverter.ToString(-1 * 60 * 60 * 1200, new() { Long = true });
        Assert.Equal("-1 hour", result5);

        var result6 = MsConverter.ToString(-1 * 60 * 60 * 10000, new() { Long = true });
        Assert.Equal("-10 hours", result6);
    }

    [Fact]
    public void ToString_Long_SupportDays()
    {
        var result1 = MsConverter.ToString(24 * 60 * 60 * 1000, new() { Long = true });
        Assert.Equal("1 day", result1);

        var result2 = MsConverter.ToString(24 * 60 * 60 * 1200, new() { Long = true });
        Assert.Equal("1 day", result2);

        var result3 = MsConverter.ToString(24 * 60 * 60 * 10000, new() { Long = true });
        Assert.Equal("10 days", result3);

        var result4 = MsConverter.ToString(-1 * 24 * 60 * 60 * 1000, new() { Long = true });
        Assert.Equal("-1 day", result4);

        var result5 = MsConverter.ToString(-1 * 24 * 60 * 60 * 1200, new() { Long = true });
        Assert.Equal("-1 day", result5);

        var result6 = MsConverter.ToString(-1 * 24 * 60 * 60 * 10000, new() { Long = true });
        Assert.Equal("-10 days", result6);
    }

    [Fact]
    public void ToString_Long_Round()
    {
        var result1 = MsConverter.ToString(234234234, new() { Long = true });
        Assert.Equal("3 days", result1);

        var result2 = MsConverter.ToString(-234234234, new() { Long = true });
        Assert.Equal("-3 days", result2);
    }


    [Fact]
    public void ToString_Short_SupportMilliseconds()
    {
        var result1 = MsConverter.ToString(500);
        Assert.Equal("500ms", result1);

        var result2 = MsConverter.ToString(-500);
        Assert.Equal("-500ms", result2);
    }

    [Fact]
    public void ToString_Short_SupportSeconds()
    {
        var result1 = MsConverter.ToString(1000);
        Assert.Equal("1s", result1);

        var result2 = MsConverter.ToString(10000);
        Assert.Equal("10s", result2);

        var result3 = MsConverter.ToString(-1000);
        Assert.Equal("-1s", result3);

        var result4 = MsConverter.ToString(-10000);
        Assert.Equal("-10s", result4);
    }

    [Fact]
    public void ToString_Short_SupportMinutes()
    {
        var result1 = MsConverter.ToString(60 * 1000);
        Assert.Equal("1m", result1);

        var result2 = MsConverter.ToString(60 * 10000);
        Assert.Equal("10m", result2);

        var result3 = MsConverter.ToString(-1 * 60 * 1000);
        Assert.Equal("-1m", result3);

        var result4 = MsConverter.ToString(-1 * 60 * 10000);
        Assert.Equal("-10m", result4);
    }

    [Fact]
    public void ToString_Short_SupportHours()
    {
        var result1 = MsConverter.ToString(60 * 60 * 1000);
        Assert.Equal("1h", result1);

        var result2 = MsConverter.ToString(60 * 60 * 10000);
        Assert.Equal("10h", result2);

        var result3 = MsConverter.ToString(-1 * 60 * 60 * 1000);
        Assert.Equal("-1h", result3);

        var result4 = MsConverter.ToString(-1 * 60 * 60 * 10000);
        Assert.Equal("-10h", result4);
    }

    [Fact]
    public void ToString_Short_SupportDays()
    {
        var result1 = MsConverter.ToString(24 * 60 * 60 * 1000);
        Assert.Equal("1d", result1);

        var result2 = MsConverter.ToString(24 * 60 * 60 * 10000);
        Assert.Equal("10d", result2);

        var result3 = MsConverter.ToString(-1 * 24 * 60 * 60 * 1000);
        Assert.Equal("-1d", result3);

        var result4 = MsConverter.ToString(-1 * 24 * 60 * 60 * 10000);
        Assert.Equal("-10d", result4);
    }

    [Fact]
    public void ToString_Short_Round()
    {
        var result1 = MsConverter.ToString(234234234);
        Assert.Equal("3d", result1);

        var result2 = MsConverter.ToString(-234234234);
        Assert.Equal("-3d", result2);
    }

}