# ScuroGuardiano.Net Slugify

Library for creating slugs from text.

## Example usage:
```csharp
Slugifier slugifier = new SlugifierBuilder()
    .CollapseReplacement()
    .Build();

var slug = slugifier.Slugify("Unions in C# actually sucks.");
// slug will be: unions-in-c-actually-sucks
// and yeah, it should be 'suck" not 'sucks', leave me alone, english is not my native language
```

Should work with many diacritics, like Polish, German, Spanish, Scandinavian.
Apache Lucene's `ASCIIFolder` was used to achieve it.

It doesn't support other alphabets than Latin-based.

<details>
<summary>Test cases for diacritics</summary>

```csharp
    [Theory]
    // Polish
    [InlineData("Zażółć gęślą jaźń", "zazolc-gesla-jazn")]
    [InlineData("Pchnąć w tę łódź jeża", "pchnac-w-te-lodz-jeza")]
    [InlineData("Łódź", "lodz")]
    [InlineData("Święty Mikołaj", "swiety-mikolaj")]

    // German
    [InlineData("Straße", "strasse")]
    [InlineData("Fußball", "fussball")]
    [InlineData("Größe", "grosse")]
    [InlineData("Schön", "schon")]
    [InlineData("Übermäßig", "ubermassig")]
    [InlineData("Ärger", "arger")]
    [InlineData("Österreich", "osterreich")]
    [InlineData("Mädchen", "madchen")]

    // Spanish
    [InlineData("Año", "ano")]
    [InlineData("España", "espana")]
    [InlineData("Corazón", "corazon")]
    [InlineData("Pingüino", "pinguino")]
    [InlineData("Málaga", "malaga")]
    [InlineData("¿Qué tal?", "que-tal")]
    [InlineData("Canción", "cancion")]
    [InlineData("Niño pequeño", "nino-pequeno")]

    // French
    [InlineData("Éléphant", "elephant")]
    [InlineData("Crème brûlée", "creme-brulee")]
    [InlineData("Français", "francais")]
    [InlineData("Garçon", "garcon")]
    [InlineData("Cœur", "coeur")]
    [InlineData("Œuvre", "oeuvre")]

    // Scandinavian
    [InlineData("Smørrebrød", "smorrebrod")]
    [InlineData("Ålesund", "alesund")]
    [InlineData("Björk", "bjork")]
    [InlineData("Æther", "aether")]
    [InlineData("Håkon", "hakon")]

    public void HandlingDiacritics(string input, string expected)
    {
        var slug = _slugifier.Slugify(input);
        Assert.Equal(expected, slug);
    }
```

</details>

## Performance
It's not implemented in most performant way but my benchmarks showed 500ns for pretty long string, so for me it's whatever. I wonder who has slugify in their hot path.

## Credits
This project contains code from [Apache Lucene](https://github.com/apache/lucenenet) project, which is licenced under Apache 2.0 license.
***The code was modified by me***.

Full text of their license is available in `APACHE-LUCENE-LICENSE` file.

## License
MIT
