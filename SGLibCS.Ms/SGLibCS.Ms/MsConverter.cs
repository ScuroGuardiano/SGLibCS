namespace SGLibCS.Ms;

using System;
using System.Text.RegularExpressions;

public class MsConverter
{
    public static double Parse(string input)
    {
        if (input.Length > 100)
        {
            throw new ArgumentException("Value exceeds the maximum length of 100 characters.", nameof(input));
        }

        var match = Regex.Match(
            input,
            @"^(?<value>-?(?:\d+)?\.?\d+) *(?<type>milliseconds?|msecs?|ms|seconds?|secs?|s|minutes?|mins?|m|hours?|hrs?|h|days?|d|weeks?|w|years?|yrs?|y)?$",
            RegexOptions.IgnoreCase
        );

        if (!match.Success)
        {
            throw new FormatException($"Input {input} is in invalid format.");
        }

        var value = match.Groups["value"].Value;
        var type = match.Groups["type"].Value.ToLower();

        if (type == String.Empty)
        {
            type = "ms";
        }

        double n = Double.Parse(value);

        switch (type)
        {
            case "years":
            case "year":
            case "yrs":
            case "yr":
            case "y":
                return n * y;
            case "weeks":
            case "week":
            case "w":
                return n * w;
            case "days":
            case "day":
            case "d":
                return n * d;
            case "hours":
            case "hour":
            case "hrs":
            case "hr":
            case "h":
                return n * h;
            case "minutes":
            case "minute":
            case "mins":
            case "min":
            case "m":
                return n * m;
            case "seconds":
            case "second":
            case "secs":
            case "sec":
            case "s":
                return n * s;
            case "milliseconds":
            case "millisecond":
            case "msecs":
            case "msec":
            case "ms":
                return n;
            default:
                // This should never happen
                throw new Exception($"The unit {type} was matched, but no matching exists.");
        }
    }

    public static bool TryParse(string input, out double result)
    {
        try
        {
            result = Parse(input);
            return true;
        }
        catch
        {
            result = 0;
            return false;
        }
    }

    public static TimeSpan ParseToTimeSpan(string input)
    {
        return TimeSpan.FromMilliseconds(Parse(input));
    }

    public static bool TryParseToTimespan(string input, out TimeSpan result)
    {
        try
        {
            result = ParseToTimeSpan(input);
            return true;
        }
        catch
        {
            result = TimeSpan.Zero;
            return false;
        }
    }

    public static string ToString(double input)
    {
        return ToString(input, new Options());
    }

    public static string ToString(double input, Options options)
    {
        if (!Double.IsFinite(input))
        {
            throw new ArgumentException("Input must be finite.", nameof(input));
        }
        return options.Long ? LongFormat(input) : ShortFormat(input);
    }

    public class Options
    {
        public bool Long  { get; set; } = false;
    }

    // Helpers
    private static string ShortFormat(double ms)
    {
        var msAbs = Math.Abs(ms);
        
        if (msAbs >= d)
        {
            return $"{Math.Round(ms / d)}d";
        }
        if (msAbs >= h)
        {
            return $"{Math.Round(ms / h)}h";
        }
        if (msAbs >= m)
        {
            return $"{Math.Round(ms / m)}m";
        }
        if (msAbs >= s)
        {
            return $"{Math.Round(ms / s)}s";
        }
        return $"{ms}ms";
    }

    private static string LongFormat(double ms)
    {
        var msAbs = Math.Abs(ms);
        
        if (msAbs >= d)
        {
            return Plural(ms, msAbs, d, "day");
        }
        if (msAbs >= h)
        {
            return Plural(ms, msAbs, h, "hour");
        }
        if (msAbs >= m)
        {
            return Plural(ms, msAbs, m, "minute");
        }
        if (msAbs >= s)
        {
            return Plural(ms, msAbs, s, "second ");
        }

        return $"{ms} ms";
    }

    private static string Plural(double ms, double msAbs, double n, string name)
    {
        var isPlural = msAbs >= n * 1.5;
        var pluralLetter = isPlural ? "s" : "";
        return $"{Math.Round(ms / n)} {name}{pluralLetter}";
        // return `${Math.round(ms / n)} ${name}${isPlural ? 's' : ''}` as StringValue;
    }

    private static double s { get => 1000; }
    private static double m { get => s * 60; }
    private static double h { get => m * 60; }
    private static double d { get => h * 24; }
    private static double w { get => d * 7; }
    private static double y { get => d * 365.25; }
}
