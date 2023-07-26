namespace SGLibCS.Ms.Tests;

public class MsConverter_ParseShould
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
    public void Parse_NotThrow(string input)
    {
        try
        {
            MsConverter.Parse(input);
        }
        catch
        {
            Assert.Fail("MsConverter.Parse should not throw on valid input");
        }
    }


    [Fact]
    public void Parse_PreserveMs()
    {
        Assert.Equal(100, MsConverter.Parse("100"));
    }

    [Fact]
    public void Parse_ConvertFromMToMs()
    {
        Assert.Equal(60000, MsConverter.Parse("1m"));
    }

    [Fact]
    public void Parse_ConvertFromHToMs()
    {
        Assert.Equal(3600000, MsConverter.Parse("1h"));
    }

    [Fact]
    public void Parse_ConvertFromDToMs()
    {
        Assert.Equal(172800000, MsConverter.Parse("2d"));
    }

    [Fact]
    public void Parse_ConvertFromWToMs()
    {
        Assert.Equal(1814400000, MsConverter.Parse("3w"));
    }

    [Fact]
    public void Parse_ConvertFromSToMs()
    {
        Assert.Equal(5000, MsConverter.Parse("5s"));
    }

    [Fact]
    public void Parse_ConvertFromMsToMs()
    {
        Assert.Equal(1337, MsConverter.Parse("1337ms"));
    }

    [Fact]
    public void Parse_ConvertFromYToMs()
    {
        Assert.Equal(31557600000, MsConverter.Parse("1y"));
    }

    [Fact]
    public void Parse_WorkWithDecimal()
    {
        Assert.Equal(5400000, MsConverter.Parse("1.5h"));
    }

    [Fact]
    public void Parse_WorkWithMultipleSpaces()
    {
        Assert.Equal(1000, MsConverter.Parse("1   s"));
    }

    [Theory]
    [InlineData("xD")]
    [InlineData("ms")]
    [InlineData("10-.5")]
    public void Parse_ThrowOnInvalid(string input)
    {
        Assert.ThrowsAny<Exception>(() => {
            MsConverter.Parse(input);
        });
    }

    [Fact]
    public void Parse_IsCaseInsensitive()
    {
        Assert.Equal(5400000, MsConverter.Parse("1.5H"));
    }

    [Fact]
    public void Parse_WorksWithNumbersStartingFromDot()
    {
        Assert.Equal(0.5, MsConverter.Parse(".5ms"));
    }

    [Fact]
    public void Parse_WorksWithNegativeIntegers()
    {
        Assert.Equal(-100, MsConverter.Parse("-100ms"));
    }

    [Fact]
    public void Parse_WorksWithNegativeDecimals()
    {
        Assert.Equal(-5400000, MsConverter.Parse("-1.5h"));
        Assert.Equal(-47336400000, MsConverter.Parse("-1.5y"));
    }

    [Fact]
    public void Parse_WorksWithNegativeDecimalsStartingWithDot()
    {
        Assert.Equal(-1800000, MsConverter.Parse("-.5h"));
    }

    [Fact]
    public void Parse_ConvertMillisecondsToMs()
    {
        Assert.Equal(53, MsConverter.Parse("53 milliseconds"));
    }

    [Fact]
    public void Parse_ConvertMsecsToMs()
    {
        Assert.Equal(17, MsConverter.Parse("17 msecs"));
    }

    [Fact]
    public void Parse_ConvertSecToMs()
    {
        Assert.Equal(1000, MsConverter.Parse("1 sec"));
    }

    [Fact]
    public void Parse_ConvertSecsToMs()
    {
        Assert.Equal(5000, MsConverter.Parse("5 secs"));
    }

    [Fact]
    public void Parse_ConvertMinToMs()
    {
        Assert.Equal(60000, MsConverter.Parse("1 min"));
    }

    [Fact]
    public void Parse_ConvertHrToMs()
    {
        Assert.Equal(3600000, MsConverter.Parse("1 hr"));
    }

    [Fact]
    public void Parse_ConvertDaysToMs()
    {
        Assert.Equal(172800000, MsConverter.Parse("2 days"));
    }

    [Fact]
    public void Parse_ConvertWeekToMs()
    {
        Assert.Equal(604800000, MsConverter.Parse("1 week"));
    }

    [Fact]
    public void Parse_ConvertYearToMs()
    {
        Assert.Equal(31557600000, MsConverter.Parse("1 year"));
    }

    [Fact]
    public void Parse_ConvertLongWithDecimals()
    {
        Assert.Equal(5400000, MsConverter.Parse("1.5 hours"));
    }

    [Fact]
    public void Parse_ConvertLongWithNegatives()
    {
        Assert.Equal(-100, MsConverter.Parse("-100 milliseconds"));
    }

    [Fact]
    public void Parse_ConvertLongWithNegativeDecimals()
    {
        Assert.Equal(-5400000, MsConverter.Parse("-1.5 hour"));
    }

    [Fact]
    public void Parse_ConverLongWithNegativeDecimalsStartingFromDot()
    {
        Assert.Equal(-1800000, MsConverter.Parse("-.5 hrs"));
    }
}