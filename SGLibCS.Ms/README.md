# SGLibCS.Ms - Port of [vercel/ms](https://github.com/vercel/ms) for .NET

Use this package to easily convert various time formats to milliseconds.

API is a little bit different than the original version, better suited for C#.

## Examples
```cs
using SGLibCS.Ms;

MsConverter.Parse("2 days");  // 172800000
MsConverter.Parse("1d");      // 86400000
MsConverter.Parse("10h");     // 36000000
MsConverter.Parse("2.5 hrs"); // 9000000
MsConverter.Parse("2h");      // 7200000
MsConverter.Parse("1m");      // 60000
MsConverter.Parse("5s");      // 5000
MsConverter.Parse("1y");      // 31557600000
MsConverter.Parse("100");     // 100
MsConverter.Parse("-3 days"); // -259200000
MsConverter.Parse("-1h");     // -3600000
MsConverter.Parse("-200");    // -200
```

### Parse will throw
* Parse will throw `ArgumentException` if input is null, empty or exceeds 100 characters
* Parse will throw `FormatException` if input format is invalid.

### Not throwing version
```cs
double result;
bool success = MsConverter.TryParse("2 days", out result);
// result: 172800000; success: true

double result;
bool success = MsConverter.TryParse("", out result);
// result: 0; success: false
```

### Parsing to TimeSpan
```cs
// Throwing
TimeSpan ts = MsConverter.ParseToTimeSpan("1h");

// Not throwing
TimeSpan ts;
bool success = MsConverter.TryParseToTimeSpan("1h", out ts);
```

### Convert from Milliseconds
```cs
MsConverter.ToString(60000);       // 1m
MsConverter.ToString(2 * 60000);   // 2m
MsConverter.ToString(-3 * 60000);  // -3m
MsConverter.ToString(
    MsConverter.Parse("10 hours")) // 10h
```

### Time Format Written-Out
```cs
MsConverter.Options opts = new () { Long = true };
MsConverter.ToString(60000, opts);      // 1 minute
MsConverter.ToString(2 * 60000, opts);  // 2 minutes
MsConverter.ToString(-3 * 60000, opts); // -3 minutes
MsConverter.ToString(
    MsCoverter.Parse("10h"), opts);     // 10 hours
```

### Todo
* Data annotation attribute for MsFormat validation so someone can easy verify in Dto or Options.

### Related packages
* [vercel/ms](https://github.com/vercel/ms) - original JS package.

## LICENSE
MIT
