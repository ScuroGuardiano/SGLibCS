namespace SGLibCS.Ms.Tests;

public class MsConverter_ParseToTimeSpanShould
{
    [Theory]
    [InlineData("1 YeARs")]
    [InlineData("1 Year")]
    [InlineData("1 Yrs")]
    [InlineData("2135 Yr")]
    [InlineData("1 Y")]
    [InlineData("1 WEEKs")]
    [InlineData("1 Week")]
    [InlineData("1 W")]
    [InlineData("1 Days")]
    [InlineData("1 Day")]
    [InlineData("5 d")]
    [InlineData("1 Hours")]
    [InlineData("1 Hour")]
    [InlineData("1 Hrs")]
    [InlineData("1 Hr")]
    [InlineData("1 H")]
    [InlineData("1 MinutEs")]
    [InlineData("1 Minute")]
    [InlineData("1 Mins")]
    [InlineData("1 Min")]
    [InlineData("1 M")]
    [InlineData("1 Seconds")]
    [InlineData("1 Second")]
    [InlineData("1 Secs")]
    [InlineData("1 Sec")]
    [InlineData("5 s")]
    [InlineData("1 Milliseconds")]
    [InlineData("6 Millisecond")]
    [InlineData("1 Msecs")]
    [InlineData("1 Msec")]
    [InlineData("1 Ms")]
    public void ParseToTimeSpan_NotThrow(string input)
    {
        try
        {
            MsConverter.ParseToTimeSpan(input);
        }
        catch
        {
            Assert.Fail("MsConverter.ParseToTimeSpan should not throw on valid input");
        }
    }

    [Fact]
    public void ParseToTimeSpan_PreserveMs()
    {
        var expected = TimeSpan.FromMilliseconds(100);
        Assert.Equal(expected, MsConverter.ParseToTimeSpan("100"));
    }

    [Fact]
    public void ParseToTimeSpan_ConvertFromMToMs()
    {
        var expected = TimeSpan.FromMilliseconds(60000);
        Assert.Equal(expected, MsConverter.ParseToTimeSpan("1m"));
    }

    [Fact]
    public void ParseToTimeSpan_ConvertFromHToMs()
    {
        var expected = TimeSpan.FromMilliseconds(3600000);
        Assert.Equal(expected, MsConverter.ParseToTimeSpan("1h"));
    }

    [Fact]
    public void ParseToTimeSpan_ConvertFromDToMs()
    {
        var expected = TimeSpan.FromMilliseconds(172800000);
        Assert.Equal(expected, MsConverter.ParseToTimeSpan("2d"));
    }

    [Fact]
    public void ParseToTimeSpan_ConvertFromWToMs()
    {
        var expected = TimeSpan.FromMilliseconds(1814400000);
        Assert.Equal(expected, MsConverter.ParseToTimeSpan("3w"));
    }

    [Fact]
    public void ParseToTimeSpan_ConvertFromSToMs()
    {
        var expected = TimeSpan.FromMilliseconds(5000);
        Assert.Equal(expected, MsConverter.ParseToTimeSpan("5s"));
    }

    [Fact]
    public void ParseToTimeSpan_ConvertFromMsToMs()
    {
        var expected = TimeSpan.FromMilliseconds(1337);
        Assert.Equal(expected, MsConverter.ParseToTimeSpan("1337ms"));
    }

    [Fact]
    public void ParseToTimeSpan_ConvertFromYToMs()
    {
        var expected = TimeSpan.FromMilliseconds(31557600000);
        Assert.Equal(expected, MsConverter.ParseToTimeSpan("1y"));
    }

    [Fact]
    public void ParseToTimeSpan_WorkWithDecimal()
    {
        var expected = TimeSpan.FromMilliseconds(5400000);
        Assert.Equal(expected, MsConverter.ParseToTimeSpan("1.5h"));
    }

    [Fact]
    public void ParseToTimeSpan_WorkWithMultipleSpaces()
    {
        var expected = TimeSpan.FromMilliseconds(1000);
        Assert.Equal(expected, MsConverter.ParseToTimeSpan("1   s"));
    }

    [Theory]
    [InlineData("xD")]
    [InlineData("ms")]
    [InlineData("10-.5")]
    public void ParseToTimeSpan_ThrowOnInvalid(string input)
    {
        Assert.ThrowsAny<Exception>(() => {
            MsConverter.ParseToTimeSpan(input);
        });
    }

    [Fact]
    public void ParseToTimeSpan_IsCaseInsensitive()
    {
        var expected = TimeSpan.FromMilliseconds(5400000);
        Assert.Equal(expected, MsConverter.ParseToTimeSpan("1.5H"));
    }

    [Fact]
    public void ParseToTimeSpan_WorksWithNumbersStartingFromDot()
    {
        var expected = TimeSpan.FromMilliseconds(0.5);
        Assert.Equal(expected, MsConverter.ParseToTimeSpan(".5ms"));
    }

    [Fact]
    public void ParseToTimeSpan_WorksWithNegativeIntegers()
    {
        var expected = TimeSpan.FromMilliseconds(-100);
        Assert.Equal(expected, MsConverter.ParseToTimeSpan("-100ms"));
    }

    [Fact]
    public void ParseToTimeSpan_WorksWithNegativeDecimals()
    {
        var expected1 = TimeSpan.FromMilliseconds(-5400000);
        Assert.Equal(expected1, MsConverter.ParseToTimeSpan("-1.5h"));

        var expected2 = TimeSpan.FromMilliseconds(-47336400000);
        Assert.Equal(expected2, MsConverter.ParseToTimeSpan("-1.5y"));
    }

    [Fact]
    public void ParseToTimeSpan_WorksWithNegativeDecimalsStartingWithDot()
    {
        var expected = TimeSpan.FromMilliseconds(-1800000);
        Assert.Equal(expected, MsConverter.ParseToTimeSpan("-.5h"));
    }

    [Fact]
    public void ParseToTimeSpan_ConvertMillisecondsToMs()
    {
        var expected = TimeSpan.FromMilliseconds(53);
        Assert.Equal(expected, MsConverter.ParseToTimeSpan("53 milliseconds"));
    }

    [Fact]
    public void ParseToTimeSpan_ConvertMsecsToMs()
    {
        var expected = TimeSpan.FromMilliseconds(17);
        Assert.Equal(expected, MsConverter.ParseToTimeSpan("17 msecs"));
    }

    [Fact]
    public void ParseToTimeSpan_ConvertSecToMs()
    {
        var expected = TimeSpan.FromMilliseconds(1000);
        Assert.Equal(expected, MsConverter.ParseToTimeSpan("1 sec"));
    }

    [Fact]
    public void ParseToTimeSpan_ConvertSecsToMs()
    {
        var expected = TimeSpan.FromMilliseconds(5000);
        Assert.Equal(expected, MsConverter.ParseToTimeSpan("5 secs"));
    }

    [Fact]
    public void ParseToTimeSpan_ConvertMinToMs()
    {
        var expected = TimeSpan.FromMilliseconds(60000);
        Assert.Equal(expected, MsConverter.ParseToTimeSpan("1 min"));
    }

    [Fact]
    public void ParseToTimeSpan_ConvertHrToMs()
    {
        var expected = TimeSpan.FromMilliseconds(3600000);
        Assert.Equal(expected, MsConverter.ParseToTimeSpan("1 hr"));
    }

    [Fact]
    public void ParseToTimeSpan_ConvertDaysToMs()
    {
        var expected = TimeSpan.FromMilliseconds(172800000);
        Assert.Equal(expected, MsConverter.ParseToTimeSpan("2 days"));
    }

    [Fact]
    public void ParseToTimeSpan_ConvertWeekToMs()
    {
        var expected = TimeSpan.FromMilliseconds(604800000);
        Assert.Equal(expected, MsConverter.ParseToTimeSpan("1 week"));
    }

    [Fact]
    public void ParseToTimeSpan_ConvertYearToMs()
    {
        var expected = TimeSpan.FromMilliseconds(31557600000);
        Assert.Equal(expected, MsConverter.ParseToTimeSpan("1 year"));
    }

    [Fact]
    public void ParseToTimeSpan_ConvertLongWithDecimals()
    {
        var expected = TimeSpan.FromMilliseconds(5400000);
        Assert.Equal(expected, MsConverter.ParseToTimeSpan("1.5 hours"));
    }

    [Fact]
    public void ParseToTimeSpan_ConvertLongWithNegatives()
    {
        var expected = TimeSpan.FromMilliseconds(-100);
        Assert.Equal(expected, MsConverter.ParseToTimeSpan("-100 milliseconds"));
    }

    [Fact]
    public void ParseToTimeSpan_ConvertLongWithNegativeDecimals()
    {
        var expected = TimeSpan.FromMilliseconds(-5400000);
        Assert.Equal(expected, MsConverter.ParseToTimeSpan("-1.5 hour"));
    }

    [Fact]
    public void ParseToTimeSpan_ConverLongWithNegativeDecimalsStartingFromDot()
    {
        var expected = TimeSpan.FromMilliseconds(-1800000);
        Assert.Equal(expected, MsConverter.ParseToTimeSpan("-.5 hrs"));
    }
}
