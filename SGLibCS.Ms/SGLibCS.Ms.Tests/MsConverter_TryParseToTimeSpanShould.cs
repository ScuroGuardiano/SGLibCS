namespace SGLibCS.Ms.Tests;

public class MsConverter_TryParseToTimeSpanShould
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
    
    public void TryParseToTimeSpan_ReturnsTrueOnValidData(string input)
    {
        TimeSpan result;
        Assert.True(MsConverter.TryParseToTimespan(input, out result));
    }

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
    public void TryParseToTimeSpan_ResultTheSameAsParseToTimeSpan(string input)
    {
        TimeSpan result;
        MsConverter.TryParseToTimespan(input, out result);
        Assert.Equal(MsConverter.ParseToTimeSpan(input), result);
    }

    [Theory]
    [InlineData("xD")]
    [InlineData("ms")]
    [InlineData("10-.5")]
    public void TryParseToTimeSpan_ReturnsFalseOnInvalidData(string input)
    {
        TimeSpan result;
        Assert.False(MsConverter.TryParseToTimespan(input, out result));
    }
}
