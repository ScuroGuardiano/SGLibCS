// See https://aka.ms/new-console-template for more information
using System.Text.RegularExpressions;
using SGLibCS.Ms;

Console.WriteLine(MsConverter.ParseToTimeSpan("2h").ToString());            // 02:00:00
Console.WriteLine(MsConverter.ToString(3600000));                           // 1h
Console.WriteLine(MsConverter.ToString(3600000, new() { Long = true }));    // 1 hour
Console.WriteLine(MsConverter.ToString(7200000, new() { Long = true }));    // 2 hours

string x = "";
string y = $"x{x}";
Console.WriteLine(y.Length);