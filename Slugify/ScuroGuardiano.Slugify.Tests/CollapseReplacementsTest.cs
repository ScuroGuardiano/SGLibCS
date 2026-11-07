namespace ScuroGuardiano.Slugify.Tests;

public class CollapseReplacementsTest
{
    private readonly Slugifier _slugifier = new SlugifierBuilder()
        .CollapseReplacement()
        .Build();

    [Theory]
    [InlineData("Unions in C# actually sucks.", "unions-in-c-actually-sucks")]
    [InlineData("$$Zażółć   gęślą jaźń", "zazolc-gesla-jazn")]
    [InlineData("Pchnąć w tę %$? łódź jeża", "pchnac-w-te-lodz-jeza")]
    [InlineData("Łó====dź", "lo-dz")]
    [InlineData("Święty Mik#@ołaj", "swiety-mik-olaj")]
    [InlineData("Str#@$aße", "str-asse")]
    [InlineData("Sch<>>ön", "sch-on")]
    public void ShouldCollapseReplacements(string input, string expected)
    {
        var slug = _slugifier.Slugify(input);
        Assert.Equal(expected, slug);
    }
}