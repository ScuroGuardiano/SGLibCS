using BenchmarkDotNet.Attributes;

namespace ScuroGuardiano.Slugify.Benchmarks;

[MemoryDiagnoser]
public class SlugifierBenchmarks
{
    private const string ShortSlug = "Co tam mordo?";
    private const string LongSlug = "Zażółć gęślą jaźń, dodaję random słowa żeby było dłuższe, co ja kuźwa robię z życiem, let's go.";

    private readonly Slugifier _slugifier = new SlugifierBuilder()
        .CollapseReplacement()
        .Build();

    [Benchmark]
    public string CreateShortSlug()
    {
        return _slugifier.Slugify(ShortSlug);
    }

    [Benchmark]
    public string CreateLongSlug()
    {
        return _slugifier.Slugify(LongSlug);
    }
}
