namespace ScuroGuardiano.Slugify.Tests;

public class WithChangedReplacementTest
{
    private readonly Slugifier _slugifier = new SlugifierBuilder()
        .ReplaceWith('_')
        .Build();

    [Theory]
    [InlineData("Unions in C# actually sucks.", "unions_in_c__actually_sucks")]
    [InlineData("Closed records seems to be better.", "closed_records_seems_to_be_better")]
    public void GeneratingBasicSlug(string input, string expected)
    {
        var slug = _slugifier.Slugify(input);
        Assert.Equal(expected, slug);
    }

    [Theory]
    // Polish
    [InlineData("Zażółć gęślą jaźń", "zazolc_gesla_jazn")]
    [InlineData("Pchnąć w tę łódź jeża", "pchnac_w_te_lodz_jeza")]
    [InlineData("Łódź", "lodz")]
    [InlineData("Święty Mikołaj", "swiety_mikolaj")]

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
    [InlineData("¿Qué tal?", "que_tal")]
    [InlineData("Canción", "cancion")]
    [InlineData("Niño pequeño", "nino_pequeno")]

    // French
    [InlineData("Éléphant", "elephant")]
    [InlineData("Crème brûlée", "creme_brulee")]
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

    [Theory]
    [InlineData("  Smørrebrød  ", "smorrebrod")]
    [InlineData("   Zażółć gęślą jaźń", "zazolc_gesla_jazn")]
    [InlineData("Crème brûlée     ", "creme_brulee")]
    public void ShouldTrim(string input, string expected)
    {
        var slug = _slugifier.Slugify(input);
        Assert.Equal(expected, slug);
    }

    [Theory]
    [InlineData("?Smørrebrød", "smorrebrod")]
    [InlineData("Œuvre!", "oeuvre")]
    [InlineData("!!Übermäßig?^", "ubermassig")]
    public void ShouldTrimReplacements(string input, string expected)
    {
        var slug = _slugifier.Slugify(input);
        Assert.Equal(expected, slug);
    }
}