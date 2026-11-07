namespace ScuroGuardiano.Slugify.Tests;

public class WithoutLowercaseConversionTest
{
    private readonly Slugifier _slugifier = new SlugifierBuilder()
        .WithoutLowercaseConvertion()
        .Build();

    [Theory]
    [InlineData("Unions in C# actually sucks.", "Unions-in-C--actually-sucks")]
    [InlineData("Closed records seems to be better.", "Closed-records-seems-to-be-better")]
    public void GeneratingBasicSlug(string input, string expected)
    {
        var slug = _slugifier.Slugify(input);
        Assert.Equal(expected, slug);
    }

    [Theory]
    // Polish
    [InlineData("Zażółć gęślą jaźń", "Zazolc-gesla-jazn")]
    [InlineData("Pchnąć w tę łódź jeża", "Pchnac-w-te-lodz-jeza")]
    [InlineData("Łódź", "Lodz")]
    [InlineData("Święty Mikołaj", "Swiety-Mikolaj")]

    // German
    [InlineData("Straße", "Strasse")]
    [InlineData("Fußball", "Fussball")]
    [InlineData("Größe", "Grosse")]
    [InlineData("Schön", "Schon")]
    [InlineData("Übermäßig", "Ubermassig")]
    [InlineData("Ärger", "Arger")]
    [InlineData("Österreich", "Osterreich")]
    [InlineData("Mädchen", "Madchen")]

    // Spanish
    [InlineData("Año", "Ano")]
    [InlineData("España", "Espana")]
    [InlineData("Corazón", "Corazon")]
    [InlineData("Pingüino", "Pinguino")]
    [InlineData("Málaga", "Malaga")]
    [InlineData("¿Qué tal?", "Que-tal")]
    [InlineData("Canción", "Cancion")]
    [InlineData("Niño pequeño", "Nino-pequeno")]

    // French
    [InlineData("Éléphant", "Elephant")]
    [InlineData("Crème brûlée", "Creme-brulee")]
    [InlineData("Français", "Francais")]
    [InlineData("Garçon", "Garcon")]
    [InlineData("Cœur", "Coeur")]
    [InlineData("Œuvre", "OEuvre")]

    // Scandinavian
    [InlineData("Smørrebrød", "Smorrebrod")]
    [InlineData("Ålesund", "Alesund")]
    [InlineData("Björk", "Bjork")]
    [InlineData("Æther", "AEther")]
    [InlineData("Håkon", "Hakon")]

    public void HandlingDiacritics(string input, string expected)
    {
        var slug = _slugifier.Slugify(input);
        Assert.Equal(expected, slug);
    }

    [Theory]
    [InlineData("  Smørrebrød  ", "Smorrebrod")]
    [InlineData("   Zażółć gęślą jaźń", "Zazolc-gesla-jazn")]
    [InlineData("Crème brûlée     ", "Creme-brulee")]
    public void ShouldTrim(string input, string expected)
    {
        var slug = _slugifier.Slugify(input);
        Assert.Equal(expected, slug);
    }

    [Theory]
    [InlineData("?Smørrebrød", "Smorrebrod")]
    [InlineData("Œuvre!", "OEuvre")]
    [InlineData("!!Übermäßig?^", "Ubermassig")]
    public void ShouldTrimReplacements(string input, string expected)
    {
        var slug = _slugifier.Slugify(input);
        Assert.Equal(expected, slug);
    }

    [Theory]
    [InlineData("Straße STRASSE", "Strasse-STRASSE")]
    [InlineData("ÜBERMÄSSIG", "UBERMASSIG")]
    [InlineData("übermäßig", "ubermassig")]
    [InlineData("ÉLÉPHANT éléphant", "ELEPHANT-elephant")]
    [InlineData("ZaŻÓŁĆ", "ZaZOLC")]
    public void ShouldPreserveLetterCase(string input, string expected)
    {
        var slug = _slugifier.Slugify(input);
        Assert.Equal(expected, slug);
    }
}
